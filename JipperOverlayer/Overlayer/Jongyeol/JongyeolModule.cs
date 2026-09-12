using System;
using System.Collections.Generic;
using JipperOverlayer.Overlayer.Util;
using TMPro;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Jongyeol;

// 原「Jongyeol 模式」的承载模块。总开关移除后它常驻运行，负责：
//   1. 六个扩展文本（FPS/Author/State/Death/Start/Timing）的创建、布局与更新
//   2. 统一的栈式布局（全部 18 个可栈排元素，顺序见 Settings.JongyeolDisplayOrder）
//   3. BPM（含伪 BPM 检测）与时间文本的统一更新实现
// 各功能由独立设置开关控制，可与普通模式任意搭配。
public class JongyeolModule
{
    private readonly Overlay _overlay;

    public TextMeshProUGUI FPSText, AuthorText, StateText, DeathText, StartText, TimingText;
    // V1.1.6 新增：Timing 平均行、XScore、三个潜力值文本（借鉴 JipperResourcePack）
    public TextMeshProUGUI AvgTimingText, XScoreText, PotentialAccuracyText, PotentialXAccuracyText, PotentialXScoreText;
    public readonly List<TextMeshProUGUI> ExtraTexts = new();

    private List<float> _timings;
    private bool _purePerfect;
    private int _pseudoFloor = -1;
    private float _lastCurKps = -1, _fpsTime, _timingsSum;
    private bool _perToCom;
    private int _lastMusicTimeTick = -1;
    private float _lastTiming;
    public int DecimalPrecision = 2;

    /// <summary>Jongyeol 模式下连击标题是否已切换为备用文本（非完美命中后）。</summary>
    public bool IsAltComboTitle => _perToCom;

    public JongyeolModule(Overlay overlay)
    {
        _overlay = overlay;
    }

    public void InitializeExtraTexts()
    {
        _overlay.SetupMainText("FPS", ref FPSText);
        _overlay.SetupMainText("Author", ref AuthorText);
        _overlay.SetupMainText("State", ref StateText);
        _overlay.SetupMainText("Death", ref DeathText);
        _overlay.SetupMainText("Start", ref StartText);
        _overlay.SetupMainText("Timing", ref TimingText);
        _overlay.SetupMainText("AvgTiming", ref AvgTimingText);
        _overlay.SetupMainText("XScore", ref XScoreText);
        _overlay.SetupMainText("PotAccuracy", ref PotentialAccuracyText);
        _overlay.SetupMainText("PotXAccuracy", ref PotentialXAccuracyText);
        _overlay.SetupMainText("PotXScore", ref PotentialXScoreText);
        _overlay.DeathText = DeathText;
        _overlay.StateText = StateText;
        ExtraTexts.AddRange([FPSText, AuthorText, StateText, DeathText, StartText, TimingText,
            AvgTimingText, XScoreText, PotentialAccuracyText, PotentialXAccuracyText, PotentialXScoreText]);
    }

    // ===== SetupLocation — replaces Overlay.SetupLocationMain when active =====

    public void SetupLocation()
    {
        if (!FPSText) return;
        int y = -15;
        var s = Main.Settings;
        bool checkAuto = !s.RemoveNotRequireInAuto || !GameRefs.IsAuto;

        _overlay.Checkpoints ??= Overlay.CollectCheckpoints();

        foreach (int elemId in s.JongyeolDisplayOrder)
        {
            var elem = (DisplayElement)elemId;
            var text = GetJongyeolStackText(elem);
            if (text == null) continue;
            bool enabled = IsJongyeolElementEnabled(elem, s, checkAuto);
            SetupText(text, enabled, ref y);
        }

        _overlay.UpdateProgress();
        VersionSafe.CalculatePercentAcc();
        _overlay.UpdateTime();
        UpdateAuthor();
        UpdateState();
        UpdateDeath();
        if (_timings != null) return;
        _timings = [];
        UpdateTiming(0);
        _timings.Clear();
        _timingsSum = 0;
    }

    TextMeshProUGUI GetJongyeolStackText(DisplayElement elem) => elem switch
    {
        DisplayElement.FPS => FPSText,
        DisplayElement.Author => AuthorText,
        DisplayElement.Progress => _overlay.ProgressText,
        DisplayElement.Accuracy => _overlay.AccuracyText,
        DisplayElement.XAccuracy => _overlay.XAccuracyText,
        DisplayElement.MusicTime => _overlay.TimeText,
        DisplayElement.MapTime => _overlay.MapTimeText,
        DisplayElement.Checkpoint => _overlay.CheckpointText,
        DisplayElement.Best => _overlay.BestText,
        DisplayElement.State => StateText,
        DisplayElement.Death => DeathText,
        DisplayElement.Start => StartText,
        DisplayElement.Timing => TimingText,
        DisplayElement.AvgTiming => AvgTimingText,
        DisplayElement.XScore => XScoreText,
        DisplayElement.PotentialAccuracy => PotentialAccuracyText,
        DisplayElement.PotentialXAccuracy => PotentialXAccuracyText,
        DisplayElement.PotentialXScore => PotentialXScoreText,
        _ => null,
    };

    bool IsJongyeolElementEnabled(DisplayElement elem, Settings s, bool checkAuto)
    {
        if (elem == DisplayElement.Author)
        {
            var ld = scnGame.instance?.levelData;
            return !string.IsNullOrEmpty(ld?.GetType().GetProperty("author")?.GetValue(ld) as string) && s.ShowAuthor;
        }
        if (elem == DisplayElement.Death) return GameRefs.IsNoFail && s.ShowDeath;
        if (elem == DisplayElement.Start) return _overlay.StartTile != 0 && s.ShowStart;
        if (elem == DisplayElement.Checkpoint) return checkAuto && s.ShowCheckpoint && (_overlay.Checkpoints?.Length > 0);
        return elem switch
        {
            DisplayElement.FPS => s.ShowFPS,
            DisplayElement.Progress => s.ShowProgress,
            DisplayElement.Accuracy => checkAuto && s.ShowAccuracy,
            DisplayElement.XAccuracy => checkAuto && s.ShowXAccuracy,
            DisplayElement.MusicTime => s.ShowMusicTime,
            DisplayElement.MapTime => s.ShowMapTime,
            DisplayElement.Best => checkAuto && s.ShowBest,
            DisplayElement.State => s.ShowState,
            DisplayElement.Timing => checkAuto && s.ShowTiming,
            // AvgTiming 行仅在 Both 模式（两行分开）时有意义
            DisplayElement.AvgTiming => checkAuto && s.ShowTiming && s.TimingTextType == TimingTextType.Both,
            // XScore 与潜力值文本的可见性跟随所属功能与其文本类型
            DisplayElement.XScore => checkAuto && s.ShowXScore && HitMarginCompat.HasNativeXPerfect,
            // 潜力值独立文本仅单人模式（coop 为合并文本，内联显示潜力值）
            DisplayElement.PotentialAccuracy => checkAuto && !VersionSafe.IsCoopMode() && s.ShowAccuracy && s.AccuracyTextType is PotentialTextType.Potential or PotentialTextType.Both,
            DisplayElement.PotentialXAccuracy => checkAuto && !VersionSafe.IsCoopMode() && s.ShowXAccuracy && s.XAccuracyTextType is PotentialTextType.Potential or PotentialTextType.Both,
            DisplayElement.PotentialXScore => checkAuto && !VersionSafe.IsCoopMode() && s.ShowXScore && HitMarginCompat.HasNativeXPerfect && s.XScorePotentialType is PotentialTextType.Potential or PotentialTextType.Both,
            _ => false,
        };
    }

    private static void SetupText(TextMeshProUGUI text, bool enabled, ref int y)
    {
        if (text == null) return;
        text.enabled = enabled;
        if (!enabled) return;
        text.rectTransform.anchoredPosition = new Vector2(228, y);
        y -= 35;
    }

    // ===== Extra Update Methods =====

    public void UpdateFPS(float deltaTime)
    {
        var s = Main.Settings;
        if (!s.ShowFPS || !_overlay.GameObject.activeSelf || (_fpsTime += deltaTime) < s.FPSRefreshRate) return;
        _fpsTime %= s.FPSRefreshRate;
        Overlay._textSb.Clear();
        Overlay._textSb.Append("<color=white>");
        Overlay._textSb.Append(s.Labels.FPS);
        Overlay._textSb.Append(" |</color> ");
        Overlay._textSb.Append((1f / deltaTime).ToString($"F{DecimalPrecision}"));
        FPSText.text = Overlay._textSb.ToString();
        FPSText.color = s.Colors.JFps;
    }

    public void UpdateAuthor()
    {
        var s = Main.Settings;
        if (!s.ShowAuthor || !_overlay.GameObject.activeSelf) return;
        var ld = scnGame.instance?.levelData;
        string author = ld?.GetType().GetProperty("author")?.GetValue(ld) as string ?? "";
        AuthorText.text = $"<color=white>{s.Labels.Author} |</color> {author}";
    }

    public void UpdateColors()
    {
        if (!_overlay.GameObject.activeSelf) return;
        var c = Main.Settings.Colors;
        if (FPSText) FPSText.color = c.JFps;
        if (AuthorText) AuthorText.color = c.JAuthor;
        if (StartText) StartText.color = c.JStart;
    }

    public void UpdateState()
    {
        _overlay.OverlayTextManager?.UpdateState(_overlay, _purePerfect);
    }

    public void CheckPurePerfect()
    {
        _purePerfect = _overlay.OverlayTextManager?.CheckPurePerfect(_overlay) ?? true;
    }

    public void UpdateDeath()
    {
        _overlay.OverlayTextManager?.UpdateDeath(_overlay);
    }

    public void UpdateStart()
    {
        var s = Main.Settings;
        if (!s.ShowStart || !_overlay.GameObject.activeSelf || _overlay.StartTile != GameRefs.CurrentSeqID) return;
        StartText.text = $"<color=white>{s.Labels.Start} |</color> {_overlay.StartTile} ({Math.Round(_overlay.OverlayTextManager.GetProgress() * 100, DecimalPrecision)}%)";
        StartText.color = s.Colors.JStart;
    }

    public void UpdateTiming(float timing)
    {
        var s = Main.Settings;
        if (!s.ShowTiming || !_overlay.GameObject.activeSelf) return;
        // 自动打击过滤（借鉴 JRP IsTimingAvailable）：自动播放/自动地板的命中使用理想时间戳
        // （≈0ms 假样本），混入统计会把平均值拉向 0
        if (GameRefs.IsAuto) return;
        if (GameRefs.CurrentFloor?.nextfloor is { auto: true }) return;
        // 自愈：补丁无条件挂载后，首次命中可能早于 SetupLocation 的初始化
        _timings ??= [];
        if (_timings.Count >= 5000)
        {
            for (int i = 0; i < 1000; i++) _timingsSum -= _timings[i];
            _timings.RemoveRange(0, 1000);
        }
        _timings.Add(timing);
        _timingsSum += timing;
        _lastTiming = timing;
        RenderTiming(timing);
    }

    private void RenderTiming(float timing)
    {
        int dp = Main.Settings.TimingDecimal;
        float average = _timings == null || _timings.Count == 0 ? 0 : (float)(_timingsSum / _timings.Count);
        var labels = Main.Settings.Labels;
        var colors = Main.Settings.Colors;
        switch (Main.Settings.TimingTextType)
        {
            case TimingTextType.Timing:
                TimingText.text = $"<color=white>{labels.Timing} |</color> {Math.Round(timing, dp)}";
                TimingText.color = colors.JTiming.GetColor(1 - Math.Min(Math.Abs(timing), 150) / 150);
                break;
            case TimingTextType.AvgTiming:
                if (AvgTimingText)
                {
                    AvgTimingText.text = $"<color=white>{labels.Timing}(Avg) |</color> {Math.Round(average, dp)}";
                    AvgTimingText.color = colors.JTiming.GetColor(1 - Math.Min(Math.Abs(average), 150) / 150);
                }
                break;
            case TimingTextType.Both:
                TimingText.text = $"<color=white>{labels.Timing} |</color> {Math.Round(timing, dp)}";
                TimingText.color = colors.JTiming.GetColor(1 - Math.Min(Math.Abs(timing), 150) / 150);
                if (AvgTimingText)
                {
                    AvgTimingText.text = $"<color=white>{labels.Timing}(Avg) |</color> {Math.Round(average, dp)}";
                    AvgTimingText.color = colors.JTiming.GetColor(1 - Math.Min(Math.Abs(average), 150) / 150);
                }
                break;
            default: // BothInOneLine：一行双值（原行为）
                TimingText.text = $"<color=white>{labels.Timing} |</color> {Math.Round(timing, dp)} ({Math.Round(average, dp)})";
                TimingText.color = colors.JTiming.GetColor(1 - Math.Min(Math.Abs(timing), 150) / 150);
                break;
        }
    }

    // 标签编辑后重绘 Timing：用缓存的最近一次采样，不向统计里追加新样本。
    internal void RefreshTiming()
    {
        if (!Main.Settings.ShowTiming || !_overlay.GameObject.activeSelf) return;
        if (_timings == null || _timings.Count == 0 || !TimingText) return;
        RenderTiming(_lastTiming);
    }

    internal void DirtyTextCaches()
    {
        _lastMusicTimeTick = -1;
        _pseudoFloor = -1;
    }

    // ===== Overridden Update Methods =====

    // 时间格式跟随独立开关：TimeDecimals 决定是否带一位小数（原 Jongyeol 模式行为）
    private static string FmtTime(float time, bool hour)
        => Main.Settings.TimeDecimals
            ? TimeFormatter.FormatWithDecimals(time, hour)
            : TimeFormatter.Format(time, hour);

    public void UpdateTime()
    {
        if (!_overlay.GameObject.activeSelf || _overlay.IsDeath) return;
        bool requireMusicToMap = false;
        var s = Main.Settings;

        // --- Music Time ---
        if (s.ShowMusicTime)
        {
            var song = GameRefs.Song;
            if (song is not AudioSource audioSrc) requireMusicToMap = true;
            else if (audioSrc.clip == null && s.ShowMapTimeIfNotMusic) requireMusicToMap = true;
            else
            {
                float time = audioSrc.time;
                if (time < 0) time = 0;
                var clip = audioSrc.clip;
                float totalTime = clip != null && clip.length > 0 ? clip.length : 0;

                // Fallback: when song has no clip, use map total time
                if (totalTime <= 0)
                {
                    var floors = GameRefs.LevelMaker?.listFloors;
                    if (floors != null && floors.Count > 0)
                        totalTime = (float)floors[floors.Count - 1].entryTime;
                }

                // Throttle: update at ~10fps to avoid per-frame song.time access
                int curTick = (int)(time * 10);
                if (_lastMusicTimeTick == curTick) return;
                _lastMusicTimeTick = curTick;

                bool hourNeed = totalTime >= 3600;
                _overlay.MusicTimeCache ??= FmtTime(totalTime, hourNeed);

                if (time > 0) _overlay.SongPlaying = true;
                else if (time == 0 && _overlay.SongPlaying) time = totalTime;

                string timeStr = time == 0 && _overlay.SongPlaying
                    ? _overlay.MusicTimeCache
                    : FmtTime(time, hourNeed);

                _overlay.TimeText.text = $"{_overlay._musicTimeLabel} {timeStr}~{_overlay.MusicTimeCache}";
                _overlay.TimeText.color = totalTime > 0 ? s.Colors.GetMusicTimeColor(time / totalTime) : Color.white;
            }
        }

        // --- Map Time (with music-time fallback when song has no clip) ---
        if (s.ShowMapTime || requireMusicToMap)
        {
            float time = (float)(GameRefs.ConductorAddoffset + GameRefs.ConductorSongpositionMinusi);
            var floors = GameRefs.LevelMaker?.listFloors;
            if (floors == null || floors.Count == 0) return;
            float totalTime = (float)floors[floors.Count - 1].entryTime;
            if (time < 0) time = 0;
            else if (time > totalTime) time = totalTime;
            if (!s.ShowMapTime && !requireMusicToMap) return;
            bool hourNeed = totalTime >= 3600;
            _overlay.MapTimeCache ??= FmtTime(totalTime, hourNeed);
            string timeStr = time == totalTime ? _overlay.MapTimeCache : FmtTime(time, hourNeed);
            string text = $"{_overlay._mapTimeLabel} {timeStr}~{_overlay.MapTimeCache}";
            if (s.ShowMapTime) { _overlay.MapTimeText.text = text; _overlay.MapTimeText.color = totalTime > 0 ? s.Colors.GetMapTimeColor(time / totalTime) : Color.white; }
            if (requireMusicToMap) { _overlay.TimeText.text = text; _overlay.TimeText.color = totalTime > 0 ? s.Colors.GetMusicTimeColor(time / totalTime) : Color.white; }
        }
    }

    public void UpdateBPM()
    {
        var s = Main.Settings;
        if (!_overlay.GameObject.activeSelf) return;
        scrFloor floor = GameRefs.CurrentFloor ?? GameRefs.FirstFloor;
        if (floor == null || floor.seqID <= _pseudoFloor) return;
        var bpm = BpmCalculator.Calculate(floor, (float)(GameRefs.SongPitch * VersionSafe.GetPlanetSpeed(GameRefs.ControllerInstance)));
        bool checkPseudo = Jbpm.CheckPseudo;
        float cbpm = 0;
        int count = 0;
        bool isPseudo = checkPseudo && CheckPseudo(floor, bpm.TileBpm, out cbpm, out count);
        if (!isPseudo) cbpm = bpm.CurrentBpm;
        float kps = cbpm / 60;
        if (isPseudo) kps *= count;

        // 判定时间窗：并入 BPM 多行文本，与 Overlay.UpdateBPM 一致。
        var tw = s.ShowTimingWindow ? TimingWindowCalculator.Calculate(floor) : default;
        bool twValid = s.ShowTimingWindow && tw.Valid;
        bool twChanged = twValid && _overlay.TimingWindowChanged(tw);

        if (_overlay.LastTileBpm == bpm.TileBpm && _overlay.LastCurBpm == cbpm && Math.Abs(_lastCurKps - kps) < 0.001f && !twChanged) return;
        string colorHex = s.Colors.GetBpmHex(bpm.TileBpm / s.BpmColorMax, true);
        string kpsPrefix = isPseudo ? $"<color=#{s.Colors.GetBpmHex(cbpm * count / s.BpmColorMax, true)}>" : "";
        string kpsSuffix = isPseudo ? "</color>" : "";
        string text = Overlay.BuildBpmText(s.BpmLineOrder, colorHex, s, bpm.TileBpm, cbpm, kps, kpsPrefix, kpsSuffix);
        if (twValid) text += _overlay.BuildTimingWindowLines(tw);
        _overlay.BPMText.text = text;
        if (_overlay.LastCurBpm != cbpm) _overlay.BPMText.color = s.Colors.GetBpmColor(cbpm / s.BpmColorMax);
        _overlay.LastTileBpm = bpm.TileBpm; _overlay.LastCurBpm = cbpm; _lastCurKps = kps;
    }

    public Color UpdateComboColor(int combo)
    {
        if (_purePerfect) return Main.Settings.Colors.JStatePerfectPlay;
        int too = _overlay.OverlayTextManager?.GetTooJudgement(_overlay) ?? 0;
        float value = (float)combo / (GameRefs.CurrentSeqID - _overlay.StartTile + too + 1) * 2;
        if (value > 1) value = 1;
        return Main.Settings.Colors.JCombo.GetColor(value);
    }

    public void OnNonPerfectHit()
    {
        if (_perToCom) return;
        _overlay.ComboTitle.text = Main.Settings.Labels.ComboTitleAlt;
        _perToCom = true;
    }

    // ===== Show / Hide Hooks =====

    public void OnShow(int floor)
    {
        _perToCom = false; _purePerfect = true; _pseudoFloor = -1;
        _timingsSum = 0;
        _lastMusicTimeTick = -1;
        if (GameRefs.CheckpointsUsed == 0) _overlay.ComboTitle.text = Main.Settings.Labels.ComboTitle;
    }

    public void OnHide()
    {
        _timings = null;
        _timingsSum = 0;
    }

    // ===== Pseudo-BPM Detection =====

    private bool CheckPseudo(scrFloor curFloor, float bpm, out float cbpm, out int count)
    {
        if (bpm < 200 || !scnGame.instance) { cbpm = count = 0; return false; }
        double allAngle = 0;
        double maxAngle = bpm < 400 ? 0.5236 : 1.0472;
        scrFloor floor = curFloor;
        int midSpin = 0;
        while (floor.nextfloor != null)
        {
            if (floor.midSpin) { floor = floor.nextfloor; midSpin++; continue; }
            double angle = floor.angleLength;
            allAngle += angle;
            if (allAngle > maxAngle && (bpm < 600 || allAngle - angle > 1e-14 || !Check90(angle)))
            {
                if (angle < maxAngle && Math.Abs(angle - (floor.prevfloor?.angleLength ?? 0)) < 1e-14)
                {
                    float speed = floor.speed;
                    do { floor = floor.nextfloor; }
                    while (floor != null && Math.Abs(floor.angleLength - floor.prevfloor.angleLength) < 1e-14 && Math.Abs(speed - floor.speed) < 1e-14);
                    _pseudoFloor = floor.seqID - 1;
                    cbpm = count = 0; return false;
                }
                break;
            }
            floor = floor.nextfloor;
        }
        if (Check90(curFloor.angleLength))
        {
            int c2 = 0;
            scrFloor f2 = curFloor;
            if (f2.midSpin) f2 = f2.nextfloor;
            while (c2 < 3 && f2 != null && Check90(f2.angleLength)) { c2++; f2 = f2.nextfloor; if (f2?.midSpin == true) f2 = f2.nextfloor; }
            if (c2 < 3)
            {
                f2 = curFloor;
                if (f2.midSpin) f2 = f2.prevfloor;
                while (c2 < 3 && f2 != null && Check90(f2.angleLength)) { c2++; f2 = f2.prevfloor; if (f2?.midSpin == true) f2 = f2.prevfloor; }
            }
            if (c2 >= 3) { cbpm = count = 0; return false; }
        }
        count = floor.seqID - curFloor.seqID + 1 - midSpin;
        if (count <= 1) { cbpm = 0; return false; }
        cbpm = floor.nextfloor != null ? (float)(60 / (floor.nextfloor.entryTime - curFloor.entryTime)) : (float)(60 / (floor.entryTime - curFloor.entryTime + 60 / bpm));
        cbpm *= GameRefs.SongPitch;
        _pseudoFloor = floor.seqID;
        return true;
    }

    private static bool Check90(double angle) => Math.Abs(angle - 1.57079642638564) < 1e-14;
}