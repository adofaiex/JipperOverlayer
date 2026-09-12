namespace JipperOverlayer.Overlayer;

/// <summary>Timing 文本的显示模式（借鉴 JipperResourcePack V1.5）。</summary>
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