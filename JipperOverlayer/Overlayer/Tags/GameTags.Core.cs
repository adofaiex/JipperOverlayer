using System;
using JipperOverlayer.Overlayer.Features;
using JipperOverlayer.Overlayer.Util;

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
