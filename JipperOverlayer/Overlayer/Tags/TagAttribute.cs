using System;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>tag 在游玩状态变化时的行为（对应参考工程 Overlayer 的 TagType 位标志）。</summary>
[Flags]
internal enum TagState
{
    /// <summary>不参与门控：任何时候都照常求值。</summary>
    None = 0,
    /// <summary>不在游玩中（菜单/结算/读图）时冻结——沿用上一次求到的值。</summary>
    BlockOnNotPlaying = 1,
    /// <summary>暂停时冻结。</summary>
    BlockOnPaused = 2,
    BlockOnAll = BlockOnNotPlaying | BlockOnPaused,
}

/// <summary>标记一个静态方法为可用 tag。方法需为静态；参数可带默认值（模板里未写的参数取默认值）。</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class TagAttribute : Attribute
{
    /// <summary>模板中使用的 tag 名（区分大小写，如 "Accuracy"）。</summary>
    public string Name { get; }

    /// <summary>游玩状态门控。默认按「游戏数值」处理：离开关卡或暂停时冻结。</summary>
    public TagState State { get; }

    public TagAttribute(string name, TagState state = TagState.BlockOnAll)
    {
        Name = name;
        State = state;
    }
}
