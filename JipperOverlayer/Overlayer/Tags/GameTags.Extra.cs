using System;
using System.Reflection;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 游戏数值 tag 集合（4/4）：从 Overlayer.Module.ADOFAI 移植的高价值项。
/// - 判定聚合（Fast/Slow/InnerPerfect/OuterPerfect/V/T）与 Auto/OverPress 计数
/// - 最大连击、最大可达连击（仰赖 Overlay.MaxCombo 的逐次峰值记录）
/// - 峰值 BPM / 不含音高的 BPM
/// - 音乐/地图时间的分量（时/分/秒/毫秒），便于自己排版
/// - 曲名 / 曲师 / 关卡名（反射，按成员缓存）
/// - 自动、无失败、练习、暂停等状态布尔
/// - 0-1 口径的进度与精度
/// </summary>
internal static partial class GameTags
{
    // ===== 判定聚合 =====

    /// <summary>Auto 判定数（自动地板）。</summary>
    [Tag("Auto")]
    internal static double AutoCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.Auto);

    [Tag("OverPress")]
    internal static double OverPressCount(int player = 0)
        => HitMarginCompat.Get(HitsFor(player), HitMarginCompat.OverPress);

    /// <summary>Miss 数（= FailMiss），与 Overlayer 的 {Miss} 同名。</summary>
    [Tag("Miss")]
    internal static double MissCount(int player = 0) => FailMissCount(player);

    /// <summary>过载失败数（= FailOverload）。</summary>
    [Tag("Overload")]
    internal static double OverloadCount(int player = 0) => FailOverloadCount(player);

    /// <summary>偏快组：TooEarly + VeryEarly + EarlyPerfect + PerfectMinus。</summary>
    [Tag("Fast")]
    internal static double FastCount(int player = 0)
    {
        int[] h = HitsFor(player);
        return HitMarginCompat.Get(h, HitMarginCompat.TooEarly)
             + HitMarginCompat.Get(h, HitMarginCompat.VeryEarly)
             + HitMarginCompat.Get(h, HitMarginCompat.EarlyPerfect)
             + HitMarginCompat.Get(h, HitMarginCompat.PerfectMinus);
    }

    /// <summary>偏慢组：PerfectPlus + LatePerfect + VeryLate + TooLate。</summary>
    [Tag("Slow")]
    internal static double SlowCount(int player = 0)
    {
        int[] h = HitsFor(player);
        return HitMarginCompat.Get(h, HitMarginCompat.PerfectPlus)
             + HitMarginCompat.Get(h, HitMarginCompat.LatePerfect)
             + HitMarginCompat.Get(h, HitMarginCompat.VeryLate)
             + HitMarginCompat.Get(h, HitMarginCompat.TooLate);
    }

    /// <summary>内圈完美：PerfectMinus + PerfectPlus（不含 X）。</summary>
    [Tag("InnerPerfect")]
    internal static double InnerPerfectCount(int player = 0)
        => PerfectMinusCount(player) + PerfectPlusCount(player);

    /// <summary>外圈完美：EarlyPerfect + LatePerfect。</summary>
    [Tag("OuterPerfect")]
    internal static double OuterPerfectCount(int player = 0)
        => EarlyPerfectCount(player) + LatePerfectCount(player);

    /// <summary>Very 组：VeryEarly + VeryLate。</summary>
    [Tag("VeryTotal")]
    internal static double VeryTotalCount(int player = 0)
        => VeryEarlyCount(player) + VeryLateCount(player);

    /// <summary>Too 组：TooEarly + TooLate。</summary>
    [Tag("TooTotal")]
    internal static double TooTotalCount(int player = 0)
        => TooEarlyCount(player) + TooLateCount(player);

    /// <summary>纯 XPerfect（不含 Auto）——与 {XPerfect} 同值，保留 Overlayer 的命名习惯。</summary>
    [Tag("PXP")]
    internal static double PureXPerfectCount(int player = 0) => XPerfectCount(player);

    // ===== 连击 =====

    /// <summary>本次尝试内的最大连击（Overlay.UpdateCombo 记录峰值）。</summary>
    [Tag("MaxCombo")]
    internal static double MaxCombo() => O?.MaxCombo ?? 0;

    /// <summary>剩余格全部完美时能达到的连击。</summary>
    [Tag("MaxPossibleCombo")]
    internal static double MaxPossibleCombo()
        => (O?.MaxCombo ?? 0) + AccuracyMath.GetRemainingTiles(SeqId);

    // ===== BPM：去音高 / 全图峰值 =====

    private static double Pitch => GameRefs.SongPitch > 0f ? GameRefs.SongPitch : 1f;

    [Tag("TileBpmWithoutPitch")]
    internal static double TileBpmWithoutPitch() => R(TileBpm() / Pitch);

    [Tag("CurBpmWithoutPitch")]
    internal static double CurBpmWithoutPitch() => R(CurBpm() / Pitch);

    /// <summary>全图最高的「每格 BPM」（不含音高）。按 listFloors 引用+长度缓存，
    /// 换图或列表变化时重算一次。</summary>
    [Tag("MaxBpmWithoutPitch")]
    internal static double MaxBpmWithoutPitch() => Math.Round(PeakBpm(), 2);

    /// <summary>全图最高 BPM（含音高）。</summary>
    [Tag("MaxBpm")]
    internal static double MaxBpm() => Math.Round(PeakBpm() * Pitch, 2);

    [Tag("MaxKps")]
    internal static double MaxKps() => Math.Round(PeakBpm() * Pitch / 60.0, 2);

    private static object _peakFloors;
    private static int _peakCount = -1;
    private static double _peakBpm;

    private static double PeakBpm()
    {
        var floors = GameRefs.LevelMaker?.listFloors;
        if (floors == null || floors.Count < 2) return 0;
        if (ReferenceEquals(_peakFloors, floors) && _peakCount == floors.Count) return _peakBpm;
        double max = 0;
        for (int i = 0; i + 1 < floors.Count; i++)
        {
            double d = floors[i + 1].entryTime - floors[i].entryTime;
            if (d > 0)
            {
                double v = 60.0 / d;
                if (v > max) max = v;
            }
        }
        _peakFloors = floors;
        _peakCount = floors.Count;
        _peakBpm = max;
        return max;
    }

    // ===== 时间分量（与 MusicTime/MapTime 同源，只是拆成时/分/秒/毫秒）=====

    private static int PartHour(float t) => (int)(t / 3600f);
    private static int PartMinute(float t) => (int)(t % 3600f / 60f);
    private static int PartSecond(float t) => (int)(t % 60f);
    private static int PartMilli(float t) => (int)((t - (int)t) * 1000f);

    [Tag("MusicHour")] internal static double MusicHour() { MusicTimes(out float t, out _); return PartHour(t); }
    [Tag("MusicMinute")] internal static double MusicMinute() { MusicTimes(out float t, out _); return PartMinute(t); }
    [Tag("MusicSecond")] internal static double MusicSecond() { MusicTimes(out float t, out _); return PartSecond(t); }
    [Tag("MusicMilliSecond")] internal static double MusicMilliSecond() { MusicTimes(out float t, out _); return PartMilli(t); }

    [Tag("MusicTotalMinute")] internal static double MusicTotalMinute() { MusicTimes(out _, out float t); return PartMinute(t); }
    [Tag("MusicTotalSecond")] internal static double MusicTotalSecond() { MusicTimes(out _, out float t); return PartSecond(t); }
    [Tag("MusicTotalMilliSecond")] internal static double MusicTotalMilliSecond() { MusicTimes(out _, out float t); return PartMilli(t); }

    [Tag("MapMinute")] internal static double MapMinute() { MapTimes(out float t, out _); return PartMinute(t); }
    [Tag("MapSecond")] internal static double MapSecond() { MapTimes(out float t, out _); return PartSecond(t); }
    [Tag("MapMilliSecond")] internal static double MapMilliSecond() { MapTimes(out float t, out _); return PartMilli(t); }

    [Tag("MapTotalMinute")] internal static double MapTotalMinute() { MapTimes(out _, out float t); return PartMinute(t); }
    [Tag("MapTotalSecond")] internal static double MapTotalSecond() { MapTimes(out _, out float t); return PartSecond(t); }
    [Tag("MapTotalMilliSecond")] internal static double MapTotalMilliSecond() { MapTimes(out _, out float t); return PartMilli(t); }

    // ===== 关卡信息（反射读 levelData，按成员缓存 PropertyInfo）=====

    private static PropertyInfo _songProp, _artistProp;
    private static bool _levelProbed;

    private static string LevelString(ref PropertyInfo cache, string member)
    {
        try
        {
            var ld = scnGame.instance?.levelData;
            if (ld == null) return "";
            if (!_levelProbed)
            {
                _levelProbed = true;
                _songProp = ld.GetType().GetProperty("song", BindingFlags.Public | BindingFlags.Instance);
                _artistProp = ld.GetType().GetProperty("artist", BindingFlags.Public | BindingFlags.Instance);
            }
            if (cache == null) return "";
            return cache.GetValue(ld) as string ?? "";
        }
        catch { return ""; }
    }

    [Tag("Title", TagState.None)] internal static string Title() => LevelString(ref _songProp, "song");
    [Tag("Artist", TagState.None)] internal static string Artist() => LevelString(ref _artistProp, "artist");

    /// <summary>左上角关卡名文本（已去掉 TMP 富文本标签，避免把 &lt;size&gt; 之类带进模板）。</summary>
    [Tag("LevelName", TagState.None)]
    internal static string LevelName()
    {
        try
        {
            var label = GameRefs.Controller?.txtLevelName;
            if (label == null) return "";
            string raw = label.text;
            if (string.IsNullOrEmpty(raw)) return "";
            if (ReferenceEquals(raw, _lastLevelNameRaw)) return _lastLevelNameStripped;
            _lastLevelNameRaw = raw;
            _lastLevelNameStripped = StripRichText(raw);
            return _lastLevelNameStripped;
        }
        catch { return ""; }
    }

    private static string _lastLevelNameRaw, _lastLevelNameStripped;

    private static string StripRichText(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        bool inTag = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    // ===== 状态布尔 =====

    [Tag("IsAuto", TagState.None)] internal static bool IsAutoEnabled() => GameRefs.IsAuto;
    [Tag("IsNoFail", TagState.None)] internal static bool IsNoFailEnabled() => GameRefs.IsNoFail;
    [Tag("IsPaused", TagState.None)] internal static bool IsPausedNow() => GameRefs.IsPaused;

    [Tag("IsPractice", TagState.None)]
    internal static bool IsPracticeModeEnabled()
    {
        try { return GCS.practiceMode; }
        catch { return false; }
    }

    // ===== 0-1 口径（便于 :0.000 之类自行排版）=====

    [Tag("Progress01")] internal static double Progress01(int player = 0) => Progress(player) / 100.0;
    [Tag("Accuracy01")] internal static double Accuracy01(int player = 0) => Accuracy(player) / 100.0;
    [Tag("XAccuracy01")] internal static double XAccuracy01(int player = 0) => XAccuracy(player) / 100.0;
    [Tag("Best01")] internal static double Best01() => Best() / 100.0;
}
