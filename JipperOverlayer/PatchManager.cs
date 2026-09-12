using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JipperOverlayer;

internal static class PatchManager
{
    private static Harmony _harmony;
    private static string _harmonyId;
    private static readonly Dictionary<string, object> _delegateCache = new();
    private static readonly Dictionary<string, MethodInfo> _methodCache = new();
    private static readonly Dictionary<string, FieldInfo> _fieldCache = new();
    private static readonly Dictionary<Type, PatchRegistration> _registeredPatches = new();
    private static readonly HashSet<Type> _appliedPatches = new();
    private static readonly HashSet<Type> _failedPatches = new();
    private static readonly List<MethodInfo> _appliedManualPatches = new();
    private static readonly object _lock = new();
    private static CancellationTokenSource _lazyPollCts;
    private static Task _lazyPollTask;
    private static volatile bool _isQuitting = false;
    private static bool _sceneIsLoading = false;

    public static void Initialize(Harmony harmony)
    {
        lock (_lock)
        {
            _harmony = harmony;
            _harmonyId = harmony.Id;
            _registeredPatches.Clear();
            _appliedPatches.Clear();
            _failedPatches.Clear();
            _delegateCache.Clear();
            _methodCache.Clear();
            _fieldCache.Clear();
            _isQuitting = false; // 重置
        }
        Application.quitting -= OnApplicationQuitting;
        Application.quitting += OnApplicationQuitting;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnApplicationQuitting()
    {
        _isQuitting = true;
        lock (_lock)
        {
            _lazyPollCts?.Cancel();
        }
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        _sceneIsLoading = true;  // 暂停轮询
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _sceneIsLoading = false; // 恢复轮询
    }

    public static void RegisterPatch(Type patchType, Func<bool> toggle = null, string versionTag = null)
    {
        lock (_lock)
        {
            _registeredPatches[patchType] = new PatchRegistration(patchType, toggle ?? (() => true));
        }
        Debug.Log(string.IsNullOrEmpty(versionTag)
            ? $"[PatchManager] Registered patch: {patchType.Name}"
            : $"[PatchManager] Registered patch: {patchType.Name} [{versionTag}]");
    }

    public static void RegisterPatches(Func<bool> toggle, params Type[] patchTypes)
    {
        foreach (var patchType in patchTypes)
            RegisterPatch(patchType, toggle);
    }

    /// <summary>带版本标注的注册：日志里标明该补丁适用的游戏版本区间，便于排查版本分支。</summary>
    public static void RegisterPatches(Func<bool> toggle, string versionTag, params Type[] patchTypes)
    {
        foreach (var patchType in patchTypes)
            RegisterPatch(patchType, toggle, versionTag);
    }

    public static void RefreshPatches()
    {
        lock (_lock)
        {
            if (_harmony == null) return;
            foreach (var registration in _registeredPatches.Values)
            {
                if (registration.IsLazy) continue;
                bool isApplied = _appliedPatches.Contains(registration.PatchType);
                bool shouldBeEnabled = registration.IsEnabled();
                if (shouldBeEnabled && !isApplied)
                {
                    try
                    {
                        _harmony.CreateClassProcessor(registration.PatchType).Patch();
                        _appliedPatches.Add(registration.PatchType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                    }
                }
                else if (!shouldBeEnabled && isApplied)
                {
                    try
                    {
                        _harmony.CreateClassProcessor(registration.PatchType).Unpatch();
                        _appliedPatches.Remove(registration.PatchType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to unpatch {registration.PatchType.Name}: {e.Message}");
                    }
                }
            }
        }
    }

    public static void RegisterLazyPatches(Func<bool> trigger, params Type[] patchTypes)
    {
        lock (_lock)
        {
            foreach (var patchType in patchTypes)
                _registeredPatches[patchType] = new PatchRegistration(patchType, () => true, trigger);
        }
        Debug.Log($"[PatchManager] Registered lazy patches: {string.Join(", ", patchTypes.Select(t => t.Name))}");
    }

    public static void RegisterManualPrefix(MethodInfo targetMethod, MethodInfo prefixMethod)
    {
        if (targetMethod == null || prefixMethod == null) return;
        lock (_lock)
        {
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
            _appliedManualPatches.Add(targetMethod);
        }
        Debug.Log($"Applied manual prefix patch: {targetMethod.DeclaringType.Name}.{targetMethod.Name} -> {prefixMethod.Name}");
    }

    public static void RegisterManualPatch(MethodInfo targetMethod, MethodInfo postfixMethod)
    {
        if (targetMethod == null || postfixMethod == null) return;
        lock (_lock)
        {
            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            _appliedManualPatches.Add(targetMethod);
        }
        Debug.Log($"Applied manual patch: {targetMethod.DeclaringType.Name}.{targetMethod.Name} -> {postfixMethod.Name}");
    }

    public static void UnpatchManualPrefix(MethodInfo targetMethod)
    {
        if (targetMethod == null) return;
        lock (_lock)
        {
            _harmony.Unpatch(targetMethod, HarmonyPatchType.Prefix, _harmonyId);
            _appliedManualPatches.Remove(targetMethod);
        }
        Debug.Log($"Unpatched manual prefix: {targetMethod.DeclaringType.Name}.{targetMethod.Name}");
    }

    public static void UnpatchManualPostfix(MethodInfo targetMethod)
    {
        if (targetMethod == null) return;
        lock (_lock)
        {
            _harmony.Unpatch(targetMethod, HarmonyPatchType.Postfix, _harmonyId);
            _appliedManualPatches.Remove(targetMethod);
        }
        Debug.Log($"Unpatched manual postfix: {targetMethod.DeclaringType.Name}.{targetMethod.Name}");
    }

    public static void ApplyAll()
    {
        lock (_lock)
        {
            if (_harmony == null) return;
            foreach (var registration in _registeredPatches.Values)
            {
                if (_appliedPatches.Contains(registration.PatchType)) continue;
                if (!registration.IsEnabled()) continue;
                if (registration.IsLazy) continue;
                try
                {
                    _harmony.CreateClassProcessor(registration.PatchType).Patch();
                    _appliedPatches.Add(registration.PatchType);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                }
            }
        }
    }

    public static void ApplyPatch(Type patchType)
    {
        if (patchType == null) throw new ArgumentNullException(nameof(patchType));
        lock (_lock)
        {
            if (!_registeredPatches.ContainsKey(patchType))
                throw new InvalidOperationException($"Patch type {patchType.FullName} is not registered");
            if (_appliedPatches.Contains(patchType)) return;
            _harmony.CreateClassProcessor(patchType).Patch();
            _appliedPatches.Add(patchType);
        }
    }

    public static void UnpatchPatch(Type patchType)
    {
        if (patchType == null) throw new ArgumentNullException(nameof(patchType));
        lock (_lock)
        {
            if (!_registeredPatches.ContainsKey(patchType))
                throw new InvalidOperationException($"Patch type {patchType.FullName} is not registered");
            if (!_appliedPatches.Contains(patchType)) return;
            _harmony.CreateClassProcessor(patchType).Unpatch();
            _appliedPatches.Remove(patchType);
        }
    }

    public static void ApplyLazyPatchesAsync()
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            if (_lazyPollCts != null) return;
            cts = _lazyPollCts = new CancellationTokenSource();
        }
        var task = ApplyLazyPatchesAsyncCore(cts);
        lock (_lock)
        {
            if (_lazyPollCts == cts)
                _lazyPollTask = task;
        }
    }

    private static async Task ApplyLazyPatchesAsyncCore(CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            while (!token.IsCancellationRequested && !_isQuitting)
            {
                if (!_sceneIsLoading)
                {
                    try
                    {
                        ApplyLazyPatches();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PatchManager] ApplyLazyPatches error: {ex}");
                    }
                }
                // 无论是否在场景加载中都要让出线程——否则场景加载期间此循环会无延时空转烧 CPU。
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[PatchManager] Async patch task fatal error: {ex}");
        }
        finally
        {
            lock (_lock)
            {
                if (_lazyPollCts == cts) _lazyPollCts = null;
            }
            cts.Dispose();
        }
    }

    private static void ApplyLazyPatches()
    {
        if (_isQuitting || _sceneIsLoading) return;
        lock (_lock)
        {
            if (_harmony == null) return;
            foreach (var registration in _registeredPatches.Values)
            {
                if (!registration.IsLazy) continue;
                if (_failedPatches.Contains(registration.PatchType)) continue;
                bool isApplied = _appliedPatches.Contains(registration.PatchType);
                bool shouldBeEnabled = registration.IsEnabled() && registration.LazyTrigger();
                if (shouldBeEnabled && !isApplied)
                {
                    try
                    {
                        _harmony.CreateClassProcessor(registration.PatchType).Patch();
                        _appliedPatches.Add(registration.PatchType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                        _failedPatches.Add(registration.PatchType);
                    }
                }
                else if (!shouldBeEnabled && isApplied)
                {
                    try
                    {
                        _harmony.CreateClassProcessor(registration.PatchType).Unpatch();
                        _appliedPatches.Remove(registration.PatchType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to unpatch patch {registration.PatchType.Name}: {e.Message}");
                    }
                }
            }
        }
    }

    public static void UnpatchAll()
    {
        // 设置退出标志，阻止新的轮询操作
        _isQuitting = true;

        CancellationTokenSource cts;
        Task task;
        lock (_lock)
        {
            cts = _lazyPollCts;
            task = _lazyPollTask;
            _lazyPollCts = null;
        }
        cts?.Cancel();

        if (task != null)
        {
            try
            {
                if (!task.Wait(2000))
                {
                    Debug.LogWarning("[PatchManager] Lazy poll task did not stop within 2s, continuing anyway.");
                }
            }
            catch (AggregateException ae)
            {
                foreach (var inner in ae.InnerExceptions)
                    Debug.LogWarning($"[PatchManager] Lazy task exception: {inner.Message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PatchManager] Lazy task wait failed: {e.Message}");
            }
        }

        Application.quitting -= OnApplicationQuitting;
        lock (_lock)
        {
            _harmony?.UnpatchAll(_harmonyId);
            _appliedPatches.Clear();
            _failedPatches.Clear();
            _appliedManualPatches.Clear();
        }
    }

    public static MethodInfo GetMethodInfo(Type declaringType, string methodName, Type[] parameters = null, Type[] generics = null)
    {
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name cannot be empty", nameof(methodName));

        string key = $"{declaringType.FullName}.{methodName}";
        if (parameters != null)
            key += "_" + string.Join(",", parameters.Select(t => t.FullName));
        if (generics != null)
            key += "_generic_" + string.Join(",", generics.Select(t => t.FullName));

        lock (_lock)
        {
            if (_methodCache.TryGetValue(key, out var cached))
                return cached;

            MethodInfo method;
            if (parameters != null)
                method = AccessTools.Method(declaringType, methodName, parameters, generics);
            else
                method = AccessTools.Method(declaringType, methodName, null, generics);

            if (method == null)
                throw new MissingMethodException($"Cannot find method {methodName} in {declaringType}");

            _methodCache[key] = method;
            return method;
        }
    }

    public static FieldInfo GetFieldInfo(Type declaringType, string fieldName)
    {
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));

        string key = $"{declaringType.FullName}.{fieldName}";

        lock (_lock)
        {
            if (_fieldCache.TryGetValue(key, out var cached))
                return cached;

            var field = AccessTools.Field(declaringType, fieldName);
            if (field == null)
                throw new MissingFieldException($"Cannot find field {fieldName} in {declaringType}");

            _fieldCache[key] = field;
            return field;
        }
    }

    public static AccessTools.FieldRef<T, F> CreateFieldRef<T, F>(string fieldName) where T : class
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be empty", nameof(fieldName));

        var key = $"Field:{typeof(T).FullName}.{fieldName}_{typeof(F).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (AccessTools.FieldRef<T, F>)cached;

            var fieldRef = AccessTools.FieldRefAccess<T, F>(fieldName);
            _delegateCache[key] = fieldRef;
            return fieldRef;
        }
    }

    public static Func<T, F> CreatePropertyGetter<T, F>(string propertyName) where T : class
    {
        var key = $"PropGet:{typeof(T).FullName}.{propertyName}_{typeof(F).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<T, F>)cached;

            var prop = AccessTools.Property(typeof(T), propertyName);
            if (prop == null) throw new MissingMemberException($"Property '{propertyName}' not found on {typeof(T)}");
            var getMethod = prop.GetGetMethod(true);
            if (getMethod == null) throw new InvalidOperationException($"Property '{propertyName}' has no getter");

            var del = (Func<T, F>)Delegate.CreateDelegate(typeof(Func<T, F>), getMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<T, F> CreatePropertySetter<T, F>(string propertyName) where T : class
    {
        var key = $"PropSet:{typeof(T).FullName}.{propertyName}_{typeof(F).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<T, F>)cached;

            var prop = AccessTools.Property(typeof(T), propertyName);
            if (prop == null) throw new MissingMemberException($"Property '{propertyName}' not found on {typeof(T)}");
            var setMethod = prop.GetSetMethod(true);
            if (setMethod == null) throw new InvalidOperationException($"Property '{propertyName}' has no setter");

            var del = (Action<T, F>)Delegate.CreateDelegate(typeof(Action<T, F>), setMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Func<TField> CreateStaticFieldGetter<TField>(Type declaringType, string fieldName)
    {
        var key = $"StaticFieldGet:{declaringType.FullName}.{fieldName}_{typeof(TField).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<TField>)cached;

            var fi = AccessTools.Field(declaringType, fieldName);
            if (fi == null) throw new MissingMemberException($"{declaringType}.{fieldName}");
            if (!fi.IsStatic) throw new ArgumentException("Field is not static");

            var method = new DynamicMethod($"get_{fieldName}", typeof(TField), Type.EmptyTypes, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, fi);
            il.Emit(OpCodes.Ret);
            var del = (Func<TField>)method.CreateDelegate(typeof(Func<TField>));
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<TField> CreateStaticFieldSetter<TField>(Type declaringType, string fieldName)
    {
        var key = $"StaticFieldSet:{declaringType.FullName}.{fieldName}_{typeof(TField).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<TField>)cached;

            var fi = AccessTools.Field(declaringType, fieldName);
            if (fi == null) throw new MissingMemberException($"{declaringType}.{fieldName}");
            if (!fi.IsStatic) throw new ArgumentException("Field is not static");

            var method = new DynamicMethod($"set_{fieldName}", typeof(void), new[] { typeof(TField) }, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, fi);
            il.Emit(OpCodes.Ret);
            var del = (Action<TField>)method.CreateDelegate(typeof(Action<TField>));
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Func<TField> CreateStaticPropertyGetter<TField>(Type declaringType, string propertyName)
    {
        var key = $"StaticPropGet:{declaringType.FullName}.{propertyName}_{typeof(TField).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<TField>)cached;

            var prop = AccessTools.Property(declaringType, propertyName);
            if (prop == null) throw new MissingMemberException($"{declaringType}.{propertyName}");
            var getMethod = prop.GetGetMethod(true);
            if (getMethod == null) throw new InvalidOperationException("Property has no getter");

            var del = (Func<TField>)Delegate.CreateDelegate(typeof(Func<TField>), getMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<TField> CreateStaticPropertySetter<TField>(Type declaringType, string propertyName)
    {
        var key = $"StaticPropSet:{declaringType.FullName}.{propertyName}_{typeof(TField).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<TField>)cached;

            var prop = AccessTools.Property(declaringType, propertyName);
            if (prop == null) throw new MissingMemberException($"{declaringType}.{propertyName}");
            var setMethod = prop.GetSetMethod(true);
            if (setMethod == null) throw new InvalidOperationException("Property has no setter");

            var del = (Action<TField>)Delegate.CreateDelegate(typeof(Action<TField>), setMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Func<T, object> CreateMemberGetter<T>(string name) where T : class
    {
        var key = $"MemberGet:{typeof(T).FullName}.{name}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<T, object>)cached;

            var field = AccessTools.Field(typeof(T), name);
            if (field != null)
            {
                var getter = new Func<T, object>(instance => instance == null ? null : field.GetValue(instance));
                _delegateCache[key] = getter;
                return getter;
            }

            var prop = AccessTools.Property(typeof(T), name);
            if (prop != null)
            {
                var getMethod = prop.GetGetMethod(true);
                if (getMethod == null) throw new InvalidOperationException($"Property '{name}' has no getter");
                var getter = new Func<T, object>(instance => instance == null ? null : getMethod.Invoke(instance, null));
                _delegateCache[key] = getter;
                return getter;
            }

            throw new MissingMemberException($"{typeof(T)}.{name}");
        }
    }

    public static Func<T, F> CreateMemberGetter<T, F>(string name) where T : class
    {
        var key = $"MemberGetTyped:{typeof(T).FullName}.{name}_{typeof(F).FullName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<T, F>)cached;

            var prop = AccessTools.Property(typeof(T), name);
            if (prop != null)
            {
                var getMethod = prop.GetGetMethod(true);
                if (getMethod != null)
                {
                    Func<T, F> getter;
                    try
                    {
                        getter = (Func<T, F>)Delegate.CreateDelegate(typeof(Func<T, F>), getMethod);
                    }
                    catch (ArgumentException)
                    {
                        // Private getter — skip-visibility dynamic-method wrapper
                        var method = new DynamicMethod($"get_{name}", typeof(F), new[] { typeof(T) }, true);
                        var il = method.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, getMethod);
                        il.Emit(OpCodes.Ret);
                        getter = (Func<T, F>)method.CreateDelegate(typeof(Func<T, F>));
                    }
                    _delegateCache[key] = getter;
                    return getter;
                }
            }

            var field = AccessTools.Field(typeof(T), name);
            if (field != null)
            {
                // Dynamic-method based FieldRef — no slow GetValue at runtime
                var refGet = AccessTools.FieldRefAccess<T, F>(field);
                var getter = new Func<T, F>(instance => instance == null ? default : refGet(instance));
                _delegateCache[key] = getter;
                return getter;
            }

            throw new MissingMemberException($"{typeof(T)}.{name}");
        }
    }

    public static Func<object> CreateStaticMemberGetter(Type declaringType, string name)
    {
        var key = $"StaticMemberGet:{declaringType.FullName}.{name}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<object>)cached;

            var field = AccessTools.Field(declaringType, name);
            if (field != null && field.IsStatic)
            {
                var getter = new Func<object>(() => field.GetValue(null));
                _delegateCache[key] = getter;
                return getter;
            }

            var prop = AccessTools.Property(declaringType, name);
            if (prop != null)
            {
                var getMethod = prop.GetGetMethod(true);
                if (getMethod != null && getMethod.IsStatic)
                {
                    var getter = new Func<object>(() => getMethod.Invoke(null, null));
                    _delegateCache[key] = getter;
                    return getter;
                }
            }

            throw new MissingMemberException($"{declaringType}.{name}");
        }
    }

    private class PatchRegistration
    {
        public Type PatchType { get; }
        public Func<bool> IsEnabled { get; }
        public Func<bool> LazyTrigger { get; }
        public bool IsLazy => LazyTrigger != null;

        public PatchRegistration(Type patchType, Func<bool> isEnabled, Func<bool> lazyTrigger = null)
        {
            PatchType = patchType;
            IsEnabled = isEnabled;
            LazyTrigger = lazyTrigger;
        }
    }
}
