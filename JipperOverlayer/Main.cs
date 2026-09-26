using HarmonyLib;
using JipperOverlayer.Overlayer;
using JipperOverlayer.Overlayer.Features;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace JipperOverlayer;

public static class Main
{
    public static Harmony Harmony { get; private set; }
    public static Settings Settings { get; private set; }
    public static bool IsEnabled => _enabled;

    private static Overlay _overlay;
    private static GameObject _overlayGo;
    private static bool _enabled;
    private static DateTime _enableRetryAfter = DateTime.MinValue;

    public static void Init(IModLoader loader)
    {
        Loader.Instance = loader;
        Settings = Settings.Load();

        loader.OnToggle += OnToggle;
        loader.OnGUI += () =>
        {
            if (Settings != null) Settings.OnGUI();
        };
        loader.OnSaveGUI += OnSaveGUI;
        loader.OnUpdate += OnUpdate;

        Harmony = new Harmony("JipperOverlayer");

        Log("JipperOverlayer initialized.");
    }

    private static void OnToggle(bool value)
    {
        if (value)
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }

    public static void Enable()
    {
        if (_enabled || DateTime.UtcNow < _enableRetryAfter) return;
        _enabled = true;

        try
        {
            Log("JipperOverlayer enabled.");

            PatchManager.Initialize(Harmony);
            GameRefs.BindDelegates();
            VersionSafe.Setup();
            RegisterFeatures();

            AssetLoader.Load();
            FontManager.ScanFonts();
            PlayCount.Load();

            if (_overlayGo == null)
            {
                _overlayGo = new GameObject("JipperOverlayer");
                UnityEngine.Object.DontDestroyOnLoad(_overlayGo);
            }

            CreateOverlay();
            PatchManager.ApplyAll();
            // 先挂补丁再首次 Show，确保局内启用时首个精度/判定/进度事件不会因
            // activeSelf 尚未切换或补丁尚未注册而丢失。StartTile 必须是当前 seqID，
            // 不能把局内重新启用误当成从第 0 格开始的新尝试。
            if (GameRefs.IsGameReady && GameRefs.LevelMaker != null)
            {
                if (GameRefs.IsPaused)
                {
                    _overlay.Show(GameRefs.CurrentSeqID, suppressNativeUI: true);
                    if (_overlay.Canvas) _overlay.Canvas.enabled = false;
                }
                else
                    _overlay.Show(GameRefs.CurrentSeqID);
            }

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _enableRetryAfter = DateTime.MinValue;
        }
        catch (Exception e)
        {
            // 资源/补丁初始化失败时给宿主一个短退避，避免 MelonLoader 每帧重试
            // 完整扫描字体并刷屏日志；下一轮 OnUpdate 会自动再试。
            _enableRetryAfter = DateTime.UtcNow.AddSeconds(5);
            Error($"Enable failed; rolling back: {e}");
            try { Disable(); } catch (Exception rollback) { Error($"Rollback failed: {rollback}"); }
        }
    }

    public static void Disable()
    {
        if (!_enabled) return;
        _enabled = false;

        Log("JipperOverlayer disabled.");
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // 禁用发生在场景回调/资源异常时也要尽力完成所有清理；某一步失败不能
        // 阻止后续 unpatch，否则 Harmony 补丁会残留在游戏进程中。
        try { _overlay?.Destroy(); } catch (Exception e) { Error($"Overlay destroy failed: {e.Message}"); }
        _overlay = null;
        Overlay.Instance = null;

        try
        {
            if (_overlayGo != null)
            {
                UnityEngine.Object.Destroy(_overlayGo);
                _overlayGo = null;
            }
        }
        catch (Exception e) { Error($"Overlay GameObject destroy failed: {e.Message}"); }

        try { PlayCount.Dispose(); } catch (Exception e) { Error($"Play data cleanup failed: {e.Message}"); }
        try { XPerfectIntegration.ResetForModDisable(); } catch (Exception e) { Error($"XPerfect cleanup failed: {e.Message}"); }
        try { AssetLoader.Unload(); } catch (Exception e) { Error($"Asset cleanup failed: {e.Message}"); }
        try { PatchManager.UnpatchAll(); } catch (Exception e) { Error($"Patch cleanup failed: {e.Message}"); }
    }

    private static void RegisterFeatures()
    {
        GameLifecyclePatches.Register();

        if (VersionSafe.IsV141OrLater)
        {
            Log("API: v141+ — registering v141 patches");
            V141Patches.RegisterAll();
        }
        else
        {
            Log("API: v136  — registering v136 patches");
            V136Patches.RegisterAll();
        }
    }

    private static void CreateOverlay()
    {
        _overlay = new Overlay();
    }

    public static void RecreateOverlay()
    {
        _overlay?.Destroy();
        _overlay = null;
        Overlay.Instance = null;
        CreateOverlay();
        if (!GameRefs.IsGameReady)
            return;
        if (_overlay == null || _overlay.GameObject.activeSelf) return;

        if (GameRefs.IsPaused)
        {
            _overlay.Show(GameRefs.CurrentSeqID, suppressNativeUI: true);
            if (_overlay.Canvas)
                _overlay.Canvas.enabled = false;
        }
        else
        {
            _overlay.Show(GameRefs.CurrentSeqID);
        }
    }

    private static void OnSceneUnloaded(Scene _)
    {
        try { _overlay?.Hide(); } catch { }
    }

    private static void OnSaveGUI()
    {
        Settings.OnSaveGUI();
    }

    private static void OnUpdate(float deltaTime)
    {
        XPerfectIntegration.EnsureInitialized();
        if (Settings.ShowFPS)
            try { _overlay?.ExtendedOverlay?.UpdateFPS(deltaTime); }
            catch { }
    }

    // Convenience wrappers
    public static void Log(string msg) => Loader.Log(msg);
    public static void Warning(string msg) => Loader.Warning(msg);
    public static void Error(string msg) => Loader.Error(msg);
}