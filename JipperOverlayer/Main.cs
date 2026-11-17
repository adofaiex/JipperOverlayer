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
    private static float _lastOnGuiErrAt = -999f;

    public static void Init(IModLoader loader)
    {
        Loader.Instance = loader;
        Settings = Settings.Load();

        loader.OnToggle += OnToggle;
        loader.OnGUI += () =>
        {
            if (Settings == null) return;
            try { Settings.OnGUI(); }
            catch (Exception e)
            {
                // UMM 捕获到 mod OnGUI 的异常后可能不再调用本回调（表现为设置窗整块灰板）。
                // 自己兜住：带完整堆栈限频记录，便于定位；后续帧照常继续绘制。
                float now = Time.realtimeSinceStartup;
                if (now - _lastOnGuiErrAt < 30f) return;
                _lastOnGuiErrAt = now;
                Error("[Exception] OnGUI: " + e);
            }
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

            // 启动即跑一次 tag 注册扫描：不装模板也能在日志里确认 tag 引擎可用（数量 / 反射失败）。
            try { Overlayer.Tags.TagManager.GetTagNames(); }
            catch (Exception e) { Loader.Warning("TagManager: 预注册失败 " + e.Message); }
            // 三语文案对齐自检（长度 + 哨兵键），错位只会在日志留 Warning。
            Overlayer.Localization.Tr.SelfAudit();
            // 注册用户自定义组合 tag（必须在内置 tag 就绪之后——重名检测依赖内置表）。
            try { Overlayer.Tags.TagManager.SetCustomTags(Settings.CustomTags); }
            catch (Exception e) { Loader.Warning("TagManager: 自定义 tag 注册失败 " + e.Message); }

            if (_overlayGo == null)
            {
                _overlayGo = new GameObject("JipperOverlayer");
                // 布局编辑器挂在常驻对象上而非覆盖层：覆盖层在未进关卡/暂停时
                // 整个 GameObject 会被停用，挂上去 OnGUI 就不再执行，编辑器会一起消失。
                _overlayGo.AddComponent<OverlayDragEditor>();
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
                // 标记为主动首显：随后游戏若对同一格补发 scnGame.Play，只去重这一次，
                // 不会把新一局（死亡/手动重试）的 Show 吞掉。
                _overlay.MarkShownByEnable();
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
        // 隐藏标题开关随模组禁用复位，避免残留 GCS.d_dontShowTitles=true。
        try { GCS.d_dontShowTitles = false; } catch { }

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
        _overlay.MarkShownByEnable();
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
        // 帧时间戳环形缓冲：{Fps(窗口)}/{FrameTime(窗口)} 的窗口平均与节流数据源。
        try { Overlayer.Tags.FpsTracker.Tick(); } catch { }
        // 隐藏关卡标题开关 → 游戏闲置字段 GCS.d_dontShowTitles（scrHUDText 每帧读，
        // 见 scrHUDText.Update）。每帧直写保证开关/启停状态无漂移；禁用时在 Disable 复位。
        try { GCS.d_dontShowTitles = Settings != null && Settings.HideTitle; } catch { }
        // 非游玩显示模式驱动：菜单/结算里点亮画布只渲染自定义行（不依赖 _mono 的 Update）。
        try { _overlay?.UpdateOutsidePlayMode(); } catch { }
        if (Settings.ShowFPS)
            try { _overlay?.ExtendedOverlay?.UpdateFPS(deltaTime); }
            catch { }
    }

    // Convenience wrappers
    public static void Log(string msg) => Loader.Log(msg);
    public static void Warning(string msg) => Loader.Warning(msg);
    public static void Error(string msg) => Loader.Error(msg);
}