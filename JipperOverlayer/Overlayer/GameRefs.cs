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
    // 全部用强类型委托：object 版每次取值都要装箱值类型、且字段走 field.GetValue()/
    // 属性走 getMethod.Invoke() 全反射。强类型版走 FieldRefAccess/CreateDelegate，
    // 而 IsPaused 每帧被 OverlayMono 读、CurrentSeqID 每次判定都读，是实打实的热路径。
    private static Func<scrController> _getControllerInstance;
    private static Func<scrController, bool> _getIsPaused;
    private static Func<scrController, int> _getCurrentSeqID;
    private static Func<scrController, scrFloor> _getCurrentFloor;
    private static Func<scrController, scrFloor> _getFirstFloor;
    private static Func<scrController, bool> _getIsNoFail;
    private static Func<scrController, float> _getPercentComplete;
    private static Func<int> _getCheckpointsUsed;

    // scrConductor
    private static Func<scrConductor> _getConductorInstance;
    private static Func<scrConductor, AudioSource> _getSong;
    private static Func<float> _getSongPitch;
    private static Func<scrConductor, bool> _getIsGameWorld;
    private static Func<scrConductor, double> _getAddoffset;
    private static Func<scrConductor, double> _getSongpositionMinusi;

    // RDC
    private static Func<bool> _getIsAuto;

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

        // 类型按游戏成员的实际声明写：scrController.paused=bool、currentSeqID=int、
        // currFloor/firstFloor=scrFloor、noFail=bool、percentComplete=float。
        // 写错只会让 TryMemberGetter 记一条 Warning 并退化成默认值（不崩溃），
        // 所以这些类型都已对着 Libs 与 .dsh-meow/r148 两个基线反编译核对过。
        _getIsPaused = TryMemberGetter<scrController, bool>("paused");
        _getCurrentSeqID = TryMemberGetter<scrController, int>("currentSeqID");
        _getCurrentFloor = TryMemberGetter<scrController, scrFloor>("currFloor");
        _getFirstFloor = TryMemberGetter<scrController, scrFloor>("firstFloor");
        _getIsNoFail = TryMemberGetter<scrController, bool>("noFail");
        _getPercentComplete = TryMemberGetter<scrController, float>("percentComplete");
        _getCheckpointsUsed = TryStaticFieldGetter<int>(typeof(scrController), "checkpointsUsed") ?? (() => 0);

        // scrConductor
        _getConductorInstance = TryStaticPropertyGetter<scrConductor>(typeof(scrConductor), "instance");

        // scrConductor.song=AudioSource、isGameWorld=bool、addoffset/songposition_minusi=double
        _getSong = TryMemberGetter<scrConductor, AudioSource>("song");
        _getIsGameWorld = TryMemberGetter<scrConductor, bool>("isGameWorld");
        _getAddoffset = TryMemberGetter<scrConductor, double>("addoffset");
        _getSongpositionMinusi = TryMemberGetter<scrConductor, double>("songposition_minusi");

        var pitchGetter = TryMemberGetter<AudioSource, float>("pitch");
        _getSongPitch = () =>
        {
            // Song 已是 AudioSource，无需再 is 判一次
            var song = Song;
            return song != null && pitchGetter != null ? pitchGetter(song) : 1f;
        };

        // RDC.auto 在不同版本可能是属性或字段：两条路都试，都是强类型委托。
        _getIsAuto = TryStaticPropertyGetter<bool>(typeof(RDC), "auto")
                  ?? TryStaticFieldGetter<bool>(typeof(RDC), "auto");

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

    /// <summary>优先强类型创建器：字段用 FieldRefAccess、属性用 CreateDelegate，
    /// 都不经过反射 GetValue/Invoke，也不装箱。
    ///
    /// 两道防线：
    /// ① **绑定期先核对成员声明类型**。FieldRefAccess 在类型不符时发出的是非法 IL，
    ///    创建不报错，要到**首次调用**才抛 InvalidProgramException——那已经在每帧
    ///    路径上了，比绑定期失败难查得多。类型不符就直接走反射回退。
    /// ② 强类型绑定抛异常时也回退。否则失败只会静默退化成 default(T)，
    ///    让 IsPaused 恒 false、CurrentSeqID 恒 0 这类隐蔽错误更难查。
    /// 两条路都走不通才返回 null（调用方回退各自的默认值）。</summary>
    private static Func<T, F> TryMemberGetter<T, F>(string name) where T : class
    {
        try
        {
            if (MemberTypeMatchesF<T, F>(name))
                return PatchManager.CreateMemberGetter<T, F>(name);
            Loader.Warning($"GameRefs: {typeof(T).Name}.{name} 声明类型不是 {typeof(F).Name}，退回反射取值");
        }
        catch (Exception e)
        {
            Loader.Warning($"GameRefs: 强类型成员 {typeof(T).Name}.{name} 绑定失败，退回反射 ({e.Message})");
        }

        try
        {
            var fallback = PatchManager.CreateMemberGetter<T>(name);
            if (fallback != null)
                return instance => fallback(instance) is F v ? v : default;
        }
        catch (Exception e2)
        {
            Loader.Warning($"GameRefs: 反射回退也失败 {typeof(T).Name}.{name} ({e2.Message})");
        }
        return null;
    }

    /// <summary>成员的声明类型是否正好是 F（属性看 PropertyType，字段看 FieldType）。</summary>
    private static bool MemberTypeMatchesF<T, F>(string name) where T : class
    {
        var fi = AccessTools.Field(typeof(T), name);
        if (fi != null) return fi.FieldType == typeof(F);
        var pi = AccessTools.Property(typeof(T), name);
        return pi != null && pi.PropertyType == typeof(F);
    }

    // ========== ADOBase ==========
    public static scrController Controller => _getController?.Invoke();
    public static scrConductor Conductor => _getConductor?.Invoke();
    public static scrLevelMaker LevelMaker => _getLevelMaker?.Invoke();
    public static bool IsScnGame => _getIsScnGame?.Invoke() ?? false;

    // ========== scrController ==========
    // 委托已是强类型 Func<T, F>，这里直接返回值即可——不再有装箱，也不需要
    // 原先那套 `is int v ? v : 0` 的拆箱模式匹配（default 的语义与旧写法一致）。
    public static scrController ControllerInstance => _getControllerInstance?.Invoke();
    public static bool IsPaused => _getIsPaused?.Invoke(Controller) ?? false;
    public static int CurrentSeqID => _getCurrentSeqID?.Invoke(ControllerInstance) ?? 0;
    public static scrFloor CurrentFloor => _getCurrentFloor?.Invoke(ControllerInstance);
    public static scrFloor FirstFloor => _getFirstFloor?.Invoke(ControllerInstance);
    public static bool IsNoFail => _getIsNoFail?.Invoke(ControllerInstance) ?? false;
    public static float PercentComplete => _getPercentComplete?.Invoke(ControllerInstance) ?? 0f;
    public static int CheckpointsUsed => _getCheckpointsUsed?.Invoke() ?? 0;

    // ========== scrConductor ==========
    public static scrConductor ConductorInstance => _getConductorInstance?.Invoke();
    /// <summary>与游戏声明一致地返回 AudioSource（此前是 object，每个调用方都要再 is 判一次）。</summary>
    public static AudioSource Song => _getSong?.Invoke(ConductorInstance);
    public static float SongPitch => _getSongPitch?.Invoke() ?? 1f;
    public static bool IsGameWorld => _getIsGameWorld?.Invoke(ConductorInstance) ?? false;
    public static double ConductorAddoffset => _getAddoffset?.Invoke(ConductorInstance) ?? 0.0;
    public static double ConductorSongpositionMinusi => _getSongpositionMinusi?.Invoke(ConductorInstance) ?? 0.0;

    // ========== RDC ==========
    public static bool IsAuto => _getIsAuto?.Invoke() ?? false;

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