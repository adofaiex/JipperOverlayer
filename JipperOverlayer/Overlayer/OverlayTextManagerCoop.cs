using System;
using System.Text;
using JipperOverlayer.Overlayer.Util;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class OverlayTextManagerCoop : IOverlayTextManager
{
    private static readonly StringBuilder _sb = new(256);

    public PlayerData[] PlayerDatas;
    public float MaxProgress;
    public float CurBest = -1;
    public int CurCheck;
    public int LastCheckpoint = -1;
    private string[] _accStrings;
    private string[] _xaccStrings;

    public OverlayTextManagerCoop(Overlay overlay)
    {
        PlayerDatas = new PlayerData[scrPlayerManager.playerCount];
        _accStrings = new string[PlayerDatas.Length + 1];
        _xaccStrings = new string[PlayerDatas.Length + 1];
        if (overlay.ProgressText) overlay.ProgressText.color = Color.white;
        if (overlay.AccuracyText) overlay.AccuracyText.color = Color.white;
        if (overlay.XAccuracyText) overlay.XAccuracyText.color = Color.white;
    }

    public void SetBest(float best) => CurBest = best;

    public void CacheProgress(scrPlanet planet)
    {
        var allFloors = GameRefs.LevelMaker?.listFloors;
        if (allFloors == null) return;
        float count = allFloors.Count;
        if ((object)planet == null)
        {
            for (int i = 0; i < PlayerDatas.Length; i++)
                SetProgress(ref PlayerDatas[i],
                    (scrPlayerManager.instance.allPlayers[i].planetarySystem.chosenPlanet.currfloor.seqID + 1) / count);
        }
        else
        {
            SetProgress(ref PlayerDatas[planet.player.playerID],
                (planet.currfloor.seqID + 1) / count);
        }
    }

    protected void SetProgress(ref PlayerData pData, float progress)
    {
        pData.Progress = progress;
        pData.ProgressString = $" | <color=#{Main.Settings.Colors.GetProgressHex(progress, true)}>{Math.Round(progress * 100, Main.Settings.ProgressDecimal)}%</color>";
        if (MaxProgress < progress) MaxProgress = progress;
    }

    public void SeedProgress(float progress)
    {
        for (int i = 0; i < PlayerDatas.Length; i++)
            SetProgress(ref PlayerDatas[i], progress);
    }

    public void UpdateAccuracy(Overlay overlay, int index)
    {
        var s = Main.Settings;
        if (s.ShowAccuracy)
        {
            if (index == -1)
                for (int i = 0; i < PlayerDatas.Length; i++)
                    SetAccuracy(ref PlayerDatas[i], overlay, i);
            else SetAccuracy(ref PlayerDatas[index], overlay, index);

            _accStrings[0] = Main.Settings.Labels.Accuracy;
            for (int i = 0; i < PlayerDatas.Length; i++) _accStrings[i + 1] = PlayerDatas[i].AccuracyString;
            overlay.AccuracyText.text = string.Concat(_accStrings);
        }
        if (Main.Settings.ShowXAccuracy)
        {
            if (index == -1)
                for (int i = 0; i < PlayerDatas.Length; i++)
                    SetXAccuracy(ref PlayerDatas[i], i);
            else SetXAccuracy(ref PlayerDatas[index], index);

            _xaccStrings[0] = Main.Settings.Labels.XAccuracy;
            for (int i = 0; i < PlayerDatas.Length; i++) _xaccStrings[i + 1] = PlayerDatas[i].XAccuracyString;
            overlay.XAccuracyText.text = string.Concat(_xaccStrings);
        }
        if (s.ShowXScore && HitMarginCompat.HasNativeXPerfect)
        {
            if (index == -1)
                for (int i = 0; i < PlayerDatas.Length; i++)
                    SetXScore(ref PlayerDatas[i], i);
            else SetXScore(ref PlayerDatas[index], index);

            var xs = overlay.Jongyeol?.XScoreText;
            if (xs)
            {
                _sb.Clear();
                _sb.Append("<color=white>XScore |</color>");
                for (int i = 0; i < PlayerDatas.Length; i++) _sb.Append(PlayerDatas[i].XScoreString);
                xs.text = _sb.ToString();
            }
        }
    }

    protected void SetAccuracy(ref PlayerData pData, Overlay overlay, int i)
    {
        var s = Main.Settings;
        float acc = scrMistakesManager.marginTrackers[i].percentAcc;
        float maxAcc = 1 + (scrPlayerManager.instance.allPlayers[i].planetarySystem.chosenPlanet.currfloor.seqID - overlay.NoCheckStartTile + 1) * 0.0001f;
        float xacc = scrMistakesManager.marginTrackers[i].percentXAcc;
        if (float.IsNaN(xacc)) xacc = 1;
        string current = Math.Round(acc * 100, s.AccuracyDecimal) + "%";
        if (s.AccuracyTextType != PotentialTextType.Current)
        {
            // 潜力值：各玩家用自己的判定数组外推。coop 无独立潜力文本槽，全部内联进主文本
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            int seqID = GameRefs.CurrentSeqID;
            float potential = AccuracyMath.GetPotentialAccuracy(hits, acc,
                AccuracyMath.GetJudgedTiles(hits, seqID), AccuracyMath.GetRemainingTiles(seqID));
            string p = Math.Round(potential * 100, s.AccuracyDecimal) + "%";
            if (s.AccuracyTextType == PotentialTextType.Potential) current = p;
            else current += $" ({p})";
        }
        pData.AccuracyString = $" | <color=#{s.Colors.GetAccuracyHex(xacc == 1 ? 1 : acc / maxAcc, xacc == 1)}>{current}</color>";
    }

    protected void SetXAccuracy(ref PlayerData pData, int i)
    {
        var s = Main.Settings;
        float xacc = scrMistakesManager.marginTrackers[i].percentXAcc;
        if (float.IsNaN(xacc)) xacc = 1;
        string current = Math.Round(xacc * 100, s.XAccuracyDecimal) + "%";
        if (s.XAccuracyTextType != PotentialTextType.Current)
        {
            int seqID = GameRefs.CurrentSeqID;
            // 与单人路径一致：已判定数必须经 GetJudgedTiles 扣除 Midspin（r149+）。
            // 原实现直接拿 seqID 当已判定数，含 Midspin 的图里潜力 X 精度会偏。
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            float potential = AccuracyMath.GetPotentialXAccuracy(xacc,
                AccuracyMath.GetJudgedTiles(hits, seqID), AccuracyMath.GetRemainingTiles(seqID));
            string p = Math.Round(potential * 100, s.XAccuracyDecimal) + "%";
            if (s.XAccuracyTextType == PotentialTextType.Potential) current = p;
            else current += $" ({p})";
        }
        pData.XAccuracyString = $" | <color=#{s.Colors.GetXAccuracyHex(xacc, xacc == 1)}>{current}</color>";
    }

    protected void SetXScore(ref PlayerData pData, int i)
    {
        var s = Main.Settings;
        int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
        int seqID = GameRefs.CurrentSeqID;
        int judged = AccuracyMath.GetJudgedTiles(hits, seqID);
        int remaining = AccuracyMath.GetRemainingTiles(seqID);
        int xScore = AccuracyMath.GetXScore(hits);
        int maxXScore = judged * AccuracyMath.XPerfectValue;
        string current = AccuracyMath.GetXScoreText(xScore, maxXScore, s.XScoreTextType);
        if (s.XScorePotentialType != PotentialTextType.Current)
        {
            string p = AccuracyMath.GetXScoreText(xScore + remaining * AccuracyMath.XPerfectValue,
                maxXScore + remaining * AccuracyMath.XPerfectValue, s.XScoreTextType, potential: true);
            if (s.XScorePotentialType == PotentialTextType.Potential) current = p;
            else current += $" ({p})";
        }
        pData.XScoreString = $" | <color=#{s.Colors.XScore.GetHex(maxXScore == 0 ? 1 : (float)xScore / maxXScore)}>{current}</color>";
    }

    public void UpdateProgress(Overlay overlay)
    {
        var strings = new string[PlayerDatas.Length + 1];
        strings[0] = Main.Settings.Labels.Progress;
        if (overlay.StartTile > 0)
            strings[0] += $" | <color=#{Main.Settings.Colors.GetProgressHex(overlay.StartProgress, true)}>{Math.Round(overlay.StartProgress * 100, Main.Settings.ProgressDecimal)}%</color> ~";
        for (int i = 0; i < PlayerDatas.Length; i++) strings[i + 1] = PlayerDatas[i].ProgressString;
        overlay.ProgressText.text = string.Concat(strings);
    }

    public void UpdateProgressBar(Overlay overlay)
    {
        var bar = overlay.ProgressBar;
        bar.LineTransform.SizeDeltaX(MaxProgress * 638);
        bar.BackgroundImage.color = Main.Settings.Colors.GetProgressBarBackgroundColor(MaxProgress);
        bar.LineImage.color = Main.Settings.Colors.GetProgressBarColor(MaxProgress);
        bar.BorderImage.color = Main.Settings.Colors.GetProgressBarBorderColor(MaxProgress);
    }

    public void UpdateCheckpoint(Overlay overlay)
    {
        bool updated = false;
        while (overlay.Checkpoints.Length > CurCheck && GameRefs.CurrentSeqID >= overlay.Checkpoints[CurCheck])
        {
            CurCheck++; updated = true;
        }
        if (LastCheckpoint == GameRefs.CheckpointsUsed && !updated) return;
        overlay.CheckpointText.text = $"<color=white>{Main.Settings.Labels.Checkpoint} |</color> {GameRefs.CheckpointsUsed} ({CurCheck}/{overlay.Checkpoints.Length})";
        LastCheckpoint = GameRefs.CheckpointsUsed;
    }

    public void UpdateBest(Overlay overlay)
    {
        if (GameRefs.IsAuto && !overlay.AutoOnceEnabled) overlay.AutoOnceEnabled = true;
        if (CurBest == -1)
            CurBest = PlayCount.GetData(overlay.LastHash)?.GetBest(overlay.StartProgress, overlay.LastMultiplier) ?? 0;
        else if (CurBest > MaxProgress || overlay.AutoOnceEnabled) return;
        UpdateBestText(overlay);
    }

    public float GetProgress() => MaxProgress;

    public void UpdateBestText(Overlay overlay)
    {
        float best = CurBest > MaxProgress || overlay.AutoOnceEnabled ? CurBest : MaxProgress;
        overlay.BestText.text = $"<color=white>{Main.Settings.Labels.Best} |</color> {Math.Round(best * 100, Main.Settings.BestDecimal)}%";
        overlay.BestText.color = Main.Settings.Colors.GetBestColor(best);
    }

    public struct PlayerData
    {
        public float Progress;
        public string ProgressString;
        public string AccuracyString;
        public string XAccuracyString;
        public string XScoreString;
    }

    // ===== Jongyeol-mode helpers (coop, per-player) =====

    private int[] _playerDeath;
    private int[] _lastPlayerDeath;

    public void UpdateDeath(Overlay overlay)
    {
        var s = Main.Settings;
        if (!s.ShowDeath || !overlay.GameObject.activeSelf || overlay.DeathText == null) return;
        int count = VersionSafe.GetPlayerCount();
        if (_playerDeath == null || _playerDeath.Length != count)
        {
            _playerDeath = new int[count];
            _lastPlayerDeath = new int[count];
            for (int i = 0; i < count; i++) _lastPlayerDeath[i] = -1;
        }
        var sb = _sb;
        sb.Clear();
        sb.Append("<color=white>");
        sb.Append(s.Labels.Death);
        sb.Append("</color>");
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            // 死亡数 = FailMiss + FailOverload（r150 下标为 10/11）
            int death = HitMarginCompat.Get(hits, HitMarginCompat.FailMiss) + HitMarginCompat.Get(hits, HitMarginCompat.FailOverload);
            _playerDeath[i] = death;
            if (_lastPlayerDeath[i] != death) { _lastPlayerDeath[i] = death; changed = true; }
            string hex = VersionSafe.GetPlayerColorHex(i);
            sb.Append(" | <color=#");
            sb.Append(hex);
            sb.Append(">");
            sb.Append(death);
            sb.Append("</color>");
        }
        if (changed) overlay.DeathText.text = sb.ToString();
        overlay.DeathText.color = Color.white;
    }

    public void UpdateState(Overlay overlay, bool _)
    {
        var s = Main.Settings;
        if (!s.ShowState || !overlay.GameObject.activeSelf || overlay.StateText == null) return;
        var labels = s.Labels;
        int count = VersionSafe.GetPlayerCount();
        var sb = _sb;
        sb.Clear();
        sb.Append("<color=white>");
        sb.Append(labels.State);
        sb.Append("</color>");
        for (int i = 0; i < count; i++)
        {
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            var p = scrPlayerManager.instance.allPlayers[i];
            string state = GetPlayerState(p, hits, overlay);
            string hex = VersionSafe.GetPlayerColorHex(i);
            sb.Append(" | <color=#");
            sb.Append(hex);
            sb.Append(">");
            sb.Append(state);
            sb.Append("</color>");
        }
        if (overlay.StartTile != 0) { sb.Append("  "); sb.Append(labels.StateMidStart); }
        overlay.StateText.text = sb.ToString();
        overlay.StateText.color = Color.white;
    }

    private static string GetPlayerState(scrPlayer player, int[] hits, Overlay overlay)
    {
        var labels = Main.Settings.Labels;
        string state;

        if (GameRefs.CurrentSeqID == overlay.StartTile)
            state = labels.StateWaiting;
        else if (!GameRefs.IsAuto && player.auto)
            state = labels.StateAuto;  // respawn waiting
        else
        {
            var curFloor = player.planetarySystem?.chosenPlanet?.currfloor;
            if (curFloor != null && curFloor.nextfloor is { auto: true })
                state = labels.StateAutoTile;
            else if (GameRefs.IsAuto)
                state = labels.StateAuto;
            else if (IsPurePerfect(hits))
                state = labels.StatePerfectPlay;
            else
            {
                int death = HitMarginCompat.Get(hits, HitMarginCompat.FailMiss) + HitMarginCompat.Get(hits, HitMarginCompat.FailOverload);
                if (death != 0) state = labels.StateComplete;
                else if (HitMarginCompat.Get(hits, HitMarginCompat.TooEarly) != 0) state = labels.StateClear;
                else if (HitMarginCompat.Get(hits, HitMarginCompat.VeryEarly) != 0 || HitMarginCompat.Get(hits, HitMarginCompat.VeryLate) != 0) state = labels.StateNoMiss;
                else state = labels.StatePerfectionist;
            }
        }
        var allFloors = GameRefs.LevelMaker?.listFloors;
        if (allFloors != null && GameRefs.CurrentSeqID != allFloors.Count)
            state += labels.StateSuffix;
        return state;
    }

    private static bool IsPurePerfect(int[] hits) => HitMarginCompat.IsPurePerfect(hits);

    public bool CheckPurePerfect(Overlay overlay)
    {
        int count = VersionSafe.GetPlayerCount();
        for (int p = 0; p < count; p++)
        {
            if (!HitMarginCompat.IsPurePerfect(VersionSafe.GetHitMarginsCountForPlayer(p)))
                return false;
        }
        return true;
    }

    public int GetTooJudgement(Overlay overlay)
    {
        int total = 0;
        int count = VersionSafe.GetPlayerCount();
        for (int i = 0; i < count; i++)
        {
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            // “Too” 判定数 = TooEarly + TooLate（r150 下标为 0/8）
            total += HitMarginCompat.Get(hits, HitMarginCompat.TooEarly) + HitMarginCompat.Get(hits, HitMarginCompat.TooLate);
        }
        return total;
    }

    public void DirtyTextCaches()
    {
        if (_lastPlayerDeath != null)
            for (int i = 0; i < _lastPlayerDeath.Length; i++) _lastPlayerDeath[i] = -1;
        LastCheckpoint = -1;
        CurBest = -1;
    }
}