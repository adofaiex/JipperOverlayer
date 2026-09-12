using System;
using System.Text;
using JipperOverlayer.Overlayer.Util;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class OverlayTextManagerNormal : IOverlayTextManager
{
    private static readonly StringBuilder _sb = new(128);
    public float Progress;
    public int CurCheck;
    public int LastCheckpoint = -1;
    public float CurBest = -1;

    public void SetBest(float best) => CurBest = best;

    public void CacheProgress(scrPlanet planet)
    {
        Progress = GameRefs.PercentComplete;
    }

    public void SeedProgress(float progress) => Progress = progress;

    public void UpdateAccuracy(Overlay overlay, int index)
    {
        var s = Main.Settings;
        var labels = s.Labels;
        float xacc = VersionSafe.GetPercentXAcc();
        if (float.IsNaN(xacc)) xacc = 1;
        int seqID = GameRefs.CurrentSeqID;
        int[] hits = overlay.Hit;
        int judged = AccuracyMath.GetJudgedTiles(hits, seqID);
        int remaining = AccuracyMath.GetRemainingTiles(seqID);

        if (s.ShowAccuracy)
        {
            float acc = VersionSafe.GetPercentAcc();
            float maxAcc = 1 + (seqID - overlay.NoCheckStartTile + 1) * 0.0001f;
            float potentialAcc = AccuracyMath.GetPotentialAccuracy(hits, acc, judged, remaining);
            SetDualText(s.AccuracyTextType, overlay.AccuracyText, overlay.Jongyeol?.PotentialAccuracyText, labels.Accuracy,
                Math.Round(acc * 100, s.AccuracyDecimal) + "%", Math.Round(potentialAcc * 100, s.AccuracyDecimal) + "%",
                t => t.color = s.Colors.GetAccuracyColor(xacc == 1 ? 1 : acc / maxAcc, xacc == 1),
                t => t.color = s.Colors.GetAccuracyColor(xacc == 1 ? 1 : potentialAcc / (maxAcc + remaining * 0.0001f), xacc == 1));
        }
        if (s.ShowXAccuracy)
        {
            float potentialXAcc = AccuracyMath.GetPotentialXAccuracy(xacc, judged, remaining);
            SetDualText(s.XAccuracyTextType, overlay.XAccuracyText, overlay.Jongyeol?.PotentialXAccuracyText, labels.XAccuracy,
                Math.Round(xacc * 100, s.XAccuracyDecimal) + "%", Math.Round(potentialXAcc * 100, s.XAccuracyDecimal) + "%",
                t => t.color = s.Colors.GetXAccuracyColor(xacc, xacc == 1),
                t => t.color = s.Colors.GetXAccuracyColor(potentialXAcc, potentialXAcc == 1));
        }
        if (s.ShowXScore && HitMarginCompat.HasNativeXPerfect)
            UpdateXScore(overlay, hits, judged, remaining);
    }

    /// <summary>当前值/潜力值双文本渲染（借鉴 JRP SetDualText）：
    /// Current 只写主文本；Potential 只写潜力文本；Both 两行；BothInOneLine 主文本一行双值。</summary>
    static void SetDualText(PotentialTextType type, TMPro.TextMeshProUGUI text, TMPro.TextMeshProUGUI potentialText,
        string label, string value, string potentialValue, Action<TMPro.TextMeshProUGUI> colorCurrent, Action<TMPro.TextMeshProUGUI> colorPotential)
    {
        if (type != PotentialTextType.Potential && text)
        {
            text.text = "<color=white>" + label + " |</color> " +
                        (type == PotentialTextType.BothInOneLine ? value + " (" + potentialValue + ")" : value);
            colorCurrent(text);
        }
        if (type is not (PotentialTextType.Potential or PotentialTextType.Both) || !potentialText) return;
        potentialText.text = "<color=white>P." + label + " |</color> " + potentialValue;
        colorPotential(potentialText);
    }

    static void UpdateXScore(Overlay overlay, int[] hits, int judged, int remaining)
    {
        var s = Main.Settings;
        int xScore = AccuracyMath.GetXScore(hits);
        int maxXScore = judged * AccuracyMath.XPerfectValue;
        int potentialXScore = xScore + remaining * AccuracyMath.XPerfectValue;
        int totalXScore = maxXScore + remaining * AccuracyMath.XPerfectValue;
        SetDualText(s.XScorePotentialType, overlay.Jongyeol?.XScoreText, overlay.Jongyeol?.PotentialXScoreText, "XScore",
            AccuracyMath.GetXScoreText(xScore, maxXScore, s.XScoreTextType),
            AccuracyMath.GetXScoreText(potentialXScore, totalXScore, s.XScoreTextType),
            t => t.color = s.Colors.XScore.GetColor(maxXScore == 0 ? 1 : (float)xScore / maxXScore),
            t => t.color = s.Colors.XScore.GetColor(totalXScore == 0 ? 1 : (float)potentialXScore / totalXScore));
    }

    public void UpdateProgress(Overlay overlay)
    {
        var labels = Main.Settings.Labels;
        // 详细进度（当前/总数 [-剩余]）为独立样式开关，原属 Jongyeol 模式
        if (Main.Settings.DetailedProgress)
        {
            int cur = GameRefs.CurrentSeqID;
            var floors = GameRefs.LevelMaker?.listFloors;
            int last = floors != null && floors.Count > 0 ? floors.Count - 1 : 0;
            overlay.ProgressText.text = $"<color=white>{labels.Progress} |</color> {cur} / {last}{(cur == last ? "" : $" [-{last - cur}]")} ({Math.Round(Progress * 100, Main.Settings.ProgressDecimal)}%)";
            overlay.ProgressText.color = Main.Settings.Colors.GetProgressColor(Progress);
        }
        else
        {
            var colors = Main.Settings.Colors;
            _sb.Clear();
            _sb.Append("<color=white>");
            _sb.Append(labels.Progress);
            _sb.Append(" |</color> ");
            if (overlay.StartTile > 0)
            {
                _sb.Append("<color=#");
                _sb.Append(colors.GetProgressHex(overlay.StartProgress, true));
                _sb.Append(">");
                _sb.Append(Math.Round(overlay.StartProgress * 100, Main.Settings.ProgressDecimal));
                _sb.Append("%</color> ~ ");
            }
            _sb.Append("<color=#");
            _sb.Append(colors.GetProgressHex(Progress, true));
            _sb.Append(">");
            _sb.Append(Math.Round(Progress * 100, Main.Settings.ProgressDecimal));
            _sb.Append("%</color>");
            overlay.ProgressText.SetText(_sb);
            overlay.ProgressText.color = Color.white;
        }
    }

    public void UpdateProgressBar(Overlay overlay)
    {
        var bar = overlay.ProgressBar;
        bar.LineTransform.SizeDeltaX(Progress * 638);
        bar.BackgroundImage.color = Main.Settings.Colors.GetProgressBarBackgroundColor(Progress);
        bar.LineImage.color = Main.Settings.Colors.GetProgressBarColor(Progress);
        bar.BorderImage.color = Main.Settings.Colors.GetProgressBarBorderColor(Progress);
    }

    public void UpdateCheckpoint(Overlay overlay)
    {
        bool updated = false;
        while (overlay.Checkpoints.Length > CurCheck && GameRefs.CurrentSeqID >= overlay.Checkpoints[CurCheck])
        {
            CurCheck++; updated = true;
        }
        if (LastCheckpoint == GameRefs.CheckpointsUsed && !updated) return;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(Main.Settings.Labels.Checkpoint);
        _sb.Append(" |</color> ");
        _sb.Append(GameRefs.CheckpointsUsed);
        _sb.Append(" (");
        _sb.Append(CurCheck);
        _sb.Append('/');
        _sb.Append(overlay.Checkpoints.Length);
        _sb.Append(')');
        overlay.CheckpointText.SetText(_sb);
        LastCheckpoint = GameRefs.CheckpointsUsed;
    }

    public void UpdateBest(Overlay overlay)
    {
        if (GameRefs.IsAuto && !overlay.AutoOnceEnabled) overlay.AutoOnceEnabled = true;
        if (CurBest == -1)
            CurBest = PlayCount.GetData(overlay.LastHash)?.GetBest(overlay.StartProgress, overlay.LastMultiplier) ?? 0;
        else if (CurBest > Progress || overlay.AutoOnceEnabled) return;
        UpdateBestText(overlay);
    }

    public float GetProgress() => Progress;

    public void UpdateBestText(Overlay overlay)
    {
        float best = CurBest > Progress || overlay.AutoOnceEnabled ? CurBest : Progress;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(Main.Settings.Labels.Best);
        _sb.Append(" |</color> ");
        _sb.Append(Math.Round(best * 100, Main.Settings.BestDecimal));
        _sb.Append('%');
        overlay.BestText.SetText(_sb);
        overlay.BestText.color = Main.Settings.Colors.GetBestColor(best);
    }

    // ===== Jongyeol-mode helpers (single-player) =====

    private int _deathCount;
    private int _lastDeath = -1;

    public void UpdateDeath(Overlay overlay)
    {
        var s = Main.Settings;
        if (!s.ShowDeath || !overlay.GameObject.activeSelf || overlay.DeathText == null) return;
        int[] hits = overlay.Hit;
        // 死亡数 = FailMiss + FailOverload（r150 下标为 10/11，不能写死 8/9）
        if (_lastDeath != (_deathCount = HitMarginCompat.Get(hits, HitMarginCompat.FailMiss) + HitMarginCompat.Get(hits, HitMarginCompat.FailOverload)))
        {
            _sb.Clear();
            _sb.Append("<color=white>");
            _sb.Append(s.Labels.Death);
            _sb.Append(" |</color> ");
            _sb.Append(_deathCount);
            overlay.DeathText.SetText(_sb);
            _lastDeath = _deathCount;
        }
        float max = (GameRefs.CurrentSeqID - overlay.StartTile) * 0.05f;
        if (max < 0.001f) return;
        overlay.DeathText.color = s.Colors.JDeath.GetColor(1 - Math.Min(_deathCount, max) / max);
    }

    public void UpdateState(Overlay overlay, bool isPurePerfect)
    {
        var s = Main.Settings;
        if (!s.ShowState || !overlay.GameObject.activeSelf || overlay.StateText == null) return;
        var sColors = s.Colors;
        var labels = s.Labels;
        string state;
        overlay.StateText.color = sColors.JStateWaiting;
        if (GameRefs.CurrentSeqID == overlay.StartTile) state = labels.StateWaiting;
        else if (GameRefs.CurrentFloor?.nextfloor is { auto: true })
        {
            state = labels.StateAutoTile;
            overlay.StateText.color = sColors.JStateAutoTile;
        }
        else if (GameRefs.IsAuto) { state = labels.StateAuto; overlay.StateText.color = sColors.JStateAuto; }
        else if (isPurePerfect) { state = labels.StatePerfectPlay; overlay.StateText.color = sColors.JStatePerfectPlay; }
        else
        {
            int[] hits = overlay.Hit;
            if (_deathCount != 0) { state = labels.StateComplete; overlay.StateText.color = sColors.JStateComplete; }
            else if (HitMarginCompat.Get(hits, HitMarginCompat.TooEarly) != 0) { state = labels.StateClear; overlay.StateText.color = sColors.JStateClear; }
            else if (HitMarginCompat.Get(hits, HitMarginCompat.VeryEarly) != 0 || HitMarginCompat.Get(hits, HitMarginCompat.VeryLate) != 0) { state = labels.StateNoMiss; overlay.StateText.color = sColors.JStateNoMiss; }
            else { state = labels.StatePerfectionist; overlay.StateText.color = sColors.JStatePerfectionist; }
        }
        var allFloors = GameRefs.LevelMaker?.listFloors;
        if (allFloors != null && GameRefs.CurrentSeqID != allFloors.Count) state += labels.StateSuffix;
        if (overlay.StartTile != 0) state += labels.StateMidStart;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(labels.State);
        _sb.Append(" |</color> ");
        _sb.Append(state);
        overlay.StateText.SetText(_sb);
    }

    public bool CheckPurePerfect(Overlay overlay) => HitMarginCompat.IsPurePerfect(overlay.Hit);

    // “Too” 判定数 = TooEarly + TooLate（r150 下标为 0/8，不能写死 0/6）
    public int GetTooJudgement(Overlay overlay) =>
        HitMarginCompat.Get(overlay.Hit, HitMarginCompat.TooEarly) + HitMarginCompat.Get(overlay.Hit, HitMarginCompat.TooLate);

    public void DirtyTextCaches()
    {
        _lastDeath = -1;
        LastCheckpoint = -1;
        CurBest = -1;   // 触发从 PlayCount 重载并在下次 UpdateBest 时无条件重绘
    }
}