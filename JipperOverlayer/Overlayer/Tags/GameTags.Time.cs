using System;
using UnityEngine;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 游戏数值 tag 集合（3/3）：时间、BPM、尝试次数、打击时机、作者、状态、判定时间窗。
/// 时间类同时提供「格式化字符串」（{MusicTime} → 1:23）与「原始秒数」（{MusicSeconds} → 83.4），
/// 后者可配合 :0.00 等 format 自行排版。
/// </summary>
internal static partial class GameTags
{
    private static ExtendedOverlay.ExtendedOverlayModule Ext => O?.ExtendedOverlay;

    // ===== 时间 =====

    private static string FmtTime(float time, bool hour)
        => Main.Settings != null && Main.Settings.TimeDecimals
            ? TimeFormatter.FormatWithDecimals(time, hour)
            : TimeFormatter.Format(time, hour);

    private static float FloorsTotalTime()
    {
        var floors = GameRefs.LevelMaker?.listFloors;
        return floors != null && floors.Count > 0 ? (float)floors[floors.Count - 1].entryTime : 0f;
    }

    /// <summary>当前音乐时间与总时长（无音频时用地图末格时间兜底，与 Time 文本一致）。</summary>
    private static void MusicTimes(out float time, out float total)
    {
        time = 0f;
        total = 0f;
        var song = GameRefs.Song;
        if (song == null) { total = FloorsTotalTime(); return; }
        float t = song.time;
        if (float.IsNaN(t) || float.IsInfinity(t) || t < 0) t = 0;
        var clip = song.clip;
        total = clip != null && clip.length > 0 ? clip.length : FloorsTotalTime();
        if (total > 0 && t > total) t = total;
        time = t;
    }

    /// <summary>当前地图时间与总时长（末格 entryTime）。</summary>
    private static void MapTimes(out float time, out float total)
    {
        total = FloorsTotalTime();
        double raw = GameRefs.ConductorAddoffset + GameRefs.ConductorSongpositionMinusi;
        float t = float.IsNaN((float)raw) || float.IsInfinity((float)raw) ? 0f : (float)raw;
        if (t < 0) t = 0;
        else if (total > 0 && t > total) t = total;
        time = t;
    }

    [Tag("MusicTime")]
    internal static string MusicTime()
    {
        MusicTimes(out float t, out float total);
        return FmtTime(t, total >= 3600);
    }

    [Tag("MusicTotal")]
    internal static string MusicTotal()
    {
        MusicTimes(out _, out float total);
        return FmtTime(total, total >= 3600);
    }

    [Tag("MusicSeconds")]
    internal static double MusicSeconds()
    {
        MusicTimes(out float t, out _);
        return R(t);
    }

    [Tag("MusicTotalSeconds")]
    internal static double MusicTotalSeconds()
    {
        MusicTimes(out _, out float total);
        return R(total);
    }

    [Tag("MapTime")]
    internal static string MapTime()
    {
        MapTimes(out float t, out float total);
        return FmtTime(t, total >= 3600);
    }

    [Tag("MapTotal")]
    internal static string MapTotal()
    {
        MapTimes(out _, out float total);
        return FmtTime(total, total >= 3600);
    }

    [Tag("MapSeconds")]
    internal static double MapSeconds()
    {
        MapTimes(out float t, out _);
        return R(t);
    }

    [Tag("MapTotalSeconds")]
    internal static double MapTotalSeconds()
    {
        MapTimes(out _, out float total);
        return R(total);
    }

    // ===== BPM / KPS =====

    /// <summary>按当前格重算（与显示文本同源：含歌曲音高与行星速度）。</summary>
    private static BpmCalculator.Result CurrentBpmValues()
    {
        scrFloor floor = GameRefs.CurrentFloor ?? GameRefs.FirstFloor;
        if (floor == null) return new BpmCalculator.Result(0, 0, 0);
        return BpmCalculator.Calculate(floor, (float)(GameRefs.SongPitch * VersionSafe.GetPlanetSpeed(GameRefs.ControllerInstance)));
    }

    [Tag("TileBpm")]
    internal static double TileBpm()
    {
        var o = O;
        if (o != null && o.LastTileBpm >= 0) return Math.Round(o.LastTileBpm, 2);
        return Math.Round(CurrentBpmValues().TileBpm, 2);
    }

    [Tag("CurBpm")]
    internal static double CurBpm()
    {
        var o = O;
        if (o != null && o.LastCurBpm >= 0) return Math.Round(o.LastCurBpm, 2);
        return Math.Round(CurrentBpmValues().CurrentBpm, 2);
    }

    [Tag("Kps")]
    internal static double Kps()
    {
        var ext = Ext;
        if (ext != null && ext.LastKps >= 0) return Math.Round(ext.LastKps, 2);
        return Math.Round(CurrentBpmValues().Kps, 2);
    }

    // ===== 尝试次数 =====

    [Tag("Attempt")]
    internal static double Attempt()
    {
        var o = O;
        if (o == null) return 0;
        return PlayCount.GetData(o.LastHash)?.GetAttempts(o.StartProgress, o.LastMultiplier) ?? 0;
    }

    [Tag("FullAttempt")]
    internal static double FullAttempt()
        => O == null ? 0 : PlayCount.GetData(O.LastHash)?.GetAttempts() ?? 0;

    // ===== 打击时机 / 判定区间 =====

    /// <summary>最近一次打击的毫秒偏移（与 Timing 文本同源）。</summary>
    [Tag("Timing")]
    internal static double Timing()
        => Math.Round(Ext?.LastTiming ?? 0f, Main.Settings?.TimingDecimal ?? 5);

    [Tag("AvgTiming")]
    internal static double AvgTiming()
        => Math.Round(Ext?.AverageTiming ?? 0f, Main.Settings?.TimingDecimal ?? 5);

    /// <summary>判定区间倍率（百分比，100 = 原速）。</summary>
    [Tag("TimingScale")]
    internal static double TimingScale()
    {
        var floor = GameRefs.CurrentFloor;
        return floor == null ? 0 : R(Math.Round(floor.marginScale * 100, 2));
    }

    [Tag("HitOffset")]
    internal static double HitOffset()
        => Math.Round(Ext?.LastTiming ?? 0f, Main.Settings?.HitOffsetDecimal ?? 5);

    /// <summary>FPS。内置 FPS 文本关闭时 UpdateFPS 不会被调用（Main.OnUpdate 有 ShowFPS 门控），
    /// 此时按本次渲染的帧时长现算，保证模板里的 {Fps} 不会恒为 0。</summary>
    [Tag("Fps", TagState.None)]
    internal static double Fps()
    {
        float cached = Ext?.LastFps ?? 0f;
        if (cached > 0f) return R(cached);
        float dt = UnityEngine.Time.deltaTime;
        return dt > 0f ? R(1f / dt) : 0;
    }

    // ===== 作者 / 状态 =====

    [Tag("Author", TagState.None)]
    internal static string Author() => Ext?.CurrentAuthor() ?? "";

    /// <summary>状态文本（与 State 文本同源，含 (playing) / (mid start) 后缀）。</summary>
    [Tag("State")]
    internal static string State()
    {
        var mgr = Manager;
        if (mgr == null) return "";
        return mgr.GetStateString(O, mgr.CheckPurePerfect(O), out _);
    }

    /// <summary>当前状态的配色 hex（等待/自动播放/完美游玩…各自的颜色），
    /// 供模板 &lt;color=#{StateColor}&gt; 复刻内置的状态变色。</summary>
    [Tag("StateColor", TagState.None)]
    internal static string StateColor()
    {
        var mgr = Manager;
        if (mgr == null) return "FFFFFF";
        mgr.GetStateString(O, mgr.CheckPurePerfect(O), out var color);
        return ColorUtility.ToHtmlStringRGB(color);
    }

    // ===== 判定时间窗（毫秒）=====

    private static TimingWindowCalculator.Result CurrentWindow()
    {
        var floor = GameRefs.CurrentFloor;
        return floor == null ? default : TimingWindowCalculator.Calculate(floor);
    }

    [Tag("XPerfectMs")]
    internal static double XPerfectMs()
    {
        var tw = CurrentWindow();
        return tw.Valid && tw.XPerfectValid ? R(Math.Round(tw.XPerfectMs)) : 0;
    }

    [Tag("PerfectMs")]
    internal static double PerfectMs()
    {
        var tw = CurrentWindow();
        return tw.Valid ? R(Math.Round(tw.PerfectMs)) : 0;
    }

    [Tag("GreatMs")]
    internal static double GreatMs()
    {
        var tw = CurrentWindow();
        return tw.Valid ? R(Math.Round(tw.GreatMs)) : 0;
    }

    [Tag("GoodMs")]
    internal static double GoodMs()
    {
        var tw = CurrentWindow();
        return tw.Valid ? R(Math.Round(tw.GoodMs)) : 0;
    }
}
