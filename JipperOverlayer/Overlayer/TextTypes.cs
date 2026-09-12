namespace JipperOverlayer.Overlayer;

using JipperOverlayer.Overlayer.Localization;

/// <summary>Timing 文本的显示模式。</summary>
public enum TimingTextType
{
    /// <summary>只显示最近一次打击的毫秒偏移</summary>
    Timing,
    /// <summary>只显示累计平均值</summary>
    AvgTiming,
    /// <summary>两个独立文本行</summary>
    Both,
    /// <summary>一行双值：本次 (平均)</summary>
    BothInOneLine,
}

/// <summary>XScore 文本的显示模式（r149+）。</summary>
public enum XScoreTextType
{
    /// <summary>纯分数</summary>
    Value,
    /// <summary>x / max</summary>
    WithMax,
    /// <summary>x (MAX-n)</summary>
    MaxMinus,
}

/// <summary>带潜力值的文本（精度/X精度/XScore）的显示模式。</summary>
public enum PotentialTextType
{
    /// <summary>只显示当前值</summary>
    Current,
    /// <summary>只显示潜力值（剩余全部满分时的最终值）</summary>
    Potential,
    /// <summary>当前值与潜力值各占一行</summary>
    Both,
    /// <summary>一行双值：当前 (潜力)</summary>
    BothInOneLine,
}

/// <summary>设置界面里各文本类型枚举的本地化显示名。
/// 原先按钮循环显示的是英文原始枚举名（Current/Both/WithMax...），
/// 用户无从知道每个值是什么意思——这里统一换成带含义的本地化名称。</summary>
public static class TextTypeNames
{
    public static string Name(PotentialTextType t) => t switch
    {
        PotentialTextType.Current => Tr.Get(Tr.Key.PotCur),
        PotentialTextType.Potential => Tr.Get(Tr.Key.PotPot),
        PotentialTextType.Both => Tr.Get(Tr.Key.PotBoth),
        _ => Tr.Get(Tr.Key.PotBothOne),
    };

    public static string Name(XScoreTextType t) => t switch
    {
        XScoreTextType.WithMax => Tr.Get(Tr.Key.XfmtWithMax),
        XScoreTextType.MaxMinus => Tr.Get(Tr.Key.XfmtMaxMinus),
        _ => Tr.Get(Tr.Key.XfmtValue),
    };

    public static string Name(TimingTextType t) => t switch
    {
        TimingTextType.AvgTiming => Tr.Get(Tr.Key.TmAvg),
        TimingTextType.Both => Tr.Get(Tr.Key.TmBoth),
        TimingTextType.BothInOneLine => Tr.Get(Tr.Key.TmBothOne),
        _ => Tr.Get(Tr.Key.TmHit),
    };
}