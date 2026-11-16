using System.Globalization;
using UnityEngine;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 把内置渲染逐槽位导出为等价模板，一键填满全部模板字段。
/// 目的：从「和现在一模一样」的起点开始自定义，而不是从空白摸索。
/// 标签/分隔符取当前 LabelConfig，渐变色转成 {ColorRange}，小数位取当前设置值。
///
/// 已知偏差（静态模板表达不了的条件分支，导出后可在面板继续改）：
/// - 内置数值是 Math.Round（尾零省略），模板用固定小数格式（0.00 补齐尾零）；
/// - Accuracy/XAccuracy 刚好满命中转金色、XScore 满级转金：ColorRange 只有两端点，近似为整段渐变；
/// - Progress 的起始段「仅 StartTile&gt;0 才显示」：模板无条件输出，故不导出起始段；
/// - State 的按状态变色、Death/Timing 的红绿渐变（取绝对值映射）无法静态表达，值以白色导出；
/// - 判定行按 XPerfect 模式的配色导出（r148 无原生 XPerfect 时绿色不含 Auto）。
/// </summary>
internal static class BuiltinTemplateGenerator
{
    public static void FillAll(Settings s)
    {
        var L = s.Labels;
        var C = s.Colors;

        string progressGrad = Grad("Progress", 0, 100, C.Progress);
        string accGrad = Grad("Accuracy", 0, 100, C.Accuracy);
        string xaccGrad = Grad("XAccuracy", 0, 100, C.XAccuracy);
        string bestGrad = Grad("Best", 0, 100, C.Best);
        string bpmGrad = Grad("TileBpm", 0, s.BpmColorMax, C.Bpm);
        string comboGrad = Grad("Combo", 0, s.ComboColorMax, C.Combo);

        // 数值 tag 已按各自槽位的小数位设置取整（内置同款 Math.Round，尾零省略），
        // 模板不再写死格式——调小数位滑条时模板跟着变。
        L.ProgressTemplate = Head(L.Progress, L.ProgressSeparator) + Wrap(progressGrad, "{Progress}%");
        L.AccuracyTemplate = Head(L.Accuracy, L.AccuracySeparator) + Wrap(accGrad, "{Accuracy}%");
        L.PotAccuracyTemplate = Head("P." + L.Accuracy, L.AccuracySeparator) + WrapGold(accGrad, C.Accuracy, "{PotentialAccuracy}%");
        L.XAccuracyTemplate = Head(L.XAccuracy, L.XAccuracySeparator) + WrapGold(xaccGrad, C.XAccuracy, "{XAccuracy}%");
        L.PotXAccuracyTemplate = Head("P." + L.XAccuracy, L.XAccuracySeparator) + WrapGold(xaccGrad, C.XAccuracy, "{PotentialXAccuracy}%");
        // XScore：内置配色 t≥1 转 PerfectColor 金——用 XScoreRatio + atMax 参数复刻。
        L.XScoreTemplate = Head("XScore", L.XScoreSeparator) + WrapGold(Grad("XScoreRatio", 0, 100, C.XScore), C.XScore, "{XScore} (MAX-{XScoreLoss})");
        L.PotXScoreTemplate = Head("P.XScore", L.XScoreSeparator) + "{PotentialXScore}";
        L.MusicTimeTemplate = Head(L.MusicTime, L.MusicTimeSeparator) + "{MusicTime}~{MusicTotal}";
        L.MapTimeTemplate = Head(L.MapTime, L.MapTimeSeparator) + "{MapTime}~{MapTotal}";
        L.CheckpointTemplate = Head(L.Checkpoint, L.CheckpointSeparator) + "{CheckpointsUsed} ({CheckpointPassed}/{CheckpointsTotal})";
        L.BestTemplate = Head(L.Best, L.BestSeparator) + Wrap(bestGrad, "{Best}%");
        L.TbpmTemplate = Head(L.TBPM, L.TBPMSeparator) + Wrap(bpmGrad, "{TileBpm}");
        L.CbpmTemplate = Head(L.CBPM, L.CBPMSeparator) + "{CurBpm}";
        L.KpsTemplate = Head(L.KPS, L.KPSSeparator) + "{Kps}";
        L.AttemptTemplate = L.Attempt + " {Attempt}";
        L.FullAttemptTemplate = L.FullAttempt + " {FullAttempt}";
        L.TimingScaleTemplate = Head(L.TimingScale, L.TimingScaleSeparator) + "{TimingScale}%";
        // 判定行：与内置 AppendJudgementLine 的嵌套色结构逐字同构，核心完美按用户偏好拆成三分量
        // （-P 绿 / X 白（跟随游戏 colourXPerfect） / +P 绿，即结算界面明细样式）。r148 无原生
        // XPerfect 时三分量 tag 均为 0，仍可正常显示。
        L.JudgementTemplate = "{FailOverload} <color=red>{TooEarly} <color=#FF6F4E>{VeryEarly} <color=#A0FF4E>{EarlyPerfect} <color=#60FF4E>{PerfectMinus}</color> <color=#FFFFFF>{XPerfect}</color> <color=#60FF4E>{PerfectPlus}</color> {LatePerfect}</color> {VeryLate}</color> {TooLate}</color> {FailMiss}";
        L.ComboTemplate = Wrap(comboGrad, "{Combo}");
        L.TimingWindowTemplate = XPerfectWindow(L) + WindowLine("#60FF4E", L.PerfectLabel, "{PerfectMs}") + WindowLine("#A0FF4E", L.GreatLabel, "{GreatMs}") + WindowLine("#FF6F4E", L.GoodLabel, "{GoodMs}");
        // 静态颜色（非 ColorRange 表达式）不能走 Wrap——Wrap 会把参数包进 {…} 当 tag 求值，
        // 纯 hex 会被当成未知 tag 原样留在行内（日志里「未知 tag {FFFFFF}」即此问题）。
        L.FpsTemplate = Head(L.FPS, L.FPSSeparator) + "<color=#" + Hex(C.JFps) + ">{Fps:0.00}</color>";
        L.AuthorTemplate = Head(L.Author, L.AuthorSeparator) + "<color=#" + Hex(C.JAuthor) + ">{Author}</color>";
        L.StateTemplate = Head(L.State, L.StateSeparator) + "<color=#{StateColor}>{State}</color>";
        L.DeathTemplate = Head(L.Death, L.DeathSeparator) + "{Death}";
        L.StartTemplate = Head(L.Start, L.StartSeparator) + "<color=#" + Hex(C.JStart) + ">{StartTile} ({StartProgress:0.00}%)</color>";
        L.TimingTemplate = Head(L.Timing, L.TimingSeparator) + "{Timing} ({AvgTiming})";
        L.AvgTimingTemplate = Head(L.Timing + "(Avg)", L.TimingSeparator) + "{AvgTiming}";
        L.HitOffsetTemplate = Head(L.HitOffset, L.HitOffsetSeparator) + "{HitOffset}";
    }

    // 判定时间窗：BuildTimingWindowLines 已在模板结果前补一个换行，
    // 所以第一行不带 {Newline}，后续行用 {Newline} 分隔。
    private static string XPerfectWindow(LabelConfig L)
    {
        return "<color=#" + GameCompat.XPerfectHex + ">" + L.XPerfectLabel + "</color>" + L.TimingWindowSeparator + "±{XPerfectMs}ms";
    }

    private static string WindowLine(string hex, string label, string msTag)
    {
        return "{Newline}<color=" + hex + ">" + label + "</color>" + Main.Settings.Labels.TimingWindowSeparator + "±" + msTag + "ms";
    }

    private static string Head(string label, string sep)
    {
        return "<color=white>" + label + sep + "</color>";
    }

    private static string Wrap(string grad, string value)
    {
        return "<color=#{" + grad + "}>" + value + "</color>";
    }

    /// <summary>带顶格色的包装：把 atMax 参数插入 ColorRange 的闭合括号之前
    /// （Grad 产物自带「)」，直接往后拼会把参数落到括号外，tag 解析成无名参数全空 → 返回空串）。</summary>
    private static string WrapGold(string grad, ColorPerDictionary d, string value)
    {
        int close = grad.LastIndexOf(')');
        // 三个逗号 = 两个空位（ease、maxLength 走默认值），第 8 个实参是 atMax 色。
        // 之前拼了四个逗号产生 9 个参数，超过 ColorRange 的 8 个形参，被「参数过多」拒绝。
        string injected = close >= 0 ? grad.Insert(close, ",,," + GoldHex(d)) : grad;
        return "<color=#{" + injected + "}>" + value + "</color>";
    }

    private static string GoldHex(ColorPerDictionary d)
    {
        // 内置满命中金是 ColorConfig.SettingsGold 常量（刻意不受全局一键染色影响，
        // 见 commit 31a895e），这里必须用同一常量——读 PerfectColor 会被用户的
        // 「应用到所有」染成别的颜色（本次红色就是这么来的）。
        return "FFDA00"; // = ColorUtility.ToHtmlStringRGB(new Color(1f, 0.8549f, 0f))
    }

    private static string Fmt(int decimals)
    {
        return decimals <= 0 ? "0" : "0." + new string('0', decimals);
    }

    private static string Grad(string tag, double min, double max, ColorPerDictionary d)
    {
        // GetHex 直接返回不带 # 的 RGB hex（alpha=false）。
        return "ColorRange(" + tag + "," + Inv(min) + "," + Inv(max) + "," + d.GetHex(0f) + "," + d.GetHex(1f) + ")";
    }

    private static string Inv(double v)
    {
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Hex(ColorCache c)
    {
        return ColorUtility.ToHtmlStringRGB((Color)c);
    }
}
