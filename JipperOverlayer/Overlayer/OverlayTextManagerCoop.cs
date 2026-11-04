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

    public OverlayTextManagerCoop(Overlay overlay)
    {
        int count = Math.Max(1, scrPlayerManager.playerCount);
        PlayerDatas = new PlayerData[count];
        for (int i = 0; i < PlayerDatas.Length; i++)
        {
            ref var pd = ref PlayerDatas[i];
            pd.ProgressCache.Buffer = new char[48];
            pd.AccuracyCache.Buffer = new char[64];
            pd.XAccuracyCache.Buffer = new char[64];
            pd.XScoreCache.Buffer = new char[96];
        }
        if (overlay.ProgressText) overlay.ProgressText.color = Color.white;
        if (overlay.AccuracyText) overlay.AccuracyText.color = Color.white;
        if (overlay.XAccuracyText) overlay.XAccuracyText.color = Color.white;
        // XScore 底色必须回白：分隔符继承组件底色，而单人模式 SetDualText 会把
        // 底色写成渐变色；coop 的数值颜色全靠 rich text 标签，残留底色会染到竖线上。
        var xs = overlay.ExtendedOverlay?.XScoreText;
        if (xs) xs.color = Color.white;
    }

    static bool TryGetPlayer(int index, out scrPlayer player)
    {
        player = null;
        var manager = scrPlayerManager.instance;
        var players = manager?.allPlayers;
        if (players == null || index < 0 || index >= players.Length) return false;
        player = players[index];
        return player != null;
    }

    static int? GetPlayerSeqID(int index)
    {
        if (!TryGetPlayer(index, out var player)) return null;
        return player.planetarySystem?.chosenPlanet?.currfloor?.seqID;
    }

    public void SetBest(float best) => CurBest = best;

    public void CacheProgress(scrPlanet planet)
    {
        var allFloors = GameRefs.LevelMaker?.listFloors;
        if (allFloors == null || allFloors.Count == 0) return;
        float count = allFloors.Count;
        if ((object)planet == null)
        {
            for (int i = 0; i < PlayerDatas.Length; i++)
            {
                int? seq = GetPlayerSeqID(i);
                if (seq.HasValue) SetProgress(ref PlayerDatas[i], (seq.Value + 1) / count);
            }
        }
        else
        {
            int index = planet.player?.playerID ?? -1;
            if (index >= 0 && index < PlayerDatas.Length && planet.currfloor != null)
                SetProgress(ref PlayerDatas[index], (planet.currfloor.seqID + 1) / count);
        }
    }

    protected void SetProgress(ref PlayerData pData, float progress)
    {
        pData.Progress = progress;
        _sb.Clear();
        _sb.Append(" | <color=#");
        _sb.Append(Main.Settings.Colors.GetProgressHex(progress, true));
        _sb.Append(">");
        _sb.Append(Math.Round(progress * 100, Main.Settings.ProgressDecimal));
        _sb.Append("%</color>");
        pData.ProgressCache.Store(_sb);
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
            else if (index >= 0 && index < PlayerDatas.Length)
                SetAccuracy(ref PlayerDatas[index], overlay, index);

            _sb.Clear();
            _sb.Append(Main.Settings.Labels.Accuracy);
            for (int i = 0; i < PlayerDatas.Length; i++) PlayerDatas[i].AccuracyCache.AppendTo(_sb);
            overlay.AccuracyText.SetText(_sb);
        }
        if (Main.Settings.ShowXAccuracy)
        {
            if (index == -1)
                for (int i = 0; i < PlayerDatas.Length; i++)
                    SetXAccuracy(ref PlayerDatas[i], i);
            else if (index >= 0 && index < PlayerDatas.Length)
                SetXAccuracy(ref PlayerDatas[index], index);

            _sb.Clear();
            _sb.Append(Main.Settings.Labels.XAccuracy);
            for (int i = 0; i < PlayerDatas.Length; i++) PlayerDatas[i].XAccuracyCache.AppendTo(_sb);
            overlay.XAccuracyText.SetText(_sb);
        }
        if (s.ShowXScore && HitMarginCompat.HasNativeXPerfect)
        {
            if (index == -1)
                for (int i = 0; i < PlayerDatas.Length; i++)
                    SetXScore(ref PlayerDatas[i], i);
            else if (index >= 0 && index < PlayerDatas.Length)
                SetXScore(ref PlayerDatas[index], index);

            var xs = overlay.ExtendedOverlay?.XScoreText;
            if (xs)
            {
                // 各段分隔符 " | " 在 rich text 标签之外，继承组件底色；单人路径
                // SetDualText 会把底色写成渐变色（开局 t=1 即 PerfectColor），coop
                // 不重置的话，上次单人局的残留色会染在竖线分隔符上。
                xs.color = Color.white;
                _sb.Clear();
                _sb.Append("<color=white>XScore |</color>");
                for (int i = 0; i < PlayerDatas.Length; i++) PlayerDatas[i].XScoreCache.AppendTo(_sb);
                xs.SetText(_sb);
            }
        }
    }

    protected void SetAccuracy(ref PlayerData pData, Overlay overlay, int i)
    {
        var s = Main.Settings;
        var trackers = scrMistakesManager.marginTrackers;
        if (trackers == null || i < 0 || i >= trackers.Length || trackers[i] == null) return;
        float acc = trackers[i].percentAcc;
        int? playerSeq = GetPlayerSeqID(i);
        float maxAcc = 1 + ((playerSeq ?? GameRefs.CurrentSeqID) - overlay.NoCheckStartTile + 1) * 0.0001f;
        float xacc = trackers[i].percentXAcc;
        if (float.IsNaN(xacc)) xacc = 1;
        var type = s.AccuracyTextType;
        int dec = s.AccuracyDecimal;
        string hex = s.Colors.GetAccuracyHex(xacc == 1 ? 1 : acc / maxAcc, xacc == 1);
        _sb.Clear();
        _sb.Append(" | <color=#");
        _sb.Append(hex);
        _sb.Append(">");
        if (type == PotentialTextType.Current)
        {
            _sb.Append(Math.Round(acc * 100, dec));
            _sb.Append('%');
        }
        else
        {
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            int seqID = GetPlayerSeqID(i) ?? GameRefs.CurrentSeqID;
            float potential = AccuracyMath.GetPotentialAccuracy(hits, acc,
                AccuracyMath.GetJudgedTiles(hits, seqID), AccuracyMath.GetRemainingTiles(seqID));
            if (type == PotentialTextType.Potential)
            {
                _sb.Append(Math.Round(potential * 100, dec));
                _sb.Append('%');
            }
            else // Both / BothInOneLine -> "curr (pot)"
            {
                _sb.Append(Math.Round(acc * 100, dec));
                _sb.Append("% (");
                _sb.Append(Math.Round(potential * 100, dec));
                _sb.Append("%)");
            }
        }
        _sb.Append("</color>");
        pData.AccuracyCache.Store(_sb);
    }

    protected void SetXAccuracy(ref PlayerData pData, int i)
    {
        var s = Main.Settings;
        var trackers = scrMistakesManager.marginTrackers;
        if (trackers == null || i < 0 || i >= trackers.Length || trackers[i] == null) return;
        float xacc = trackers[i].percentXAcc;
        if (float.IsNaN(xacc)) xacc = 1;
        var type = s.XAccuracyTextType;
        int dec = s.XAccuracyDecimal;
        string hex = s.Colors.GetXAccuracyHex(xacc, xacc == 1);
        _sb.Clear();
        _sb.Append(" | <color=#");
        _sb.Append(hex);
        _sb.Append(">");
        if (type == PotentialTextType.Current)
        {
            _sb.Append(Math.Round(xacc * 100, dec));
            _sb.Append('%');
        }
        else
        {
            int seqID = GetPlayerSeqID(i) ?? GameRefs.CurrentSeqID;
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            float potential = AccuracyMath.GetPotentialXAccuracy(xacc,
                AccuracyMath.GetJudgedTiles(hits, seqID), AccuracyMath.GetRemainingTiles(seqID));
            if (type == PotentialTextType.Potential)
            {
                _sb.Append(Math.Round(potential * 100, dec));
                _sb.Append('%');
            }
            else
            {
                _sb.Append(Math.Round(xacc * 100, dec));
                _sb.Append("% (");
                _sb.Append(Math.Round(potential * 100, dec));
                _sb.Append("%)");
            }
        }
        _sb.Append("</color>");
        pData.XAccuracyCache.Store(_sb);
    }

    protected void SetXScore(ref PlayerData pData, int i)
    {
        var s = Main.Settings;
        int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
        int seqID = GetPlayerSeqID(i) ?? GameRefs.CurrentSeqID;
        int judged = AccuracyMath.GetJudgedTiles(hits, seqID);
        int remaining = AccuracyMath.GetRemainingTiles(seqID);
        var trackers = scrMistakesManager.marginTrackers;
        bool native = AccuracyMath.TryGetNativeXScore(trackers != null && i < trackers.Length ? trackers[i] : null,
            out int x, out int mapMax);
        int soFarMax, potentialX, potentialTotal;
        if (native)
        {
            int rem = AccuracyMath.GetRemainingPlayerHitFloors(seqID);
            soFarMax = Math.Max(0, mapMax - rem * AccuracyMath.XPerfectValue);
            potentialTotal = mapMax;
            potentialX = x + rem * AccuracyMath.XPerfectValue;
        }
        else
        {
            x = AccuracyMath.GetXScore(hits);
            soFarMax = judged * AccuracyMath.XPerfectValue;
            potentialTotal = soFarMax + remaining * AccuracyMath.XPerfectValue;
            potentialX = x + remaining * AccuracyMath.XPerfectValue;
        }
        string hex = s.Colors.GetXScoreHex(soFarMax == 0 ? 1 : (float)x / soFarMax);
        var type = s.XScorePotentialType;
        _sb.Clear();
        _sb.Append(" | <color=#");
        _sb.Append(hex);
        _sb.Append(">");
        if (type == PotentialTextType.Current)
        {
            _sb.Append(AccuracyMath.GetXScoreText(x, soFarMax, s.XScoreTextType));
        }
        else if (type == PotentialTextType.Potential)
        {
            string p = AccuracyMath.GetXScoreText(potentialX, potentialTotal, s.XScoreTextType, potential: true);
            _sb.Append(p);
        }
        else // Both / BothInOneLine
        {
            string cur = AccuracyMath.GetXScoreText(x, soFarMax, s.XScoreTextType);
            string p = AccuracyMath.GetXScoreText(potentialX, potentialTotal, s.XScoreTextType, potential: true);
            _sb.Append(cur);
            _sb.Append(" (");
            _sb.Append(p);
            _sb.Append(')');
        }
        _sb.Append("</color>");
        pData.XScoreCache.Store(_sb);
    }

    public void UpdateProgress(Overlay overlay)
    {
        _sb.Clear();
        _sb.Append(Main.Settings.Labels.Progress);
        if (overlay.StartTile > 0)
        {
            _sb.Append(" | <color=#");
            _sb.Append(Main.Settings.Colors.GetProgressHex(overlay.StartProgress, true));
            _sb.Append(">");
            _sb.Append(Math.Round(overlay.StartProgress * 100, Main.Settings.ProgressDecimal));
            _sb.Append("%</color> ~");
        }
        for (int i = 0; i < PlayerDatas.Length; i++) PlayerDatas[i].ProgressCache.AppendTo(_sb);
        overlay.ProgressText.SetText(_sb);
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
        else if (CurBest > MaxProgress || overlay.AutoOnceEnabled) return;
        UpdateBestText(overlay);
    }

    public float GetProgress() => MaxProgress;

    public void UpdateBestText(Overlay overlay)
    {
        float best = CurBest > MaxProgress || overlay.AutoOnceEnabled ? CurBest : MaxProgress;
        _sb.Clear();
        _sb.Append("<color=white>");
        _sb.Append(Main.Settings.Labels.Best);
        _sb.Append(" |</color> ");
        _sb.Append(Math.Round(best * 100, Main.Settings.BestDecimal));
        _sb.Append('%');
        overlay.BestText.SetText(_sb);
        overlay.BestText.color = Main.Settings.Colors.GetBestColor(best);
    }

    public struct PlayerData
    {
        public float Progress;
        public CharCache ProgressCache;
        public CharCache AccuracyCache;
        public CharCache XAccuracyCache;
        public CharCache XScoreCache;
    }

    // ===== Extended-overlay helpers (coop, per-player) =====

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
        if (changed) overlay.DeathText.SetText(sb);
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
            if (!TryGetPlayer(i, out var p)) continue;
            int[] hits = VersionSafe.GetHitMarginsCountForPlayer(i);
            string state = GetPlayerState(p, hits, overlay);
            string hex = VersionSafe.GetPlayerColorHex(i);
            sb.Append(" | <color=#");
            sb.Append(hex);
            sb.Append(">");
            sb.Append(state);
            sb.Append("</color>");
        }
        if (overlay.StartTile != 0) { sb.Append("  "); sb.Append(labels.StateMidStart); }
        overlay.StateText.SetText(sb);
        overlay.StateText.color = Color.white;
    }

    private static string GetPlayerState(scrPlayer player, int[] hits, Overlay overlay)
    {
        var labels = Main.Settings.Labels;
        string state;

        if (GameRefs.CurrentSeqID == overlay.StartTile)
            state = labels.StateWaiting;
        else if (!GameRefs.IsAuto && player.auto)
            state = labels.StateAuto;
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
