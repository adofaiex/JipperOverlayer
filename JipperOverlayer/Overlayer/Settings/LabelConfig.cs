using System;
using System.IO;
using Newtonsoft.Json;
using JipperOverlayer.Overlayer.Localization;

namespace JipperOverlayer.Overlayer;

public class LabelConfig
{
    // Standard labels
    public string Progress = "Progress";
    public string Accuracy = "Accuracy";
    public string XAccuracy = "XAccuracy";
    public string MusicTime = "Music Time";
    public string MapTime = "Map Time";
    public string Checkpoint = "CheckPoint";
    public string Best = "Best";
    public string TBPM = "TBPM";
    public string CBPM = "CBPM";
    public string KPS = "KPS";
    public string Attempt = "Attempt";
    public string FullAttempt = "Full Attempt";
    public string TimingScale = "Timing Scale";
    public string XPerfectLabel = "XPerfect";
    public string PerfectLabel = "Perfect";
    public string GreatLabel = "Great";
    public string GoodLabel = "Good";
    public string ComboTitle = "Perfect";
    public string ComboTitleAlt = "Combo";

    // Extended overlay labels
    public string FPS = "FPS";
    public string Author = "Author";
    public string State = "State";
    public string Death = "Death";
    public string Start = "Start";
    public string Timing = "Timing";

    // State text (ExtendedOverlay)
    public string StateWaiting = "Waiting";
    public string StateAutoTile = "Auto Tile";
    public string StateAuto = "Auto Play";
    public string StatePerfectPlay = "Perfect Play";
    public string StateComplete = "Completed";
    public string StateClear = "Clear";
    public string StateNoMiss = "No Miss";
    public string StatePerfectionist = "Perfectionist";
    public string StateSuffix = " (playing)";
    public string StateMidStart = " (mid start)";

    public void Save()
    {
        try { File.WriteAllText(Path.Combine(Loader.ModPath, "labels.json"), JsonConvert.SerializeObject(this, Formatting.Indented)); }
        catch (Exception e) { Loader.Warning($"Save labels failed: {e.Message}"); }
    }

    public static LabelConfig Load()
    {
        try {
            string p = Path.Combine(Loader.ModPath, "labels.json");
            if (File.Exists(p)) {
                return JsonConvert.DeserializeObject<LabelConfig>(File.ReadAllText(p)) ?? new LabelConfig();
            }
        }
        catch (Exception e) { Loader.Warning($"Load labels failed: {e.Message}"); }
        return new LabelConfig();
    }

    public static LabelConfig GetPreset(Language language) => language switch
    {
        Language.Korean => KoreanPreset(),
        Language.Chinese => ChinesePreset(),
        _ => new LabelConfig(),
    };

    static LabelConfig KoreanPreset() => new()
    {
        Progress = "진행도",
        Accuracy = "정확도",
        XAccuracy = "X정확도",
        MusicTime = "음악 시간",
        MapTime = "맵 시간",
        Checkpoint = "체크포인트",
        Best = "최고 기록",
        TBPM = "TBPM",
        CBPM = "CBPM",
        KPS = "KPS",
        Attempt = "시도 횟수",
        FullAttempt = "전체 시도",
        TimingScale = "타이밍 스케일",
        XPerfectLabel = "X완벽",
        PerfectLabel = "완벽",
        GreatLabel = "빠름/느림",
        GoodLabel = "너무 빠름/느림",
        ComboTitle = "완벽",
        ComboTitleAlt = "콤보",
        FPS = "FPS",
        Author = "제작자",
        State = "상태",
        Death = "사망",
        Start = "시작",
        Timing = "타이밍",
        StateWaiting = "대기",
        StateAutoTile = "자동 플레이 타일",
        StateAuto = "자동 플레이",
        StatePerfectPlay = "완벽한 플레이",
        StateComplete = "완주",
        StateClear = "클리어",
        StateNoMiss = "노미스",
        StatePerfectionist = "완벽주의",
        StateSuffix = " 중",
        StateMidStart = "(중간에서 시작)",
    };

    static LabelConfig ChinesePreset() => new()
    {
        Progress = "进度",
        Accuracy = "准确率",
        XAccuracy = "X准确率",
        MusicTime = "音乐时间",
        MapTime = "地图时间",
        Checkpoint = "检查点",
        Best = "最佳",
        TBPM = "TBPM",
        CBPM = "CBPM",
        KPS = "KPS",
        Attempt = "尝试次数",
        FullAttempt = "总尝试",
        TimingScale = "判定区间",
        XPerfectLabel = "X完美",
        PerfectLabel = "完美",
        GreatLabel = "稍快/稍慢",
        GoodLabel = "太快/太慢",
        ComboTitle = "完美",
        ComboTitleAlt = "连击",
        FPS = "FPS",
        Author = "作者",
        State = "状态",
        Death = "死亡",
        Start = "开始",
        Timing = "时机",
        StateWaiting = "等待",
        StateAutoTile = "自动方块",
        StateAuto = "自动播放",
        StatePerfectPlay = "完美游玩",
        StateComplete = "完成",
        StateClear = "通关",
        StateNoMiss = "无Miss",
        StatePerfectionist = "完美主义",
        StateSuffix = " 中",
        StateMidStart = " (中途开始)",
    };
}
