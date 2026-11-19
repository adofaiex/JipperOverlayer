using System.Globalization;
using UnityEngine;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 把内置渲染逐槽位导出为等价模板，一键填满全部模板字段。
/// 目的：从「和现在一模一样」的起点开始自定义，而不是从空白摸索。
/// 标签取导入时的 LabelConfig，聚合行的分隔符在求值时读取；静态渐变色转成 {ColorRange}。
///
/// 已知偏差（静态模板表达不了的条件分支，导出后可在面板继续改）：
/// - 内置数值是 Math.Round（尾零省略）；聚合 tag 同样按小数位设置取整并省略尾零，
///   仅 Fps/Start 等少数静态模板保留固定 0.00 格式；
/// - State 的按状态变色、Death/Timing 的红绿渐变（取绝对值映射）无法静态表达，值以白色导出；
/// - 判定行按 XPerfect 模式的配色导出（r148 无原生 XPerfect 时绿色不含 Auto）。
/// </summary>
internal static class BuiltinTemplateGenerator
{
    public static void FillAll(Settings s)
    {
        var L = s.Labels;
        var C = s.Colors;

        string bestGrad = Grad("Best", 0, 100, C.Best);
        string bpmGrad = Grad("TileBpm", 0, s.BpmColorMax, C.Bpm);
        string comboGrad = Grad("Combo", 0, s.ComboColorMax, C.Combo);

        // 数值 tag 已按各自槽位的小数位设置取整（内置同款 Math.Round，尾零省略），
        // 模板不再写死格式——调小数位滑条时模板跟着变。
        // 聚合 tag 同时生成首分隔符与玩家间分隔符，游玩状态冻结时也保持一致。
        // 将进度色点写入模板；每个分段仍用自己的原始进度从这份色板取色。
        L.ProgressTemplate = Head(L.Progress, "") + "{ProgressDetailed(-1,true," + ProgressTemplateGradient.Export(C.Progress) + ")}";
        L.AccuracyTemplate = Head(L.Accuracy, "") + "{AccuracyRun(-1,-1,false,true)}";
        L.PotAccuracyTemplate = Head("P." + L.Accuracy, "") + "{AccuracyRun(-1,-1,true,true)}";
        L.XAccuracyTemplate = Head(L.XAccuracy, "") + "{XAccuracyRun(-1,-1,false,true)}";
        L.PotXAccuracyTemplate = Head("P." + L.XAccuracy, "") + "{XAccuracyRun(-1,-1,true,true)}";
        L.XScoreTemplate = Head("XScore", "") + "{XScoreRun(-1,false,true)}";
        L.PotXScoreTemplate = Head("P.XScore", "") + "{XScoreRun(-1,true,true)}";
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
