using System;
using HarmonyLib;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public static class GameRefs
{
    private static bool _delegatesBound;

    // ADOBase
    private static Func<scrController> _getController;
    private static Func<scrConductor> _getConductor;
    private static Func<scrLevelMaker> _getLevelMaker;
    private static Func<bool> _getIsScnGame;

    // scrController
    private static Func<scrController> _getControllerInstance;
    private static Func<scrController, object> _getIsPaused;
    private static Func<scrController, object> _getCurrentSeqID;
    private static Func<scrController, object> _getCurrentFloor;
    private static Func<scrController, object> _getFirstFloor;
    private static Func<scrController, object> _getIsNoFail;
    private static Func<scrController, object> _getPercentComplete;
    private static Func<int> _getCheckpointsUsed;

    // scrConductor
    private static Func<scrConductor> _getConductorInstance;
    private static Func<scrConductor, object> _getSong;
    private static Func<float> _getSongPitch;
    private static Func<scrConductor, object> _getIsGameWorld;
    private static Func<scrConductor, object> _getAddoffset;
    private static Func<scrConductor, object> _getSongpositionMinusi;

    // RDC
    private static Func<object> _getIsAuto;

    // scnGame / scnEditor
    private static Func<scnGame> _getGameInstance;
    private static Func<scnEditor> _getEditorInstance;

    internal static void BindDelegates()
    {
        if (_delegatesBound) return;

        // ADOBase
        _getController = TryStaticPropertyGetter<scrController>(typeof(ADOBase), "controller");
        _getConductor = TryStaticPropertyGetter<scrConductor>(typeof(ADOBase), "conductor");
        _getLevelMaker = TryStaticPropertyGetter<scrLevelMaker>(typeof(ADOBase), "lm");
        _getIsScnGame = TryStaticPropertyGetter<bool>(typeof(ADOBase), "isScnGame");

        _getControllerInstance = TryStaticPropertyGetter<scrController>(typeof(scrController), "instance")
                              ?? TryStaticFieldGetter<scrController>(typeof(scrController), "_instance");

        _getIsPaused = TryMemberGetter<scrController>("paused");
        _getCurrentSeqID = TryMemberGetter<scrController>("currentSeqID");
        _getCurrentFloor = TryMemberGetter<scrController>("currFloor");
        _getFirstFloor = TryMemberGetter<scrController>("firstFloor");
        _getIsNoFail = TryMemberGetter<scrController>("noFail");
        _getPercentComplete = TryMemberGetter<scrController>("percentComplete");
        _getCheckpointsUsed = TryStaticFieldGetter<int>(typeof(scrController), "checkpointsUsed") ?? (() => 0);

        // scrConductor
        _getConductorInstance = TryStaticPropertyGetter<scrConductor>(typeof(scrConductor), "instance");

        _getSong = TryMemberGetter<scrConductor>("song");
        _getIsGameWorld = TryMemberGetter<scrConductor>("isGameWorld");
        _getAddoffset = TryMemberGetter<scrConductor>("addoffset");
        _getSongpositionMinusi = TryMemberGetter<scrConductor>("songposition_minusi");

        var pitchGetter = TryMemberGetter<AudioSource, float>("pitch");
        _getSongPitch = () =>
        {
            var song = Song;
            return song is AudioSource audioSrc && pitchGetter != null ? pitchGetter(audioSrc) : 1f;
        };

        // RDC
        _getIsAuto = TryStaticMemberGetter(typeof(RDC), "auto");

        // scnGame / scnEditor
        _getGameInstance   = TryStaticFieldGetter<scnGame>(typeof(scnGame), "instance");
        _getEditorInstance = TryStaticFieldGetter<scnEditor>(typeof(scnEditor), "instance");
        // 与 VersionSafe 一样，只有全部绑定完成才置成功标志；异常回滚后
        // 下一次 Enable 会重新绑定，而不是沿用半初始化的静态状态。
        _delegatesBound = true;
    }

    private static Func<TField> TryStaticPropertyGetter<TField>(Type type, string name)
    {
        try { return PatchManager.CreateStaticPropertyGetter<TField>(type, name); }
        catch (Exception e) { Loader.Warning($"GameRefs: 静态属性 {type.Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static Func<TField> TryStaticFieldGetter<TField>(Type type, string name)
    {
        try { return PatchManager.CreateStaticFieldGetter<TField>(type, name); }
        catch (Exception e) { Loader.Warning($"GameRefs: 字段 {type.Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static Func<T, object> TryMemberGetter<T>(string name) where T : class
    {
        try { return PatchManager.CreateMemberGetter<T>(name); }
        catch (Exception e) { Loader.Warning($"GameRefs: 字段或属性 {typeof(T).Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static Func<T, F> TryMemberGetter<T, F>(string name) where T : class
    {
        try { return PatchManager.CreateMemberGetter<T, F>(name); }
        catch (Exception e) { Loader.Warning($"GameRefs: 字段或属性 {typeof(T).Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static Func<object> TryStaticMemberGetter(Type type, string name)
    {
        try { return PatchManager.CreateStaticMemberGetter(type, name); }
        catch (Exception e) { Loader.Warning($"GameRefs: 字段或属性 {type.Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    // ========== ADOBase ==========
    public static scrController Controller => _getController?.Invoke();
    public static scrConductor Conductor => _getConductor?.Invoke();
    public static scrLevelMaker LevelMaker => _getLevelMaker?.Invoke();
    public static bool IsScnGame => _getIsScnGame?.Invoke() ?? false;

    // ========== scrController ==========
    public static scrController ControllerInstance => _getControllerInstance?.Invoke();
    public static bool IsPaused => _getIsPaused?.Invoke(Controller) is true;
    public static int CurrentSeqID => _getCurrentSeqID?.Invoke(ControllerInstance) is int v ? v : 0;
    public static scrFloor CurrentFloor => _getCurrentFloor?.Invoke(ControllerInstance) as scrFloor;
    public static scrFloor FirstFloor => _getFirstFloor?.Invoke(ControllerInstance) as scrFloor;
    public static bool IsNoFail => _getIsNoFail?.Invoke(ControllerInstance) is true;
    public static float PercentComplete => _getPercentComplete?.Invoke(ControllerInstance) is float v ? v : 0f;
    public static int CheckpointsUsed => _getCheckpointsUsed?.Invoke() ?? 0;

    // ========== scrConductor ==========
    public static scrConductor ConductorInstance => _getConductorInstance?.Invoke();
    public static object Song => _getSong?.Invoke(ConductorInstance);
    public static float SongPitch => _getSongPitch?.Invoke() ?? 1f;
    public static bool IsGameWorld => _getIsGameWorld?.Invoke(ConductorInstance) is true;
    public static double ConductorAddoffset => _getAddoffset?.Invoke(ConductorInstance) is double v ? v : 0.0;
    public static double ConductorSongpositionMinusi => _getSongpositionMinusi?.Invoke(ConductorInstance) is double v ? v : 0.0;

    // ========== RDC ==========
    public static bool IsAuto => _getIsAuto?.Invoke() is true;

    // ========== scnGame ==========
    public static scnGame GameInstance => _getGameInstance?.Invoke();

    // ========== scnEditor ==========
    public static scnEditor EditorInstance => _getEditorInstance?.Invoke();

    // ========== Composite checks ==========
    public static bool IsGameReady =>
        Controller != null &&
        Conductor != null &&
        IsGameWorld &&
        ControllerInstance != null;
}