using System;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 游戏数值 tag 集合（2/3）：XScore 四值 + 判定计数。
/// XScore 口径与 <see cref="AccuracyMath"/> 完全一致（原生 r149+ 走 marginTracker，
/// 否则按判定计数自推），因此模板里的 {XScore} 与内置 XScore 文本逐分相同。
/// 玩家相关 tag 首参 int player = 0；单人模式忽略该参数。
/// </summary>
internal static partial class GameTags
{
    /// <summary>取指定玩家的判定计数数组：coop 走每玩家快照，单人走全局。</summary>
    internal static int[] HitsFor(int player)
    {
        if (!VersionSafe.IsCoopMode()) return Hits;
        try { return VersionSafe.GetHitMarginsCountForPlayer(player); }
        catch { return Hits; }
    }

    /// <summary>XScore 对应的 marginTracker（单人取 0 号）。越界返回 null。</summary>
    private static scrMarginTracker TrackerFor(int player)
    {
        var trackers = scrMistakesManager.marginTrackers;
        if (trackers == null || trackers.Length == 0) return null;
        int i = VersionSafe.IsCoopMode() ? player : 0;
        return i >= 0 && i < trackers.Length ? trackers[i] : null;
    }

    /// <summary>
    /// XScore 四值：x = 当前分，max = 已判定计分格满分，potX = 潜力分，potTotal = 潜力满分。
    /// 原生可用时 max 与 potTotal 都取自游戏 maxXScore（全图满分），
    /// 与 OverlayTextManagerNormal.UpdateXScore / Coop.SetXScore 同源。
    /// </summary>
    internal static void GetXScoreValues(int player, out int x, out int max, out int potX, out int potTotal)
    {
        int[] hits = HitsFor(player);
        int seqId = PlayerSeqId(player);
        int judged = AccuracyMath.GetJudgedTiles(hits, seqId);
        int remaining = AccuracyMath.GetRemainingTiles(seqId);
        if (AccuracyMath.TryGetNativeXScore(TrackerFor(player), out x, out int mapMax))
        {
            AccuracyMath.GetXScoreValues(hits, x, mapMax, out max, out potX);
            potTotal = mapMax;
            return;
        }
        x = AccuracyMath.GetXScore(hits);
        max = judged * AccuracyMath.XPerfectValue;
        potTotal = max + remaining * AccuracyMath.XPerfectValue;
        potX = x + remaining * AccuracyMath.XPerfectValue;
    }

    // ===== XScore =====

    [Tag("XScore")]
    internal static double XScore(int player = 0)
    {
        GetXScoreValues(player, out int x, out _, out _, out _);
        return x;
    }

    /// <summary>已判定计分格的满分（当前分 + 已掉分 = 此项）。</summary>
    [Tag("XScoreMax")]
    internal static double XScoreMax(int player = 0)
    {
        GetXScoreValues(player, out _, out int max, out _, out _);
        return max;
    }

    /// <summary>MAX-n 里的 n：到目前为此掉了多少分（0 = 全 XPerfect）。</summary>
    [Tag("XScoreLoss")]
    internal static double XScoreLoss(int player = 0)
    {
        GetXScoreValues(player, out int x, out int max, out _, out _);
        return max - x;
    }

    /// <summary>全图满分（原生 maxXScore / 自推满分）。</summary>
    [Tag("XScoreTotal")]
    internal static double XScoreTotal(int player = 0)
    {
        GetXScoreValues(player, out _, out _, out _, out int potTotal);
        return potTotal;
    }

    [Tag("PotentialXScore")]
    internal static double PotentialXScore(int player = 0)
    {
        GetXScoreValues(player, out _, out _, out int potX, out _);
        return potX;
    }

    /// <summary>潜力值的分母（剩余全部 XPerfect 时的满分）。</summary>
    [Tag("PotentialXScoreMax")]
    internal static double PotentialXScoreMax(int player = 0)
    {
        GetXScoreValues(player, out _, out _, out _, out int potTotal);
        return potTotal;
    }

    /// <summary>XScore / 已判定满分 x100（满命中 = 100）。供 ColorRange 做「满命中转金」：
    /// 内置配色是 t≥1 转 PerfectColor 金，渐变两端点表达不了顶格色，用 atMax 参数实现。</summary>
    [Tag("XScoreRatio")]
    internal static double XScoreRatio(int player = 0)
    {
        GetXScoreValues(player, out int x, out int max, out _, out _);
        return max <= 0 ? 100.0 : Math.Round(x * 100.0 / max, 2);
    }

    // ===== 判定计数（单人 = 全局数组；coop = 指定玩家）=====

    [Tag("XPerfect")]
    internal static double XPerfectCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.XPerfect);

    [Tag("PerfectMinus")]
    internal static double PerfectMinusCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.PerfectMinus);

    [Tag("PerfectPlus")]
    internal static double PerfectPlusCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.PerfectPlus);

    /// <summary>中心完美总数（r148 = Perfect；r150 = PerfectMinus + XPerfect + PerfectPlus）。</summary>
    [Tag("Perfect")]
    internal static double PerfectCount(int player = 0)
        => HitMarginCompat.SumCorePerfect(HitsFor(player));

    [Tag("EarlyPerfect")]
    internal static double EarlyPerfectCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.EarlyPerfect);

    [Tag("LatePerfect")]
    internal static double LatePerfectCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.LatePerfect);

    [Tag("VeryEarly")]
    internal static double VeryEarlyCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.VeryEarly);

    [Tag("VeryLate")]
    internal static double VeryLateCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.VeryLate);

    [Tag("TooEarly")]
    internal static double TooEarlyCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.TooEarly);

    [Tag("TooLate")]
    internal static double TooLateCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.TooLate);

    [Tag("FailMiss")]
    internal static double FailMissCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.FailMiss);

    [Tag("FailOverload")]
    internal static double FailOverloadCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.FailOverload);

    /// <summary>死亡数 = FailMiss + FailOverload（与 Death 文本同口径）。</summary>
    [Tag("Death")]
    internal static double DeathCount(int player = 0)
    {
        int[] h = HitsFor(player);
        return HitMarginCompat.Get(h, HitMarginCompat.FailMiss)
             + HitMarginCompat.Get(h, HitMarginCompat.FailOverload);
    }

    [Tag("Multipress")]
    internal static double MultipressCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.Multipress);

    /// <summary>已判定格数（含 auto / midspin，用于进度类统计）。</summary>
    [Tag("JudgedTiles")]
    internal static double JudgedTiles(int player = 0)
        => AccuracyMath.GetJudgedTiles(HitsFor(player), PlayerSeqId(player));

    /// <summary>已判定计分格数（auto 与 midspin 不计分）。</summary>
    [Tag("ScoredTiles")]
    internal static double ScoredTiles(int player = 0)
        => AccuracyMath.GetScoredJudgedTiles(HitsFor(player));
}
