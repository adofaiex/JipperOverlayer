using System;
using JipperOverlayer.Overlayer.Features;
using JipperOverlayer.Overlayer.Util;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 游戏数值 tag 集合（1/3）：进度、精度、检查点、Best、格子计数、连击、颜色等基础项。
/// 带 [Tag] 的静态方法由 TagManager 反射注册；数值统一返回 double（可用 {Tag:0.0} 控制小数），
/// 百分比类已 ×100（0-100 口径）；玩家相关 tag 首参 int player = 0（单人模式忽略）。
/// 输出默认保留 4 位小数以避免浮点尾巴，需要自定义精度用 :0.0 等 format。
/// </summary>
internal static partial class GameTags
{
    private static Overlay O => Overlay.Instance;
    private static IOverlayTextManager Manager => O?.OverlayTextManager;
    private static OverlayTextManagerNormal Normal => Manager as OverlayTextManagerNormal;
    private static OverlayTextManagerCoop Coop => Manager as OverlayTextManagerCoop;
    private static int[] Hits => O?.Hit;
    private static int SeqId => GameRefs.CurrentSeqID;

    /// <summary>数值 tag 的默认精度：避免 float→double 的二进制尾巴。</summary>
    private static double R(double v) => Math.Round(v, 4);

    // ===== 玩家相关共享 helper =====

    /// <summary>玩家当前所在格 seqID；单人或越界回退全局 CurrentSeqID。</summary>
    internal static int PlayerSeqId(int player)
    {
        if (!VersionSafe.IsCoopMode()) return SeqId;
        try
        {
            var players = scrPlayerManager.instance?.allPlayers;
            int? sid = null;
            if (players != null)
            {
                if (players is scrPlayer[] arr)
                {
                    if (player >= 0 && player < arr.Length)
                        sid = arr[player]?.planetarySystem?.chosenPlanet?.currfloor?.seqID;
                }
                else if (players is System.Collections.Generic.IList<scrPlayer> list)
                {
                    if (player >= 0 && player < list.Count)
                        sid = list[player]?.planetarySystem?.chosenPlanet?.currfloor?.seqID;
                }
            }
            if (sid.HasValue) return sid.Value;
        }
        catch { }
        return SeqId;
    }

    /// <summary>单人取全局口径；coop 从对应 marginTracker 读，NaN 一律兜底为 1（与 Normal 一致）。</summary>
    private static float PlayerTrackerFloat(int player, bool xAcc)
    {
        if (!VersionSafe.IsCoopMode())
        {
            float v = xAcc ? VersionSafe.GetPercentXAcc() : VersionSafe.GetPercentAcc();
            return float.IsNaN(v) ? 1f : v;
        }
        try
        {
            var trackers = scrMistakesManager.marginTrackers;
            if (trackers != null && player >= 0 && player < trackers.Length)
            {
                float v = xAcc ? trackers[player].percentXAcc : trackers[player].percentAcc;
                return float.IsNaN(v) ? 1f : v;
            }
        }
        catch { }
        return 1f;
    }

    /// <summary>coop 取指定玩家的判定计数快照；异常一律回退 null（调用方按 100% 兜底）。</summary>
    private static int[] SafePlayerHits(int player)
    {
        try { return VersionSafe.GetHitMarginsCountForPlayer(player); }
        catch { return null; }
    }

    private static float PlayerAcc(int player) => PlayerTrackerFloat(player, false);
    private static float PlayerXAcc(int player) => PlayerTrackerFloat(player, true);

    // ===== 进度 =====

    [Tag("Progress")]
    internal static double Progress(int player = 0)
    {
        // 按槽位小数位设置取整（与内置 Math.Round 同款，尾零由字符串化省略），
        // 模板里 {Progress} 即内置数字本身，调小数位滑条也会跟随。
        int dp = Main.Settings?.ProgressDecimal ?? 2;
        if (VersionSafe.IsCoopMode())
        {
            var datas = Coop?.PlayerDatas;
            if (datas != null && player >= 0 && player < datas.Length)
                return Math.Round(datas[player].Progress * 100.0, dp);
        }
        return Math.Round(GameRefs.PercentComplete * 100.0, dp);
    }

    // ===== 精度 =====

    [Tag("Accuracy")]
    internal static double Accuracy(int player = 0)
        => Math.Round(PlayerAcc(player) * 100.0, Main.Settings?.AccuracyDecimal ?? 2);

    [Tag("XAccuracy")]
    internal static double XAccuracy(int player = 0)
        => Math.Round(PlayerXAcc(player) * 100.0, Main.Settings?.XAccuracyDecimal ?? 2);

    [Tag("PotentialAccuracy")]
    internal static double PotentialAccuracy(int player = 0)
    {
        int[] hits = VersionSafe.IsCoopMode() ? SafePlayerHits(player) : Hits;
        if (hits == null) return 100.0;
        int seqId = PlayerSeqId(player);
        int judged = AccuracyMath.GetJudgedTiles(hits, seqId);
        int remaining = AccuracyMath.GetRemainingTiles(seqId);
        return Math.Round(AccuracyMath.GetPotentialAccuracy(hits, PlayerAcc(player), judged, remaining) * 100.0, Main.Settings?.AccuracyDecimal ?? 2);
    }

    [Tag("PotentialXAccuracy")]
    internal static double PotentialXAccuracy(int player = 0)
    {
        int[] hits = VersionSafe.IsCoopMode() ? SafePlayerHits(player) : Hits;
        if (hits == null) return 100.0;
        int seqId = PlayerSeqId(player);
        int judged = AccuracyMath.GetJudgedTiles(hits, seqId);
        int remaining = AccuracyMath.GetRemainingTiles(seqId);
        return Math.Round(AccuracyMath.GetPotentialXAccuracy(PlayerXAcc(player), judged, remaining) * 100.0, Main.Settings?.XAccuracyDecimal ?? 2);
    }

    // ===== Best / 检查点 =====

    [Tag("Best")]
    internal static double Best()
    {
        var o = O;
        if (o == null) return 0;
        float curBest = PlayCount.GetData(o.LastHash)?.GetBest(o.StartProgress, o.LastMultiplier) ?? 0f;
        float prog = Manager?.GetProgress() ?? 0f;
        float best = curBest > prog || o.AutoOnceEnabled ? curBest : prog;
        return Math.Round(best * 100.0, Main.Settings?.BestDecimal ?? 2);
    }

    [Tag("CheckpointPassed")]
    internal static double CheckpointPassed() => Normal?.CurCheck ?? Coop?.CurCheck ?? 0;

    [Tag("CheckpointsTotal")]
    internal static double CheckpointsTotal()
    {
        int[] cps = O?.Checkpoints;
        return cps?.Length ?? 0;
    }

    [Tag("CheckpointsUsed")]
    internal static double CheckpointsUsed() => GameRefs.CheckpointsUsed;

    // ===== 格子 =====

    [Tag("CurTile")]
    internal static double CurTile() => SeqId;

    [Tag("TotalTile")]
    internal static double TotalTile()
    {
        var floors = GameRefs.LevelMaker?.listFloors;
        return floors != null && floors.Count > 0 ? floors.Count - 1 : 0;
    }

    [Tag("TilesLeft")]
    internal static double TilesLeft() => AccuracyMath.GetRemainingTiles(SeqId);

    [Tag("StartTile")]
    internal static double StartTile() => O?.StartTile ?? 0;

    [Tag("StartProgress")]
    internal static double StartProgress() => R((O?.StartProgress ?? 0f) * 100.0);

    /// <summary>进度整段：从检查点开始时附带起始进度；联机逐玩家显示。
    /// 首段默认不带分隔符；导入模板时显式开启。</summary>
    [Tag("ProgressRun")]
    internal static string ProgressRun(int decimals = -1, bool includeSeparator = false, string gradient = null)
    {
        var s = Main.Settings;
        int dp = NumberDecimals(s?.ProgressDecimal ?? 2, decimals);
        var o = O;
        bool fromCheckpoint = o != null && o.StartTile > 0;
        string separator = s?.Labels?.ProgressSeparator;
        string prefix = includeSeparator ? separator : "";
        var palette = ProgressTemplateGradient.Resolve(gradient) ?? s?.Colors?.Progress;
        string Segment(float progress) => Colorize(palette?.GetHex(progress, true), PercentText(progress, dp));
        if (VersionSafe.IsCoopMode())
        {
            var datas = Coop?.PlayerDatas;
            string players = CoopSegments(separator, -1, i => Segment(datas == null ? GameRefs.PercentComplete : datas[i].Progress));
            return prefix + (fromCheckpoint ? Segment(o.StartProgress) + " ~" + separator + players : players);
        }
        string current = Segment(Normal?.Progress ?? GameRefs.PercentComplete);
        return prefix + (fromCheckpoint ? Segment(o.StartProgress) + " ~ " + current : current);
    }

    /// <summary>详细进度开启时显示格数与百分比；联机和普通模式用 ProgressRun。</summary>
    [Tag("ProgressDetailed")]
    internal static string ProgressDetailed(int decimals = -1, bool includeSeparator = false, string gradient = null)
    {
        var s = Main.Settings;
        if (!(s?.DetailedProgress ?? false) || VersionSafe.IsCoopMode()) return ProgressRun(decimals, includeSeparator, gradient);
        int dp = NumberDecimals(s.ProgressDecimal, decimals);
        int cur = SeqId;
        int last = (int)TotalTile();
        float progress = Normal?.Progress ?? GameRefs.PercentComplete;
        var palette = ProgressTemplateGradient.Resolve(gradient) ?? s.Colors?.Progress;
        string hex = palette == null ? null : ColorUtility.ToHtmlStringRGBA(palette.GetColor(progress));
        string prefix = includeSeparator ? s.Labels?.ProgressSeparator : "";
        return prefix + Colorize(hex, cur + " / " + last
            + (cur == last ? "" : " [-" + (last - cur) + "]")
            + " (" + PercentText(progress, dp) + ")");
    }

    // 用 playerIndex 而非 player：后者省略时会被 TagManager 替换成当前玩家，
    // 而整行 tag 需要在省略时汇总全员，只有显式指定非负索引才挑出一名玩家。
    [Tag("AccuracyRun")]
    internal static string AccuracyRun(int playerIndex = -1, int decimals = -1, bool potentialOnly = false, bool includeSeparator = false)
        => AccuracyRunCore(false, playerIndex, decimals, potentialOnly, includeSeparator);

    [Tag("XAccuracyRun")]
    internal static string XAccuracyRun(int playerIndex = -1, int decimals = -1, bool potentialOnly = false, bool includeSeparator = false)
        => AccuracyRunCore(true, playerIndex, decimals, potentialOnly, includeSeparator);

    private static string AccuracyRunCore(bool isX, int playerIndex, int decimals, bool potentialOnly, bool includeSeparator)
    {
        var s = Main.Settings;
        var type = isX ? s?.XAccuracyTextType ?? PotentialTextType.Current : s?.AccuracyTextType ?? PotentialTextType.Current;
        int dp = NumberDecimals(isX ? s?.XAccuracyDecimal ?? 2 : s?.AccuracyDecimal ?? 2, decimals);
        string separator = isX ? s?.Labels?.XAccuracySeparator : s?.Labels?.AccuracySeparator;
        string segments = VersionSafe.IsCoopMode()
            ? CoopSegments(separator, playerIndex, i => AccuracySegment(i, isX, type, dp, potentialOnly, true))
            : AccuracySegment(0, isX, type, dp, potentialOnly, false);
        return includeSeparator && segments.Length > 0 ? separator + segments : segments;
    }

    private static string AccuracySegment(int index, bool isX, PotentialTextType type, int dp, bool potentialOnly, bool coop)
    {
        if (coop && TrackerFor(index) == null) return "";
        var colors = Main.Settings?.Colors;
        float xAcc = PlayerXAcc(index);
        float current = isX ? xAcc : PlayerAcc(index);
        int seq = PlayerSeqId(index);
        bool showPotential = potentialOnly || type == PotentialTextType.Potential;
        bool showBoth = !showPotential && ShowInlinePotential(type, coop);
        int remaining = 0;
        float potential = current;
        if (showPotential || showBoth)
        {
            var hits = coop ? SafePlayerHits(index) : Hits;
            int judged = AccuracyMath.GetJudgedTiles(hits, seq);
            remaining = AccuracyMath.GetRemainingTiles(seq);
            potential = isX ? AccuracyMath.GetPotentialXAccuracy(current, judged, remaining)
                : AccuracyMath.GetPotentialAccuracy(hits, current, judged, remaining);
        }

        // 内置联机段用当前值决定色彩；单人的独立潜力行用潜力值决定色彩。
        bool colorPotential = !coop && showPotential;
        float valueForColor = colorPotential ? potential : current;
        float maxAcc = 1 + (seq - (O?.NoCheckStartTile ?? 0) + 1) * 0.0001f;
        float accColorRatio = xAcc == 1 ? 1 : valueForColor / (maxAcc + (colorPotential ? remaining * 0.0001f : 0f));
        string hex = colors == null ? null : coop
            ? (isX ? colors.GetXAccuracyHex(valueForColor, valueForColor == 1) : colors.GetAccuracyHex(accColorRatio, xAcc == 1))
            : ColorUtility.ToHtmlStringRGBA(isX ? colors.GetXAccuracyColor(valueForColor, valueForColor == 1)
                : colors.GetAccuracyColor(accColorRatio, xAcc == 1));
        string text = showPotential ? PercentText(potential, dp) : PercentText(current, dp);
        if (showBoth) text += " (" + PercentText(potential, dp) + ")";
        return Colorize(hex, text);
    }

    [Tag("XScoreRun")]
    internal static string XScoreRun(int playerIndex = -1, bool potentialOnly = false, bool includeSeparator = false)
    {
        string separator = Main.Settings?.Labels?.XScoreSeparator;
        string segments = VersionSafe.IsCoopMode()
            ? CoopSegments(separator, playerIndex, i => XScoreSegment(i, potentialOnly, true))
            : XScoreSegment(0, potentialOnly, false);
        return includeSeparator && segments.Length > 0 ? separator + segments : segments;
    }

    private static string XScoreSegment(int index, bool potentialOnly, bool coop)
    {
        var s = Main.Settings;
        GetXScoreValues(index, out int current, out int max, out int potential, out int total);
        var type = s?.XScorePotentialType ?? PotentialTextType.Current;
        var format = s?.XScoreTextType ?? XScoreTextType.MaxMinus;
        bool showPotential = potentialOnly || type == PotentialTextType.Potential;
        bool colorPotential = !coop && showPotential;
        int colorValue = colorPotential ? potential : current;
        int colorMax = colorPotential ? total : max;
        float ratio = colorMax == 0 ? 1 : (float)colorValue / colorMax;
        var colors = s?.Colors;
        string hex = colors == null ? null : coop ? colors.GetXScoreHex(ratio)
            : ColorUtility.ToHtmlStringRGBA(colors.GetXScoreColor(ratio));
        string text = showPotential ? AccuracyMath.GetXScoreText(potential, total, format, potential: true)
            : AccuracyMath.GetXScoreText(current, max, format);
        if (!showPotential && ShowInlinePotential(type, coop))
            text += " (" + AccuracyMath.GetXScoreText(potential, total, format, potential: true) + ")";
        return Colorize(hex, text);
    }

    [Tag("XScoreText")]
    internal static string XScoreText(int player = 0)
        => XScoreValueText(Main.Settings?.XScoreTextType ?? XScoreTextType.MaxMinus, false, player);

    [Tag("PotentialXScoreText")]
    internal static string PotentialXScoreText(int player = 0)
        => XScoreValueText(Main.Settings?.XScoreTextType ?? XScoreTextType.MaxMinus, true, player);

    private static string XScoreValueText(XScoreTextType type, bool potential, int player)
    {
        GetXScoreValues(player, out int x, out int max, out int potX, out int potTotal);
        return potential ? AccuracyMath.GetXScoreText(potX, potTotal, type, potential: true)
            : AccuracyMath.GetXScoreText(x, max, type);
    }

    private static bool ShowInlinePotential(PotentialTextType type, bool coop)
        => type == PotentialTextType.BothInOneLine || (coop && type == PotentialTextType.Both);

    private static string CoopSegments(string separator, int playerIndex, Func<int, string> render)
    {
        int count = Coop?.PlayerDatas?.Length ?? VersionSafe.GetPlayerCount();
        if (playerIndex >= 0) return playerIndex < count ? render(playerIndex) : "";
        var sb = new System.Text.StringBuilder(128);
        for (int i = 0; i < count; i++)
        {
            string segment = render(i);
            if (string.IsNullOrEmpty(segment)) continue;
            if (sb.Length > 0) sb.Append(separator);
            sb.Append(segment);
        }
        return sb.ToString();
    }

    private static int NumberDecimals(int setting, int requested) => requested < 0 ? setting : Math.Min(requested, 4);
    private static string PercentText(float fraction, int dp)
        => Math.Round(fraction * 100, dp).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
    private static string Colorize(string hex, string text) => "<color=#" + (hex ?? "FFFFFF") + ">" + text + "</color>";

    // ===== 杂项 =====

    [Tag("Combo")]
    internal static double Combo() => GameLifecycleHelper.ComboCount;

    [Tag("Newline", TagState.None)]
    internal static string Newline() => "\n";

    /// <summary>玩家颜色 hex（不含 #）；无效回退白色。coop 用 {PColor(1)} 取 2P。</summary>
    [Tag("PColor", TagState.None)]
    internal static string PColor(int player = 0)
    {
        string hex = VersionSafe.GetPlayerColorHex(player);
        return string.IsNullOrEmpty(hex) ? "FFFFFF" : hex;
    }
}
