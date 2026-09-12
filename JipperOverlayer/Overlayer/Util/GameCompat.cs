using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Util;

/// <summary>
/// 判定枚举的跨版本兼容层。
///
/// 编译基线固定为 r148（Libs/Assembly-CSharp.dll），因此代码里不能出现 r150 才有的符号；
/// 所有判定下标在运行时按“名字”解析，使同一个构建产物既能跑 r148 也能跑 r150。
///
/// r148: HitMargin 共 12 值，中心判定是唯一的 Perfect(3)
/// r150: HitMargin 共 16 值，中心判定拆分为 PerfectMinus(3) / XPerfect(4) / PerfectPlus(5)
///
/// 直接在代码里写 HitMargin.Perfect 在 r150 上不会报错（值 3 仍然合法），
/// 但语义会静默错位成 PerfectMinus，因此必须全部走本类的语义谓词。
/// </summary>
internal static class HitMarginCompat
{
    // ===== 语义下标；-1 表示该版本不存在此判定 =====
    public static readonly int TooEarly;
    public static readonly int VeryEarly;
    public static readonly int EarlyPerfect;
    public static readonly int PerfectMinus;
    public static readonly int XPerfect;
    public static readonly int PerfectPlus;
    public static readonly int LatePerfect;
    public static readonly int VeryLate;
    public static readonly int TooLate;
    public static readonly int Multipress;
    public static readonly int FailMiss;
    public static readonly int FailOverload;
    public static readonly int Auto;
    public static readonly int OverPress;
    public static readonly int Midspin;
    public static readonly int FailedFloor;

    /// <summary>r148 的中心判定 Perfect(3)；r150 无 Perfect，回退为 PerfectMinus(3)。</summary>
    public static readonly int Perfect;

    /// <summary>r150 起基础游戏原生支持 XPerfect 判定（无需外部 XPerfect mod）。</summary>
    public static readonly bool HasNativeXPerfect;

    /// <summary>HitMargin 取值个数（r148=12，r150=16），用于分配 hitMarginsCount 数组。</summary>
    public static readonly int Count;

    /// <summary>
    /// 游戏实际 release 号（Releases.releaseNumber；读取失败为 -1）。
    /// 它是 const：直接引用会在编译期烧成本 mod 基线(r148)的值，必须反射读运行时程序集。
    /// </summary>
    public static readonly int ReleaseNumber;

    /// <summary>启动日志用的版本摘要。</summary>
    public static string VersionReport =>
        $"game r{(ReleaseNumber >= 0 ? ReleaseNumber.ToString() : "?")}, HitMargin={Count} 值, " +
        (HasNativeXPerfect ? "原生 XPerfect" : "无原生 XPerfect");

    static HitMarginCompat()
    {
        TooEarly = Resolve("TooEarly");
        VeryEarly = Resolve("VeryEarly");
        EarlyPerfect = Resolve("EarlyPerfect");
        PerfectMinus = Resolve("PerfectMinus");
        XPerfect = Resolve("XPerfect");
        PerfectPlus = Resolve("PerfectPlus");
        LatePerfect = Resolve("LatePerfect");
        VeryLate = Resolve("VeryLate");
        TooLate = Resolve("TooLate");
        Multipress = Resolve("Multipress");
        FailMiss = Resolve("FailMiss");
        FailOverload = Resolve("FailOverload");
        Auto = Resolve("Auto");
        OverPress = Resolve("OverPress");
        Midspin = Resolve("Midspin");
        FailedFloor = Resolve("FailedFloor");

        HasNativeXPerfect = XPerfect >= 0;
        Perfect = HasNativeXPerfect ? PerfectMinus : Resolve("Perfect");

        try
        {
            ReleaseNumber = (int)(typeof(Releases)
                .GetField("releaseNumber", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) ?? (object)(-1));
        }
        catch { ReleaseNumber = -1; }

        int count = 0;
        try { count = Enum.GetValues(typeof(HitMargin)).Length; } catch { }
        Count = count > 0 ? count : (HasNativeXPerfect ? 16 : 12);
    }

    /// <summary>
    /// 按名字取枚举值；该版本没有这个判定时返回 -1。
    /// 用 Enum.Parse + 捕获 ArgumentException 判定存在性：未定义名字必然抛异常，
    /// 这一行为在 net481 与所有运行时上一致，不依赖 Enum.IsDefined(Type, string) 的实现细节。
    /// </summary>
    private static int Resolve(string name)
    {
        try { return (int)Enum.Parse(typeof(HitMargin), name); }
        catch { return -1; }
    }

    /// <summary>越界安全取值：数组缺失或下标无效时返回 0。</summary>
    public static int Get(int[] counts, int index)
        => counts != null && index >= 0 && index < counts.Length ? counts[index] : 0;

    /// <summary>
    /// 中心“完美”判定（绿色数字）：
    /// r148 = { Perfect }；r150 = { PerfectMinus, XPerfect, PerfectPlus }。
    /// 不含 EarlyPerfect / LatePerfect（黄绿）与 Auto。
    /// </summary>
    public static bool IsPerfectCore(int hit)
        => HasNativeXPerfect
            ? hit == PerfectMinus || hit == XPerfect || hit == PerfectPlus
            : hit == Perfect;

    /// <summary>黄绿及以上（中心完美 + EarlyPerfect / LatePerfect）——Jongyeol 连击用。</summary>
    public static bool IsPerfectExtended(int hit)
        => IsPerfectCore(hit) || hit == EarlyPerfect || hit == LatePerfect;

    /// <summary>
    /// 纯完美（Pure Perfect）允许存在的判定：这些判定非零不会破坏 Pure Perfect。
    /// r148 = { Perfect, Auto, Multipress }；r150 = { PerfectMinus, XPerfect, PerfectPlus, Auto, Midspin, Multipress }。
    /// 其余任何判定只要非零即视为已破。
    /// </summary>
    public static bool IsPurePerfectAllowed(int index)
    {
        if (index < 0) return true;
        if (index == Auto || index == Multipress || index == OverPress) return true;
        if (HasNativeXPerfect)
            return index == PerfectMinus || index == XPerfect || index == PerfectPlus || index == Midspin;
        return index == Perfect;
    }

    /// <summary>
    /// 中心“完美”判定计数之和（判定面板的绿色数字）。
    /// r148 = Perfect；r150 = PerfectMinus + XPerfect + PerfectPlus。
    /// </summary>
    public static int SumCorePerfect(int[] counts)
    {
        if (HasNativeXPerfect)
            return Get(counts, PerfectMinus) + Get(counts, XPerfect) + Get(counts, PerfectPlus);
        return Get(counts, Perfect);
    }

    /// <summary>
    /// 取“中心完美”的 +/X/- 三分量。
    /// r150 由基础游戏原生统计；r148 取自外部 XPerfect mod（未安装时全为 0）。
    /// </summary>
    public static void GetPerfectBreakdown(int[] counts, int player, out int plus, out int x, out int minus)
    {
        if (HasNativeXPerfect)
        {
            plus = Get(counts, PerfectPlus);
            x = Get(counts, XPerfect);
            minus = Get(counts, PerfectMinus);
            return;
        }
        plus = XPerfectIntegration.GetPlayerPlusPerfect(player);
        x = XPerfectIntegration.GetPlayerXPerfect(player);
        minus = XPerfectIntegration.GetPlayerMinusPerfect(player);
    }

    /// <summary>整数组纯完美判定：只允许中心完美组 + Auto/Multipress/OverPress/Midspin 非零。</summary>
    public static bool IsPurePerfect(int[] counts)
    {
        if (counts == null) return true;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] == 0) continue;
            if (IsPurePerfectAllowed(i)) continue;
            return false;
        }
        return true;
    }

    /// <summary>失败判定（死亡数）：FailMiss + FailOverload。</summary>
    public static bool IsFail(int hit) => hit == FailMiss || hit == FailOverload;

    /// <summary>
    /// 是否具备可显示的 XPerfect 数据：
    /// r150 由基础游戏原生提供；r148 依赖外部 XPerfect mod。
    /// </summary>
    public static bool XPerfectDisplayAvailable
        => HasNativeXPerfect || XPerfectIntegration.IsAvailable;
}

/// <summary>
/// 判定时间窗相关的跨版本兼容层。
///
/// r148: static double scrMisc.GetAdjustedAngleBoundaryInDeg(HitMarginGeneral, double, double, double)
/// r150: static scrMisc/HitMarginGeneralWithXPerfectValuesStruct&lt;double&gt;
///           scrMisc.GetAdjustedAngleBoundaryInDeg(Difficulty, double, double, double)
///
/// 首参类型与返回类型同时变化，直接调用在另一个版本上会抛 MissingMethodException，
/// 被 try/catch 吞掉后表现为“判定时间窗静默消失”。因此这里全部走运行时探测。
/// </summary>
internal static class GameCompat
{
    private static bool _probed;

    // r148 路径：返回单个 double 角度边界
    private static Func<HitMarginGeneral, double, double, double, double> _boundaryV148;

    // r150 路径：返回结构体，需反射调用 + 读字段
    private static MethodInfo _boundaryV150;
    private static FieldInfo _fCounted, _fPerfect, _fPure, _fXPerfect;
    private static bool _invokeWarned;

    public static bool HasNativeXPerfect => HitMarginCompat.HasNativeXPerfect;

    private static void Probe()
    {
        if (_probed) return;
        _probed = true;

        if (HitMarginCompat.HasNativeXPerfect)
        {
            _boundaryV150 = FindMethod(typeof(Difficulty));
            if (_boundaryV150 != null)
            {
                var rt = _boundaryV150.ReturnType;
                _fCounted = rt.GetField("Counted");
                _fPerfect = rt.GetField("Perfect");
                _fPure = rt.GetField("Pure");
                _fXPerfect = rt.GetField("XPerfect");
                if (_fCounted == null || _fPerfect == null || _fPure == null)
                {
                    Loader.Warning("GameCompat: GetAdjustedAngleBoundaryInDeg 返回结构字段缺失，判定时间窗不可用");
                    _boundaryV150 = null;
                }
                else
                {
                    Loader.Log("GameCompat: 判定边界走 r149+ 路径（Difficulty 重载，原生 XPerfect 边界）");
                }
            }
            if (_boundaryV150 == null)
                Loader.Warning("GameCompat: 未找到 r150 版 GetAdjustedAngleBoundaryInDeg");
        }
        else
        {
            var m = FindMethod(typeof(HitMarginGeneral));
            if (m != null)
            {
                try
                {
                    _boundaryV148 = (Func<HitMarginGeneral, double, double, double, double>)Delegate.CreateDelegate(
                        typeof(Func<HitMarginGeneral, double, double, double, double>), m);
                }
                catch (Exception e)
                {
                    Loader.Warning($"GameCompat: GetAdjustedAngleBoundaryInDeg 绑定失败 ({e.Message})");
                }
                if (_boundaryV148 != null)
                    Loader.Log("GameCompat: 判定边界走 r141-r148 路径（HitMarginGeneral 重载）");
            }
            if (_boundaryV148 == null)
                Loader.Warning("GameCompat: 未找到 r148 版 GetAdjustedAngleBoundaryInDeg");
        }
    }

    private static MethodInfo FindMethod(Type firstParam)
    {
        try
        {
            return AccessTools.Method(typeof(scrMisc), "GetAdjustedAngleBoundaryInDeg",
                new[] { firstParam, typeof(double), typeof(double), typeof(double) });
        }
        catch (Exception e)
        {
            Loader.Warning($"GameCompat: GetAdjustedAngleBoundaryInDeg 探测失败 ({e.Message})");
            return null;
        }
    }

    /// <summary>
    /// 取判定角度边界（度）。
    /// counted/perfect/pure 为 Counted/Perfect/Pure 三条边界；xPerfect 仅 r150 原生提供（r148 返回 0）。
    /// </summary>
    public static bool TryGetAngleBoundaries(
        double bpmTimesSpeed, double conductorPitch, double marginScale,
        out double counted, out double perfect, out double pure, out double xPerfect)
    {
        counted = perfect = pure = xPerfect = 0.0;
        Probe();

        try
        {
            if (_boundaryV150 != null)
            {
                object boxed = _boundaryV150.Invoke(null,
                    new object[] { GCS.difficulty, bpmTimesSpeed, conductorPitch, marginScale });
                if (boxed == null) return false;
                counted = (double)_fCounted.GetValue(boxed);
                perfect = (double)_fPerfect.GetValue(boxed);
                pure = (double)_fPure.GetValue(boxed);
                if (_fXPerfect != null) xPerfect = (double)_fXPerfect.GetValue(boxed);
                return true;
            }

            if (_boundaryV148 != null)
            {
                counted = _boundaryV148(HitMarginGeneral.Counted, bpmTimesSpeed, conductorPitch, marginScale);
                perfect = _boundaryV148(HitMarginGeneral.Perfect, bpmTimesSpeed, conductorPitch, marginScale);
                pure = _boundaryV148(HitMarginGeneral.Pure, bpmTimesSpeed, conductorPitch, marginScale);
                return true;
            }
        }
        catch (Exception e)
        {
            // 调用方每帧重试（失败不写缓存），必须只警告一次，否则刷爆日志
            if (!_invokeWarned)
            {
                _invokeWarned = true;
                Loader.Warning($"GameCompat: 判定边界计算失败 ({e.Message})");
            }
        }

        return false;
    }

    // ===== XPerfect 显示色 =====
    // r150 基础游戏把判定配色放在 RDC.hitMarginColoursBySettings（玩家可在游戏设置里改），
    // 默认 colourXPerfect 为纯白 #FFFFFF；r148 无此成员，沿用外部 XPerfect mod 的 #4DCCFF 蓝。
    // 注意本 mod 其余判定色（#60FF4E/#A0FF4E/#FF6F4E/红）本来就与游戏默认配色逐项一致，
    // XPerfect 是唯一的历史偏差——源于外部 mod 而非游戏。
    private static string _xPerfectHex;
    private static bool _xPerfectHexProbed;

    /// <summary>XPerfect 数字/标签用的十六进制颜色（不含 # 前缀）。探测一次后缓存；游戏内改配色下次会话生效。</summary>
    public static string XPerfectHex
    {
        get
        {
            if (_xPerfectHex != null) return _xPerfectHex;
            if (!_xPerfectHexProbed)
            {
                _xPerfectHexProbed = true;
                if (HitMarginCompat.HasNativeXPerfect)
                {
                    try
                    {
                        var scheme = PatchManager.CreateStaticPropertyGetter<ColourSchemeHitMargin>(
                            typeof(RDC), "hitMarginColoursBySettings")();
                        var color = PatchManager.CreateMemberGetter<ColourSchemeHitMargin, Color>("colourXPerfect")(scheme);
                        _xPerfectHex = ColorUtility.ToHtmlStringRGB(color);
                        Loader.Log($"GameCompat: XPerfect 显示色 <- 游戏 colourXPerfect #{_xPerfectHex}");
                    }
                    catch (Exception e)
                    {
                        Loader.Warning($"GameCompat: 读取 colourXPerfect 失败，回退 #4DCCFF ({e.Message})");
                    }
                }
            }
            return _xPerfectHex ??= "4DCCFF";
        }
    }
}

/// <summary>
/// 精度/XScore 相关的计算（公式对照 r150 游戏源码推导）。
///
/// 游戏原生精度公式（scrMarginTracker.CalculatePercentAcc，r150）：
///   acc = (Perfect + Early/LatePerfect) / 总判定数 + Perfect×0.0001 − FailedFloor×0.0001
/// 即每个完美 +0.01%，acc 可以超过 100%；FailedFloor 每个再扣 0.01%。
/// 「潜力值」= 剩余格子全部满分时最终能达到的值。
/// </summary>
internal static class AccuracyMath
{
    /// <summary>XPerfect 的分值（r149+ 原生 HitMarginXScores：X=2、Perfect±=1）。</summary>
    public const int XPerfectValue = 2;

    /// <summary>r149+：Midspin 不算已判定格。
    /// 必须从 hits 自身求和而不是用 seqID：AddHit（我们的更新在此触发）先于
    /// MoveToNextFloor 更新 currentSeqID（scrPlanet.cs 785→930→1102），打击瞬间
    /// seqID 恒落后一格，用 seqID 会把满分算成 MAX--2，直到结算才恢复。
    /// 检查点重试时游戏清零 hitMarginsCount，此式也随之归零，与原生 acc 重开一致。</summary>
    public static int GetJudgedTiles(int[] hits, int seqID)
        => HitMarginCompat.HasNativeXPerfect
            ? SumHits(hits) - HitMarginCompat.Get(hits, HitMarginCompat.Midspin)
            : SumHits(hits);

    static int SumHits(int[] hits)
    {
        int sum = 0;
        for (int i = 0; i < hits.Length; i++) sum += hits[i];
        return sum;
    }

    /// <summary>剩余未判定格数（终点格不算）。</summary>
    public static int GetRemainingTiles(int seqID)
    {
        var floors = GameRefs.LevelMaker?.listFloors;
        if (floors == null || floors.Count == 0) return 0;
        int remaining = floors.Count - 1 - seqID;
        return remaining > 0 ? remaining : 0;
    }

    // ===== 原生 XScore 读数（r150）：不再自推分母 =====
    // 自推的 judged = Σhits − midspin 与原生口径不符：原生满分只数
    // PlayerHitFloors（seqID>0 且非 shouldBeAutoPlayed、非 midSpin，scrLevelMaker.cs:94），
    // 而 Σhits 还包含 auto 地板、Multipress 多余按压，以及死亡续跑时
    // RegisterDeadTiles 批量补记的 FailedFloor——每块 auto 地板即差 2 分。
    // 这些成员 r148 没有，直接引用过不了 compat-r148 编译门禁，故走反射缓存。

    static FieldInfo _nativeXScoreField;
    static PropertyInfo _nativeMaxXScoreProp;
    static bool _nativeXScoreProbed;

    /// <summary>读游戏原生 xScore / maxXScore（r150 公开成员）。菜单中 lm 为空时返回 false。
    /// xScore 当前分；maxXScore = 全图满分（PlayerHitFloors×2，常量）。
    /// MAX−n 用「游戏结算同口径」：maxXScore − xScore − 2×剩余玩家打击格
    /// （结算时剩余为 0，正好退化成 DetailedResults 的原式）。不能用
    /// playerHitMarginCount 当分母——它把 TooEarly 这类不前进格子的多余按压也计入。</summary>
    public static bool TryGetNativeXScore(scrMarginTracker tracker, out int xScore, out int maxXScore)
    {
        xScore = maxXScore = 0;
        if (!HitMarginCompat.HasNativeXPerfect || tracker == null || ADOBase.lm == null) return false;
        if (!_nativeXScoreProbed)
        {
            _nativeXScoreProbed = true;
            _nativeXScoreField = AccessTools.Field(typeof(scrMarginTracker), "xScore");
            _nativeMaxXScoreProp = AccessTools.Property(typeof(scrMarginTracker), "maxXScore");
        }
        if (_nativeXScoreField == null || _nativeMaxXScoreProp == null) return false;
        xScore = (int)_nativeXScoreField.GetValue(tracker);
        maxXScore = (int)_nativeMaxXScoreProp.GetValue(tracker);
        return true;
    }

    static FieldInfo _playerHitFloorsField;
    static int _remPlayerSeqID = -1, _remPlayerCount;

    /// <summary>剩余「玩家打击格」数（seqID>0 且非 auto、非 midspin 的格子）。
    /// 与原生 maxXScore 同口径，按 seqID 记忆化；反射失败退回全格口径。</summary>
    public static int GetRemainingPlayerHitFloors(int seqID)
    {
        if (!HitMarginCompat.HasNativeXPerfect) return GetRemainingTiles(seqID);
        if (_remPlayerSeqID == seqID) return _remPlayerCount;
        _playerHitFloorsField ??= AccessTools.Field(typeof(scrLevelMaker), "PlayerHitFloors");
        int count;
        if (_playerHitFloorsField?.GetValue(ADOBase.lm) is IReadOnlyList<scrFloor> floors)
        {
            count = 0;
            foreach (var f in floors) if (f != null && f.seqID > seqID) count++;
        }
        else count = GetRemainingTiles(seqID);
        _remPlayerSeqID = seqID;
        return _remPlayerCount = count;
    }

    /// <summary>从判定计数取「完美组」与「合格组」数量（合格 = 完美 + Early/LatePerfect）。
    /// r148 无 XPerfect 拆分，且 Auto 在 R149 前也计分。</summary>
    public static void GetAccuracyCounts(int[] hits, out int perfect, out int accurate)
    {
        if (HitMarginCompat.HasNativeXPerfect)
        {
            perfect = HitMarginCompat.Get(hits, HitMarginCompat.PerfectMinus)
                    + HitMarginCompat.Get(hits, HitMarginCompat.XPerfect)
                    + HitMarginCompat.Get(hits, HitMarginCompat.PerfectPlus);
            accurate = perfect
                    + HitMarginCompat.Get(hits, HitMarginCompat.EarlyPerfect)
                    + HitMarginCompat.Get(hits, HitMarginCompat.LatePerfect);
        }
        else
        {
            perfect = HitMarginCompat.Get(hits, HitMarginCompat.Perfect) + HitMarginCompat.Get(hits, HitMarginCompat.Auto);
            accurate = perfect
                    + HitMarginCompat.Get(hits, HitMarginCompat.EarlyPerfect)
                    + HitMarginCompat.Get(hits, HitMarginCompat.LatePerfect);
        }
    }

    /// <summary>潜力精度：剩余全完美时最终 acc。先用 acc 反解出游戏实际使用的分母，
    /// 再按 (perfect+remaining)×0.0001 + (accurate+remaining)/total 外推。</summary>
    public static float GetPotentialAccuracy(int[] hits, float acc, int judged, int remaining)
    {
        GetAccuracyCounts(hits, out int perfect, out int accurate);
        float rate = acc - perfect * 0.0001f;
        int count = accurate == 0 || rate <= 0 || float.IsNaN(rate) ? judged : (int)Math.Round(accurate / rate);
        if (count < accurate) count = accurate;
        int total = count + remaining;
        return total == 0 ? 1 : (perfect + remaining) * 0.0001f + (float)(accurate + remaining) / total;
    }

    /// <summary>潜力 X 精度：剩余全部 XPerfect 时的收敛值（与游戏原生 maxPossibleXAcc 同构）。
    /// 注：游戏对 XAcc 有检查点惩罚 ×0.9875^检查点数，此式与其原生 maxPossibleXAcc 一样未计入。</summary>
    public static float GetPotentialXAccuracy(float xacc, int judged, int remaining)
    {
        int total = judged + remaining;
        return total == 0 ? 1 : (xacc * judged + remaining) / total;
    }

    /// <summary>由判定计数计算 XScore（r149+ 语义：XPerfect=2、Perfect±=1）。</summary>
    public static int GetXScore(int[] hits)
        => HitMarginCompat.HasNativeXPerfect
            ? XPerfectValue * HitMarginCompat.Get(hits, HitMarginCompat.XPerfect)
              + HitMarginCompat.Get(hits, HitMarginCompat.PerfectMinus)
              + HitMarginCompat.Get(hits, HitMarginCompat.PerfectPlus)
            : 0;

    /// <summary>XScore 文本（Value / x÷max / x (MAX-n)）。
    /// 两处省略：n == 0（未掉分）时 (MAX-0) 没有信息量；潜力值的 MAX−n 恒等于
    /// 当前值的（两者都 = maxXScore − xScore，潜力只是两边同加 2×剩余格），
    /// potential:=true 时 MaxMinus 退化为纯数值，重复展示没有意义。</summary>
    public static string GetXScoreText(int xScore, int maxXScore, XScoreTextType type, bool potential = false) => type switch
    {
        XScoreTextType.WithMax => xScore + "/" + maxXScore,
        XScoreTextType.MaxMinus => !potential && maxXScore > xScore ? xScore + " (MAX-" + (maxXScore - xScore) + ")" : xScore.ToString(),
        _ => xScore.ToString(),
    };
}