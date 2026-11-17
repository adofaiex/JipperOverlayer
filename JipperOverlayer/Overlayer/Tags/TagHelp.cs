using System.Collections.Generic;
using JipperOverlayer.Overlayer.Localization;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 面向玩家的 tag 说明表：设置面板「文本模板 → 可用 tag」直接渲染这张表。
/// 三语（中/英/韩）跟随设置语言；与 <see cref="GameTags"/> 的实现分开存放，
/// 避免把展示用文案混进求值代码。新增 tag 时必须同时在这里登记，
/// 否则面板会显示「未登记说明」。顺序即面板里的展示顺序。
/// </summary>
internal static class TagHelp
{
    internal readonly struct Entry
    {
        public readonly string Name;
        private readonly string Zh;
        private readonly string En;
        private readonly string Ko;
        public Entry(string name, string zh, string en, string ko) { Name = name; Zh = zh; En = en; Ko = ko; }
        /// <summary>按当前设置语言取说明。</summary>
        public string Desc => L(Zh, En, Ko);
    }

    internal sealed class Group
    {
        private readonly string Zh;
        private readonly string En;
        private readonly string Ko;
        public readonly Entry[] Items;
        public Group(string zh, string en, string ko, params Entry[] items) { Zh = zh; En = en; Ko = ko; Items = items; }
        public string Title => L(Zh, En, Ko);
    }

    internal sealed class Example
    {
        private readonly string Zh;
        private readonly string En;
        private readonly string Ko;
        public readonly string Template;
        public Example(string zh, string en, string ko, string template) { Zh = zh; En = en; Ko = ko; Template = template; }
        public string Title => L(Zh, En, Ko);
    }

    /// <summary>语言取值：与 Tr.Get 相同的回退顺序（设置缺失/未知 → 英文）。</summary>
    private static string L(string zh, string en, string ko)
    {
        try
        {
            var lang = Main.Settings?.CurrentLanguage;
            if (lang == Language.Chinese) return zh;
            if (lang == Language.Korean) return ko;
            return en;
        }
        catch { return en; }
    }

    private static Entry E(string name, string zh, string en, string ko) => new(name, zh, en, ko);

    internal static readonly Group[] Groups =
    {
        new("进度与格子", "Progress & Tiles", "진행도와 타일",
            E("Progress", "进度百分比（0-100）", "Progress percent (0-100)", "진행도 백분율 (0-100)"),
            E("Progress01", "进度比例（0-1）", "Progress ratio (0-1)", "진행도 비율 (0-1)"),
            E("CurTile", "当前格 seqID", "Current tile seqID", "현재 타일 seqID"),
            E("TotalTile", "总格数（末格 seqID）", "Total tiles (last tile seqID)", "전체 타일 수 (마지막 타일 seqID)"),
            E("TilesLeft", "剩余未判定格数", "Remaining unjudged tiles", "남은 미판정 타일 수"),
            E("StartTile", "本局起始格", "Run start tile", "이번 런 시작 타일"),
            E("StartProgress", "起始进度百分比", "Start progress percent", "시작 진행도 백분율"),
            E("Best", "最佳进度百分比", "Best progress percent", "최고 진행도 백분율"),
            E("Best01", "最佳进度比例（0-1）", "Best progress ratio (0-1)", "최고 진행도 비율 (0-1)"),
            E("CheckpointPassed", "本次已过检查点数", "Checkpoints passed this run", "이번 런에서 통과한 체크포인트 수"),
            E("CheckpointsTotal", "地图检查点总数", "Total checkpoints on the map", "맵의 전체 체크포인트 수"),
            E("CheckpointsUsed", "已使用检查点次数", "Checkpoints used", "사용한 체크포인트 횟수")),

        new("精度", "Accuracy", "정확도",
            E("Accuracy", "准确率百分比", "Accuracy percent", "정확도 백분율"),
            E("Accuracy01", "准确率比例（0-1）", "Accuracy ratio (0-1)", "정확도 비율 (0-1)"),
            E("XAccuracy", "X 准确率百分比", "XAccuracy percent", "X정확도 백분율"),
            E("XAccuracy01", "X 准确率比例（0-1）", "XAccuracy ratio (0-1)", "X정확도 비율 (0-1)"),
            E("PotentialAccuracy", "潜力准确率（剩余全完美）", "Potential accuracy (all remaining perfect)", "잠재 정확도 (남은 칸 전부 완벽)"),
            E("PotentialXAccuracy", "潜力 X 准确率", "Potential XAccuracy", "잠재 X정확도")),

        new("XScore", "XScore", "XScore",
            E("XScore", "当前 XScore", "Current XScore", "현재 XScore"),
            E("XScoreMax", "已判定格的满分（当前分 + 已掉分）", "Max score of already judged tiles", "판정된 칸의 만점 (현재 점수 + 잃은 점수)"),
            E("XScoreLoss", "掉分额，即 MAX-n 里的 n", "Points lost - the n in MAX-n", "잃은 점수, MAX-n의 n"),
            E("XScoreTotal", "全图满分", "Full-map max score", "맵 전체 만점"),
            E("PotentialXScore", "潜力 XScore", "Potential XScore", "잠재 XScore"),
            E("PotentialXScoreMax", "潜力值分母", "Potential value denominator", "잠재값 분모"),
            E("XScoreRatio", "XScore / 已判定满分 x100（满命中 = 100，配合 ColorRange atMax 做满命中转金）", "XScore / max of judged tiles x100 (100 = perfect, use with ColorRange atMax for gold)", "XScore / 판정된 칸 만점 x100 (완벽 = 100, ColorRange atMax와 함께 사용)")),

        new("判定计数", "Judgement counts", "판정 카운트",
            E("XPerfect", "X 完美", "XPerfect", "X완벽"),
            E("PerfectMinus", "稍快（-P）", "Slightly early (-P)", "완벽(빠름, -P)"),
            E("PerfectPlus", "稍慢（+P）", "Slightly late (+P)", "완벽(느림, +P)"),
            E("Perfect", "中心完美合计（-P + X + +P）", "Core perfect total (-P + X + +P)", "중앙 완벽 합계 (-P + X + +P)"),
            E("PXP", "纯 XPerfect（不含 Auto）", "Pure XPerfect (excluding Auto)", "순수 X완벽 (Auto 제외)"),
            E("EarlyPerfect", "早 Perfect（黄绿）", "Early Perfect (yellow-green)", "약간 빠름 (연두)"),
            E("LatePerfect", "晚 Perfect（黄绿）", "Late Perfect (yellow-green)", "약간 느림 (연두)"),
            E("VeryEarly", "Very Early（橙）", "Very Early (orange)", "매우 빠름 (주황)"),
            E("VeryLate", "Very Late（橙）", "Very Late (orange)", "매우 느림 (주황)"),
            E("TooEarly", "Too Early（红）", "Too Early (red)", "너무 빠름 (빨강)"),
            E("TooLate", "Too Late（红）", "Too Late (red)", "너무 느림 (빨강)"),
            E("FailMiss", "Miss 失败", "Fail (miss)", "실패 (미스)"),
            E("FailOverload", "过载失败", "Fail (overload)", "실패 (과부하)"),
            E("Miss", "Miss 数（= FailMiss）", "Miss count (= FailMiss)", "미스 수 (= FailMiss)"),
            E("Overload", "过载数（= FailOverload）", "Overload count (= FailOverload)", "과부하 수 (= FailOverload)"),
            E("Death", "死亡数（FailMiss + FailOverload）", "Death count (FailMiss + FailOverload)", "사망 수 (FailMiss + FailOverload)"),
            E("Multipress", "多押次数", "Multipress count", "다중 입력 횟수"),
            E("Auto", "Auto 判定数（自动地板）", "Auto judgement count (auto floors)", "자동 판정 수 (자동 타일)"),
            E("OverPress", "OverPress 次数", "OverPress count", "과입력 횟수"),
            E("JudgedTiles", "已判定格数（含 auto/midspin）", "Judged tiles (incl. auto/midspin)", "판정된 타일 수 (auto/midspin 포함)"),
            E("ScoredTiles", "已判定计分格数", "Scored judged tiles", "점수화된 판정 타일 수")),

        new("判定聚合", "Judgement aggregates", "판정 집계",
            E("Fast", "偏快组：TooEarly + VeryEarly + EarlyPerfect + -P", "Early-side total: TooEarly + VeryEarly + EarlyPerfect + -P", "빠른 쪽 합계: TooEarly + VeryEarly + EarlyPerfect + -P"),
            E("Slow", "偏慢组：+P + LatePerfect + VeryLate + TooLate", "Late-side total: +P + LatePerfect + VeryLate + TooLate", "느린 쪽 합계: +P + LatePerfect + VeryLate + TooLate"),
            E("InnerPerfect", "内圈完美：-P + +P", "Inner perfects: -P + +P", "내부 완벽: -P + +P"),
            E("OuterPerfect", "外圈完美：EarlyPerfect + LatePerfect", "Outer perfects: EarlyPerfect + LatePerfect", "외부 완벽: EarlyPerfect + LatePerfect"),
            E("VeryTotal", "Very 组：VeryEarly + VeryLate", "Very total: VeryEarly + VeryLate", "Very 합계: VeryEarly + VeryLate"),
            E("TooTotal", "Too 组：TooEarly + TooLate", "Too total: TooEarly + TooLate", "Too 합계: TooEarly + TooLate")),

        new("连击", "Combo", "콤보",
            E("Combo", "当前连击", "Current combo", "현재 콤보"),
            E("MaxCombo", "本次尝试最大连击", "Best combo this run", "이번 런 최대 콤보"),
            E("MaxPossibleCombo", "剩余格全完美的最终连击", "Final combo if all remaining tiles are perfect", "남은 칸을 전부 완벽으로 맞췄을 때의 콤보")),

        new("时间", "Time", "시간",
            E("MusicTime", "音乐当前时间（1:23）", "Song time (1:23)", "노래 현재 시간 (1:23)"),
            E("MusicTotal", "音乐总时长（1:23）", "Song length (1:23)", "노래 총 길이 (1:23)"),
            E("MusicSeconds", "音乐当前时间（秒）", "Song time in seconds", "노래 현재 시간 (초)"),
            E("MusicTotalSeconds", "音乐总时长（秒）", "Song length in seconds", "노래 총 길이 (초)"),
            E("MusicHour", "音乐当前时间：时", "Song time: hour", "노래 현재 시간: 시"),
            E("MusicMinute", "音乐当前时间：分", "Song time: minute", "노래 현재 시간: 분"),
            E("MusicSecond", "音乐当前时间：秒", "Song time: second", "노래 현재 시간: 초"),
            E("MusicMilliSecond", "音乐当前时间：毫秒", "Song time: millisecond", "노래 현재 시간: 밀리초"),
            E("MusicTotalMinute", "音乐总时长：分", "Song length: minute", "노래 총 길이: 분"),
            E("MusicTotalSecond", "音乐总时长：秒", "Song length: second", "노래 총 길이: 초"),
            E("MusicTotalMilliSecond", "音乐总时长：毫秒", "Song length: millisecond", "노래 총 길이: 밀리초"),
            E("MapTime", "地图当前时间（1:23）", "Map time (1:23)", "맵 현재 시간 (1:23)"),
            E("MapTotal", "地图总时长（1:23）", "Map length (1:23)", "맵 총 길이 (1:23)"),
            E("MapSeconds", "地图当前时间（秒）", "Map time in seconds", "맵 현재 시간 (초)"),
            E("MapTotalSeconds", "地图总时长（秒）", "Map length in seconds", "맵 총 길이 (초)"),
            E("MapMinute", "地图当前时间：分", "Map time: minute", "맵 현재 시간: 분"),
            E("MapSecond", "地图当前时间：秒", "Map time: second", "맵 현재 시간: 초"),
            E("MapMilliSecond", "地图当前时间：毫秒", "Map time: millisecond", "맵 현재 시간: 밀리초"),
            E("MapTotalMinute", "地图总时长：分", "Map length: minute", "맵 총 길이: 분"),
            E("MapTotalSecond", "地图总时长：秒", "Map length: second", "맵 총 길이: 초"),
            E("MapTotalMilliSecond", "地图总时长：毫秒", "Map length: millisecond", "맵 총 길이: 밀리초")),

        new("BPM", "BPM", "BPM",
            E("TileBpm", "Tile BPM（含音高）", "Tile BPM (with pitch)", "타일 BPM (피치 포함)"),
            E("CurBpm", "当前 BPM（含音高）", "Current BPM (with pitch)", "현재 BPM (피치 포함)"),
            E("Kps", "每秒按键数（当前 BPM ÷ 60）", "Keys per second (BPM / 60)", "초당 키 수 (현재 BPM / 60)"),
            E("TileBpmWithoutPitch", "Tile BPM（不含音高）", "Tile BPM (pitch excluded)", "타일 BPM (피치 제외)"),
            E("CurBpmWithoutPitch", "当前 BPM（不含音高）", "Current BPM (pitch excluded)", "현재 BPM (피치 제외)"),
            E("MaxBpm", "全图最高 BPM（含音高）", "Highest BPM on the map (with pitch)", "맵 최고 BPM (피치 포함)"),
            E("MaxKps", "全图最高 KPS", "Highest KPS on the map", "맵 최고 KPS"),
            E("MaxBpmWithoutPitch", "全图最高 BPM（不含音高）", "Highest BPM (pitch excluded)", "맵 최고 BPM (피치 제외)")),

        new("音高", "Pitch", "피치",
            E("Pitch", "当前实际播放音高（地图 pitch × 速度倍率）", "Current playback pitch (map pitch × speed)", "현재 재생 피치 (맵 피치 × 속도)"),
            E("EditorPitch", "地图 pitch 设置（1 = 100%）", "Map pitch setting (1 = 100%)", "맵 피치 설정 (1 = 100%)"),
            E("SpeedPitch", "速度试炼倍率（1 = 原速）", "Speed trial multiplier (1 = normal)", "속도 시련 배율 (1 = 기본)")),

        new("尝试与时机", "Attempts & Timing", "시도와 타이밍",
            E("Attempt", "本起点尝试次数", "Attempts from this start point", "이 시작점의 시도 횟수"),
            E("FullAttempt", "总尝试次数", "Total attempts", "전체 시도 횟수"),
            E("Timing", "最近一次打击偏移（ms）", "Latest hit offset (ms)", "최근 타격 오프셋 (ms)"),
            E("AvgTiming", "全部打击平均偏移（ms）", "Average hit offset (ms)", "전체 타격 평균 오프셋 (ms)"),
            E("HitOffset", "最近一次打击误差（ms）", "Latest hit offset (ms)", "최근 타격 오차 (ms)"),
            E("TimingScale", "判定区间倍率百分比（100 = 原速）", "Timing window scale percent (100 = normal)", "판정 배율 백분율 (100 = 기본)"),
            E("Fps", "帧率；可带窗口参数：Fps(500) 为最近 500ms 平均值且每 500ms 更新一次", "Frames per second; optional window: Fps(500) averages the last 500ms and refreshes every 500ms", "프레임율; 창 인수 지원: Fps(500)은 최근 500ms 평균이며 500ms마다 갱신")),

        new("判定时间窗", "Judgement windows", "판정 창",
            E("XPerfectMs", "X 完美窗口（±ms，仅 r149+）", "XPerfect window (±ms, r149+ only)", "X완벽 판정창 (±ms, r149+)"),
            E("PerfectMs", "完美窗口（±ms）", "Perfect window (±ms)", "완벽 판정창 (±ms)"),
            E("GreatMs", "Great 窗口（±ms）", "Great window (±ms)", "Great 판정창 (±ms)"),
            E("GoodMs", "Good 窗口（±ms）", "Good window (±ms)", "Good 판정창 (±ms)")),

        new("关卡与状态", "Level & State", "레벨과 상태",
            E("Title", "曲名", "Song title", "곡 제목"),
            E("Artist", "曲师", "Song artist", "곡 아티스트"),
            E("Author", "关卡作者", "Level author", "레벨 제작자"),
            E("LevelName", "关卡名文本（已去富文本）", "Level name text (rich text stripped)", "레벨 이름 텍스트 (리치 텍스트 제거)"),
            E("State", "状态文本", "State text", "상태 텍스트"),
            E("StateColor", "当前状态的配色 hex（等待/自动播放/完美游玩…），用法 <color=#{StateColor}>", "Hex of the current state's colour: <color=#{StateColor}>", "현재 상태의 색상 hex: <color=#{StateColor}>"),
            E("IsAuto", "是否自动播放（True/False）", "Autoplay enabled (True/False)", "오토 플레이 여부 (True/False)"),
            E("IsNoFail", "是否无失败模式", "No-fail enabled", "노페일 여부"),
            E("IsPractice", "是否练习模式", "Practice mode enabled", "연습 모드 여부"),
            E("IsPaused", "是否暂停中", "Game paused", "일시정지 여부")),

        new("特效与状态", "Effects & Meta", "이펙트와 메타",
            E("ColorRange", "按数值在两色间渐变，输出 hex；第 8 参数 atMax 为顶格色：ColorRange(XAccuracy,0,100,FF00FF,FFFFFF,,,FFD700)", "Interpolate between two colours by a tag value, returns hex; 8th arg atMax = colour at value >= max", "태그 값에 따라 두 색 사이를 보간해 hex 반환; 8번째 인수 atMax는 최대값 도달 시 색"),
            E("EasedValue", "数值缓动跟随：EasedValue(Progress,2,500,OutExpo)", "Eased follow of a tag value: EasedValue(Progress,2,500,OutExpo)", "태그 값을 부드럽게 따라감: EasedValue(Progress,2,500,OutExpo)"),
            E("MovingMan", "数值变化瞬间的脉冲缓动：MovingMan(Combo,30,60,30,300)；speed 可省略（默认 250）", "Pulse between two sizes when a tag changes: MovingMan(Combo,30,60,30,300); speed optional (default 250)", "값이 변하는 순간 두 크기 사이를 이동: MovingMan(Combo,30,60,30,300); speed 생략 가능(기본 250)"),
            E("FrameTime", "平均帧耗时（毫秒）；可带窗口参数：FrameTime(500) 每 500ms 更新一次", "Average frame time (ms); optional window: FrameTime(500) refreshes every 500ms", "평균 프레임 시간 (ms); 창 인수 지원: FrameTime(500)은 500ms마다 갱신"),
            E("SessionAttempts", "本次游戏会话的尝试次数", "Attempts started this game session", "이 게임 세션의 시도 횟수"),
            E("Difficulty", "难度（本地化名）", "Difficulty (localized)", "난이도 (현지화)"),
            E("DifficultyRaw", "难度（英文枚举名）", "Difficulty (raw enum name)", "난이도 (영문)"),
            E("LastXScore", "上一次结算口径的 XScore", "Last XScore (results accounting)", "마지막 XScore"),
            E("MaxXAccuracy", "剩余全 XPerfect 可达的 X 准确率上限（%）", "Max reachable XAccuracy if all remaining tiles are XPerfect (%)", "남은 칸을 전부 X완벽으로 맞췄을 때의 X정확도 상한 (%)"),
            E("MaxXAccuracy01", "同上（0-1）", "Same as MaxXAccuracy (0-1)", "MaxXAccuracy와 동일 (0-1)"),
            E("MarginCombo", "当前连击段：MarginCombo(XP|A) 只数 XPerfect 与 Auto（支持缩写/全名，| 分隔）", "Current combo restricted to judgements: MarginCombo(XP|A) (aliases or full names, | separated)", "지정 판정만 세는 현재 콤보: MarginCombo(XP|A) (약어/전체 이름, | 구분)"),
            E("MarginMaxCombo", "本次尝试中该判定集合的最大连击段", "Best run of that judgement set this run", "이번 런에서 해당 판정 집합의 최대 콤보")),

        new("工具", "Utility", "유틸리티",
            E("Newline", "换行符", "Newline character", "줄바꿈 문자"),
            E("PColor", "玩家颜色 hex（不含 #），用法 <color=#{PColor(1)}>", "Player colour hex (no #): <color=#{PColor(1)}>", "플레이어 색상 hex (# 없이): <color=#{PColor(1)}>")),
    };

    /// <summary>
    /// 现成示例模板：设置面板「示例」行一键复制到剪贴板，粘贴进对应槽位即可用。
    /// Title 为按钮文案（悬停 Tooltip 显示完整模板），三语跟随设置语言。
    /// </summary>
    internal static readonly Example[] Examples =
    {
        new("进度渐变", "Progress gradient", "진행도 그라데이션",
            "<color=#{ColorRange(Progress,0,100,FF0000,00FF00,OutExpo)}>{EasedValue(Progress,2):0.0}%</color>"),
        new("判定行", "Judgement line", "판정 줄",
            "<color=white>判定 </color><color=red>{FailOverload}</color> {TooEarly} {VeryEarly} <color=#60FF4E>{Perfect}</color> {VeryLate} {TooLate} {FailMiss}"),
        new("连击+最大", "Combo + max", "콤보+최대",
            "<color=white>Combo </color>{Combo} <color=#888888>(max {MaxCombo})</color>"),
        new("曲名曲师", "Title & artist", "곡 제목·아티스트",
            "{Title} <color=#888888>- {Artist}</color>"),
        new("判定连击", "Judgement combo", "판정 콤보",
            "XP {MarginCombo(XP)} (max {MarginMaxCombo(XP)})"),
        new("连击弹跳", "Combo bounce", "콤보 바운스",
            "<size={MovingMan(Combo,140,108,108,300)}>{Combo}</size>"),
    };

    /// <summary>按名字查说明；未登记返回 null（面板显示「未登记说明」）。</summary>
    internal static string Describe(string name)
    {
        foreach (var group in Groups)
            foreach (var item in group.Items)
                if (item.Name == name) return item.Desc;
        return null;
    }

    /// <summary>表里登记过的名字集合，用于与 TagManager 的实际注册表对账。</summary>
    internal static HashSet<string> AllNames()
    {
        var set = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var group in Groups)
            foreach (var item in group.Items)
                set.Add(item.Name);
        return set;
    }
}
