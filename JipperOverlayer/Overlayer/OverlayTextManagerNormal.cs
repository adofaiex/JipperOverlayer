using System;
using System.Text;
using JipperOverlayer.Overlayer.Tags;
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
            SetDualText(s.AccuracyTextType, overlay.AccuracyText, overlay.ExtendedOverlay?.PotentialAccuracyText, labels.Accuracy, labels.AccuracySeparator,
                Math.Round(acc * 100, s.AccuracyDecimal) + "%", Math.Round(potentialAcc * 100, s.AccuracyDecimal) + "%",
                t => t.color = s.Colors.GetAccuracyColor(xacc == 1 ? 1 : acc / maxAcc, xacc == 1),
                t => t.color = s.Colors.GetAccuracyColor(xacc == 1 ? 1 : potentialAcc / (maxAcc + remaining * 0.0001f), xacc == 1),
                labels.AccuracyTemplate, labels.PotAccuracyTemplate);
        }
        if (s.ShowXAccuracy)
        {
            float potentialXAcc = AccuracyMath.GetPotentialXAccuracy(xacc, judged, remaining);
            SetDualText(s.XAccuracyTextType, overlay.XAccuracyText, overlay.ExtendedOverlay?.PotentialXAccuracyText, labels.XAccuracy, labels.XAccuracySeparator,
                Math.Round(xacc * 100, s.XAccuracyDecimal) + "%", Math.Round(potentialXAcc * 100, s.XAccuracyDecimal) + "%",
                t => t.color = s.Colors.GetXAccuracyColor(xacc, xacc == 1),
                t => t.color = s.Colors.GetXAccuracyColor(potentialXAcc, potentialXAcc == 1),
                labels.XAccuracyTemplate, labels.PotXAccuracyTemplate);
        }
        if (s.ShowXScore && HitMarginCompat.HasNativeXPerfect)
            UpdateXScore(overlay, hits, judged, remaining);
    }

    /// <summary>当前值/潜力值双文本渲染：
    /// Current 只写主文本；Potential 只写潜力文本；Both 两行；BothInOneLine 主文本一行双值。
    /// separator = 标签与数值之间的分隔符（LabelConfig 按文本项独立配置）。</summary>
    static void SetDualText(PotentialTextType type, TMPro.TextMeshProUGUI text, TMPro.TextMeshProUGUI potentialText,
        string label, string separator, string value, string potentialValue, Action<TMPro.TextMeshProUGUI> colorCurrent, Action<TMPro.TextMeshProUGUI> colorPotential,
        string template = null, string potentialTemplate = null)
    {
        if (type != PotentialTextType.Potential && text && !TemplateEngine.TryApply(template, text))
        {
            text.text = "<color=white>" + label + separator + "</color>" +
                        (type == PotentialTextType.BothInOneLine ? value + " (" + potentialValue + ")" : value);
            colorCurrent(text);
        }
        if (type is not (PotentialTextType.Potential or PotentialTextType.Both) || !potentialText) return;
        if (TemplateEngine.TryApply(potentialTemplate, potentialText)) return;
        potentialText.text = "<color=white>P." + label + separator + "</color>" + potentialValue;
        colorPotential(potentialText);
    }

    static void UpdateXScore(Overlay overlay, int[] hits, int judged, int remaining)
    {
        var s = Main.Settings;
        var trackers = scrMistakesManager.marginTrackers;
        bool native = AccuracyMath.TryGetNativeXScore(trackers is { Length: > 0 } ? trackers[0] : null,
            out int x, out int mapMax);
        int soFarMax, potentialX, potentialTotal;
        if (native)
        {
            // JRP 口径：分母 = 已判定计分格数 × 2，剩余格只用于潜力值。
            // 这样命中瞬间的位置滞后（currentSeqID 落后一格）不会再让 MAX−n 少 2 分，
            // 结算界面与原生 DetailedResults（maxXScore − xScore）完全一致。
            AccuracyMath.GetXScoreValues(hits, x, mapMax, out soFarMax, out potentialX);
            potentialTotal = mapMax;
        }
        else
        {
            x = AccuracyMath.GetXScore(hits);
            soFarMax = judged * AccuracyMath.XPerfectValue;
            potentialTotal = soFarMax + remaining * AccuracyMath.XPerfectValue;
            potentialX = x + remaining * AccuracyMath.XPerfectValue;
        }
        SetDualText(s.XScorePotentialType, overlay.ExtendedOverlay?.XScoreText, overlay.ExtendedOverlay?.PotentialXScoreText, "XScore", s.Labels.XScoreSeparator,
            AccuracyMath.GetXScoreText(x, soFarMax, s.XScoreTextType),
            AccuracyMath.GetXScoreText(potentialX, potentialTotal, s.XScoreTextType, potential: true),
            t => t.color = s.Colors.GetXScoreColor(soFarMax == 0 ? 1 : (float)x / soFarMax),
            t => t.color = s.Colors.GetXScoreColor(potentialTotal == 0 ? 1 : (float)potentialX / potentialTotal),
            s.Labels.XScoreTemplate, s.Labels.PotXScoreTemplate);
    }

    public void UpdateProgress(Overlay overlay)
    {
        var labels = Main.Settings.Labels;
        // 模板接管：整行内容（含标签与颜色）由模板决定，内置渲染整段跳过。
        if (TemplateEngine.TryApply(labels.ProgressTemplate, overlay.ProgressText)) return;
        // 详细进度（当前/总数 [-剩余]）为独立样式开关，原属 扩展叠加层
        if (Main.Settings.DetailedProgress)
        {
            int cur = GameRefs.CurrentSeqID;
            var floors = GameRefs.LevelMaker?.listFloors;
            int last = floors != null && floors.Count > 0 ? floors.Count - 1 : 0;
            overlay.ProgressText.text = $"<color=white>{labels.Progress}{labels.ProgressSeparator}</color>{cur} / {last}{(cur == last ? "" : $" [-{last - cur}]")} ({Math.Round(Progress * 100, Main.Settings.ProgressDecimal)}%)";
            overlay.ProgressText.color = Main.Settings.Colors.GetProgressColor(Progress);
        }
        else
        {
            var colors = Main.Settings.Colors;
            _sb.Clear();
            _sb.Append("<color=white>");
            _sb.Append(labels.Progress);
            _sb.Append(labels.ProgressSeparator);
            _sb.Append("</color>");
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
        if (TemplateEngine.TryApply(Main.Settings.Labels.CheckpointTemplate, overlay.CheckpointText))
        {
            LastCheckpoint = GameRefs.CheckpointsUsed;
            return;
        }
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(Main.Settings.Labels.Checkpoint);
        _sb.Append(Main.Settings.Labels.CheckpointSeparator);
        _sb.Append("</color>");
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
        if (TemplateEngine.TryApply(Main.Settings.Labels.BestTemplate, overlay.BestText)) return;
        float best = CurBest > Progress || overlay.AutoOnceEnabled ? CurBest : Progress;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(Main.Settings.Labels.Best);
        _sb.Append(Main.Settings.Labels.BestSeparator);
        _sb.Append("</color>");
        _sb.Append(Math.Round(best * 100, Main.Settings.BestDecimal));
        _sb.Append('%');
        overlay.BestText.SetText(_sb);
        overlay.BestText.color = Main.Settings.Colors.GetBestColor(best);
    }

    // ===== Extended-overlay helpers (single-player) =====

    private int _deathCount;
    private int _lastDeath = -1;

    public void UpdateDeath(Overlay overlay)
    {
        var s = Main.Settings;
        int[] hits = overlay.Hit;
        // 死亡数不仅驱动死亡文本，也驱动单人 State 的 Completed/Clear 分支；
        // 因此即使文本隐藏、overlay 尚未激活，也要先更新内部计数。
        _deathCount = HitMarginCompat.Get(hits, HitMarginCompat.FailMiss)
                    + HitMarginCompat.Get(hits, HitMarginCompat.FailOverload);
        if (!s.ShowDeath || !overlay.GameObject.activeSelf || overlay.DeathText == null)
        {
            // 重新打开开关时强制重绘一次，避免沿用隐藏前的旧文本。
            _lastDeath = -1;
            return;
        }
        if (_lastDeath != _deathCount)
        {
            if (!TemplateEngine.TryApply(s.Labels.DeathTemplate, overlay.DeathText))
            {
                _sb.Clear();
                _sb.Append("<color=white>");
                _sb.Append(s.Labels.Death);
                _sb.Append(s.Labels.DeathSeparator);
                _sb.Append("</color>");
                _sb.Append(_deathCount);
                overlay.DeathText.SetText(_sb);
            }
            _lastDeath = _deathCount;
        }
        // 模板接管时底色必须保持白，否则外层的颜色渐变会把整行（含标签）一起染色。
        if (!string.IsNullOrEmpty(s.Labels.DeathTemplate)) return;
        float max = (GameRefs.CurrentSeqID - overlay.StartTile) * 0.05f;
        if (max < 0.001f) return;
        overlay.DeathText.color = s.Colors.JDeath.GetColor(1 - Math.Min(_deathCount, max) / max);
    }

    /// <summary>状态文本（不含标签前缀）：UpdateState 与 {State} tag 共用这一份判定分支，
    /// 保证模板里读到的状态与内置 State 行永远一致。</summary>
    public string GetStateString(Overlay overlay, bool isPurePerfect, out Color color)
    {
        var sColors = Main.Settings.Colors;
        var labels = Main.Settings.Labels;
        string state;
        color = sColors.JStateWaiting;
        if (GameRefs.CurrentSeqID == overlay.StartTile) state = labels.StateWaiting;
        else if (GameRefs.CurrentFloor?.nextfloor is { auto: true })
        {
            state = labels.StateAutoTile;
            color = sColors.JStateAutoTile;
        }
        else if (GameRefs.IsAuto) { state = labels.StateAuto; color = sColors.JStateAuto; }
        else if (isPurePerfect) { state = labels.StatePerfectPlay; color = sColors.JStatePerfectPlay; }
        else
        {
            int[] hits = overlay.Hit;
            // 不依赖 UpdateDeath 的文本门控/上一帧缓存：State 必须按本次判定数组判断。
            int death = HitMarginCompat.Get(hits, HitMarginCompat.FailMiss)
                      + HitMarginCompat.Get(hits, HitMarginCompat.FailOverload);
            if (death != 0) { state = labels.StateComplete; color = sColors.JStateComplete; }
            else if (HitMarginCompat.Get(hits, HitMarginCompat.TooEarly) != 0) { state = labels.StateClear; color = sColors.JStateClear; }
            else if (HitMarginCompat.Get(hits, HitMarginCompat.VeryEarly) != 0 || HitMarginCompat.Get(hits, HitMarginCompat.VeryLate) != 0) { state = labels.StateNoMiss; color = sColors.JStateNoMiss; }
            else { state = labels.StatePerfectionist; color = sColors.JStatePerfectionist; }
        }
        var allFloors = GameRefs.LevelMaker?.listFloors;
        if (allFloors != null && GameRefs.CurrentSeqID != allFloors.Count) state += labels.StateSuffix;
        if (overlay.StartTile != 0) state += labels.StateMidStart;
        return state;
    }

    public void UpdateState(Overlay overlay, bool isPurePerfect)
    {
        var s = Main.Settings;
        if (!s.ShowState || !overlay.GameObject.activeSelf || overlay.StateText == null) return;
        if (TemplateEngine.TryApply(s.Labels.StateTemplate, overlay.StateText)) return;
        var labels = s.Labels;
        string state = GetStateString(overlay, isPurePerfect, out var stateColor);
        overlay.StateText.color = stateColor;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(labels.State);
        _sb.Append(labels.StateSeparator);
        _sb.Append("</color>");
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
