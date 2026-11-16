using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// tag 注册表：反射扫描程序集内带 <see cref="TagAttribute"/> 的静态方法并按名字索引。
/// 求值参数全部来自模板文本（字符串），按目标参数类型转换（InvariantCulture）；
/// 未提供的可选参数取其默认值。未知 tag / 参数过多 / 转换失败 / 调用异常均返回 false，
/// 由模板引擎保留占位符原文，并按消息去重警告（避免每帧刷日志）。
/// </summary>
internal static class TagManager
{
    private sealed class TagEntry
    {
        public string Name;
        public MethodInfo Method;
        public ParameterInfo[] Parameters;
        public string[] ParamNames;
        public Type[] ParamTypes;
        public object[] Defaults;      // DBNull.Value = 必填
        public int RequiredCount;      // 无默认值的参数个数
        public TagState State;
        public Func<object[], object> Compiled; // 惰性编译的表达式委托
    }

    private static readonly Dictionary<string, TagEntry> Tags = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);

    // ===== 用户自定义组合 tag（设置面板「自定义 tag」分区）=====
    private static readonly Dictionary<string, string> CustomTags = new(StringComparer.Ordinal);
    private static int CustomEvalDepth;
    private static readonly Regex CustomTagNamePattern = new("^[A-Za-z0-9_]+$");
    private static readonly object[] EmptyArgs = Array.Empty<object>();
    private static bool _registered;

    /// <summary>已注册的 tag 名（供设置面板「复制 tag 列表」用，排序后返回）。</summary>
    public static List<string> GetTagNames()
    {
        EnsureRegistered();
        var names = new List<string>(Tags.Keys);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>取某个 tag 的游玩状态标记；未注册返回 false（视为不门控）。</summary>
    public static bool TryGetTagState(string name, out TagState state)
    {
        state = TagState.None;
        EnsureRegistered();
        if (string.IsNullOrEmpty(name) || !Tags.TryGetValue(name, out var entry)) return false;
        state = entry.State;
        return true;
    }

    /// <summary>非游玩显示模式开启时置真：解除门控，菜单里直接求实时值，
    /// 避免「从未求值过」的 tag 永远显示原文（{Combo} 闪原文即此）。</summary>
    internal static bool IgnoreGating;

    /// <summary>按标记判定当前是否应当冻结（沿用上一次好值）。</summary>
    public static bool IsBlockedState(TagState state)
        => !IgnoreGating
        && (((state & TagState.BlockOnNotPlaying) != 0 && !PlaybackState.IsPlaying)
        || ((state & TagState.BlockOnPaused) != 0 && PlaybackState.IsPaused));

    /// <summary>
    /// 当前玩家上下文：模板按玩家逐个求值时（如 coop 判定行）由引擎临时设置。
    /// tag 里名为 player、默认值为 0 的参数在「模板未写该参数」时取这个上下文，
    /// 模板里显式写了 {XPerfect(1)} 仍然优先——上下文只替代默认值，不覆盖显式参数。
    /// </summary>
    internal static int CurrentPlayer;

    private static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        try
        {
            // 本程序集引用游戏类型，个别类型在运行时可能解析失败（ReflectionTypeLoadException），
            // 直接 GetTypes() 会整批抛异常导致一个 tag 都注册不上——这里退回已成功加载的那部分。
            Type[] types;
            try { types = typeof(TagManager).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e)
            {
                var loaded = new List<Type>();
                foreach (var t in e.Types) if (t != null) loaded.Add(t);
                types = loaded.ToArray();
                WarnOnce("TagManager: 部分类型加载失败，已跳过 " + (e.Types.Length - types.Length) + " 个");
            }
            foreach (var type in types)
            {
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); }
                catch { continue; }
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<TagAttribute>(false);
                    if (attr == null) continue;
                    if (!method.IsStatic)
                    {
                        WarnOnce("TagManager: tag " + attr.Name + " 不是静态方法，已忽略");
                        continue;
                    }
                    if (Tags.ContainsKey(attr.Name))
                    {
                        WarnOnce("TagManager: tag " + attr.Name + " 重名，后者被忽略");
                        continue;
                    }
                    var ps = method.GetParameters();
                    var pnames = new string[ps.Length];
                    var ptypes = new Type[ps.Length];
                    var pdefaults = new object[ps.Length];
                    int required = 0;
                    for (int pi = 0; pi < ps.Length; pi++)
                    {
                        pnames[pi] = ps[pi].Name ?? "";
                        ptypes[pi] = ps[pi].ParameterType;
                        if (ps[pi].HasDefaultValue)
                        {
                            var dv = ps[pi].DefaultValue;
                            pdefaults[pi] = dv == DBNull.Value ? DBNull.Value : dv;
                        }
                        else
                        {
                            pdefaults[pi] = DBNull.Value;
                            required++;
                        }
                    }
                    Tags[attr.Name] = new TagEntry
                    {
                        Name = attr.Name,
                        Method = method,
                        Parameters = ps,
                        ParamNames = pnames,
                        ParamTypes = ptypes,
                        Defaults = pdefaults,
                        RequiredCount = required,
                        State = attr.State,
                    };
                }
            }
            Loader.Log("TagManager: 已注册 " + Tags.Count + " 个 tag");
        }
        catch (Exception e)
        {
            Loader.Warning("TagManager: tag 注册失败 (" + e.Message + ")");
        }
    }

    /// <summary>
    /// 注册/刷新用户自定义组合 tag（设置面板「自定义 tag」分区编辑，Settings 加载与 UI 变更时调用）。
    /// 名称仅限字母/数字/下划线；与内置 tag 重名的会被拒绝并告警。
    /// </summary>
    public static void SetCustomTags(IEnumerable<CustomTagDef> defs)
    {
        CustomTags.Clear();
        if (defs == null) return;
        EnsureRegistered();
        foreach (var d in defs)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.Name)) continue;
            var name = d.Name.Trim();
            if (!CustomTagNamePattern.IsMatch(name)) { WarnOnce("TagManager: 自定义 tag 名含非法字符，已跳过 " + name); continue; }
            if (Tags.ContainsKey(name)) { WarnOnce("TagManager: 自定义 tag 与内置重名，已跳过 " + name); continue; }
            CustomTags[name] = d.Template ?? "";
        }
    }

    /// <summary>按名字调用 tag。args 为模板里写的字符串参数（可为 null/空数组）。
    /// 调用语义与参考引擎对齐：无括号调用且存在必填参数 → 原样保留（用户立刻看出缺参数）；
    /// 多余实参忽略；缺失/空位 → 声明默认值 → 类型默认；实参经 CoerceArg 归一化后由
    /// 编译委托调用（与 TagCore.Invoke 同构，无每帧反射 Invoke）。</summary>
    public static bool TryInvoke(string name, string[] args, out object result)
    {
        result = null;
        EnsureRegistered();
        if (string.IsNullOrEmpty(name)) return false;
        if (!Tags.TryGetValue(name, out var entry))
        {
            // 用户自定义组合 tag：展开其模板片段（深度限制防环引用）。
            // 展开失败/空模板不告警——原样保留由模板层处理，避免菜单里每帧刷日志。
            if (CustomTags.TryGetValue(name, out var ctpl))
            {
                if (CustomEvalDepth >= 4) return false;
                CustomEvalDepth++;
                try
                {
                    result = TemplateEngine.EvaluateKeepContext(ctpl);
                    return result != null;
                }
                finally { CustomEvalDepth--; }
            }
            WarnOnce("TemplateEngine: 未知 tag {" + name + "}");
            return false;
        }

        // 必填参数不足（完全无实参，或只给了部分实参）→ 原样保留（参考引擎同语义，
        // 让用户立刻看出少参数，而不是静默填类型默认值变成 <color=#> 空壳）。
        // player 参数视为「总是有值」（缺省走玩家上下文），不参与必填计数。
        int supplied = 0;
        if (args != null)
        {
            for (int i = 0; i < args.Length && i < entry.ParamNames.Length; i++)
            {
                if (args[i].Length > 0 || entry.ParamNames[i] == "player") supplied++;
            }
        }
        if (supplied < entry.RequiredCount)
        {
            WarnOnce("TemplateEngine: tag {" + name + "} 缺少必填参数（" + supplied + "/" + entry.RequiredCount + "）");
            return false;
        }

        try
        {
            var normalized = NormalizeArgs(entry, args);
            if (entry.Compiled == null) entry.Compiled = CompileEntry(entry);
            result = entry.Compiled(normalized);
            return true;
        }
        catch (Exception e)
        {
            var msg = e.InnerException != null ? e.InnerException.Message : e.Message;
            WarnOnce("TemplateEngine: tag {" + name + "} 求值失败 (" + msg + ")");
            return false;
        }
    }

    /// <summary>实参归一化（参考引擎 TagCore.NormalizeArgs 移植）：
    /// 非空实参 Coerce 到形参类型；缺失/空位 → 声明默认值 → 类型默认；
    /// player 参数缺失或空位 → 玩家上下文。多余实参忽略。</summary>
    private static object[] NormalizeArgs(TagEntry e, string[] args)
    {
        args ??= Array.Empty<string>();
        var normalized = new object[e.Parameters.Length];
        for (int i = 0; i < e.Parameters.Length; i++)
        {
            bool omitted = i >= args.Length;
            bool empty = !omitted && args[i].Length == 0;
            if ((omitted || empty) && e.ParamNames[i] == "player")
            {
                normalized[i] = CurrentPlayer;
            }
            else if (!omitted && !empty)
            {
                normalized[i] = CoerceArg(args[i], e.ParamTypes[i]);
            }
            else if (e.Defaults[i] != DBNull.Value)
            {
                normalized[i] = CoerceArg(e.Defaults[i], e.ParamTypes[i]);
            }
            else
            {
                normalized[i] = e.ParamTypes[i].IsValueType ? Activator.CreateInstance(e.ParamTypes[i]) : null;
            }
        }
        return normalized;
    }

    /// <summary>实参/默认值 → 形参类型（参考引擎 TagCore.CoerceArg 移植）。</summary>
    private static object CoerceArg(object value, Type target)
    {
        if (value == null)
        {
            return target.IsValueType && Nullable.GetUnderlyingType(target) == null
                ? Activator.CreateInstance(target)
                : null;
        }
        if (target.IsInstanceOfType(value))
        {
            return value;
        }
        if (target == typeof(string))
        {
            return value.ToString();
        }
        if (value is string text)
        {
            return ConvertText(text, target);
        }
        if (value is IConvertible && (target.IsPrimitive || target == typeof(decimal)))
        {
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        throw new InvalidCastException("无法把 " + value.GetType().FullName + " 转为 " + target.FullName);
    }

    private static object ConvertText(string text, Type target)
    {
        if (target == typeof(string)) return text;
        if (text.Length == 0) return DefaultOf(target);
        if (target == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (target == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (target == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
        if (target == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        if (target == typeof(bool) && bool.TryParse(text, out var bl)) return bl;
        if (target.IsEnum) return Enum.Parse(target, text, true);
        return Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
    }

    private static object DefaultOf(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>每个 tag 编译一次表达式委托（参考引擎 TagCore.Invoke 同构）：
    /// 归一化后的实参已是形参精确类型，委托只做拆箱与调用，无每帧反射 Invoke。</summary>
    private static Func<object[], object> CompileEntry(TagEntry e)
    {
        var argsParam = Expression.Parameter(typeof(object[]), "args");
        var paramExprs = new Expression[e.Parameters.Length];
        for (int i = 0; i < e.Parameters.Length; i++)
        {
            var accessor = Expression.ArrayIndex(argsParam, Expression.Constant(i));
            paramExprs[i] = Expression.Convert(accessor, e.Parameters[i].ParameterType);
        }
        var call = Expression.Call(null, e.Method, paramExprs);
        return Expression.Lambda<Func<object[], object>>(Expression.Convert(call, typeof(object)), argsParam).Compile();
    }

    /// <summary>模板参数字符串 → 目标类型（数值用 InvariantCulture）。</summary>
    private static bool TryConvert(string raw, Type target, out object converted)
    {
        converted = null;
        if (raw == null) return false;
        if (target == typeof(string)) { converted = raw; return true; }
        if (raw.Length == 0) return false;
        try
        {
            if (target == typeof(int))
            {
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return false;
                converted = v;
                return true;
            }
            if (target == typeof(long))
            {
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return false;
                converted = l;
                return true;
            }
            if (target == typeof(float))
            {
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return false;
                converted = f;
                return true;
            }
            if (target == typeof(double))
            {
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
                converted = d;
                return true;
            }
            if (target == typeof(bool))
            {
                if (!bool.TryParse(raw, out var b)) return false;
                converted = b;
                return true;
            }
            if (target.IsEnum)
            {
                // net481 只有泛型 Enum.TryParse<TEnum>，非泛型 (Type,string,bool,out object) 是
                // .NET Core 2.0 才加的，这里只能用 Parse + try/catch 判定。
                object e;
                try { e = Enum.Parse(target, raw, true); }
                catch { return false; }
                if (!Enum.IsDefined(target, e)) return false;
                converted = e;
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private static void WarnOnce(string message)
    {
        if (Warned.Add(message)) Loader.Warning(message);
    }
}
