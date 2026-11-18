using System.Collections.Generic;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

/// <summary>
/// 玩家自定义的一行文本：模板 + 定位方式 + 字号。
/// 与 26 个固定槽位的区别是——它是**可增删的列表项**，每项在启动/配置变化时
/// 由 Overlay 动态创建一个 TextMeshProUGUI，模板按 ~10Hz 求值。
/// </summary>
public class CustomTextLine
{
    /// <summary>模板内容，语法同「文本模板」分区（{Tag} / {Tag:0.00} / {Tag(1)}）。</summary>
    public string Template = "";

    /// <summary>关闭后该行不显示，但保留配置。</summary>
    public bool Enabled = true;

    /// <summary>非游玩状态（菜单/结算/读图）也显示本行。任一行开启即点亮菜单画布（只渲染开启的行）。</summary>
    public bool ShowOutsidePlay = true;

    /// <summary>true = 并入主栈（跟随进度/准度那一列自动竖向排列）；false = 自由坐标。
    /// 默认自由：进栈行的父级是主容器，容器本身受 AnyStackedTextVisible 与非游玩显示模式
    /// 两处 SetActive 控制，任何一处判错都会让整行列（甚至整块）一起消失；
    /// 自由行直接挂在画布根下，不受主容器开关影响，所以新行默认自由。</summary>
    public bool InStack = false;

    /// <summary>进栈目标：0 = 主栈（进度/准度那一列），1 = BPM，2 = 判定，3 = 连击。
    /// 仅在 InStack 为真时有效。目标组本身是一个 RectTransform，行直接挂成它的子节点，
    /// 所以分组被拖动、改偏移或做动画时，行会跟着一起走。</summary>
    public int StackTarget;

    /// <summary>字号；0 = 跟随主区域字号。</summary>
    public int FontSize = 0;

    /// <summary>自由坐标时的偏移（画布参考单位，相对左上角主栈原点）。</summary>
    public float OffsetX;
    public float OffsetY;

    /// <summary>换行宽度：0 = 不换行（长文本保持单行、溢出绘制）；大于 0 = 按该宽度自动换行。
    /// 之前宽度硬编码成 456（照抄内置主列），长关卡名被切成两行且无从调整。</summary>
    public float WrapWidth = 456f;

    /// <summary>TMP 行距（像素，可为负）；0 = 字体默认。</summary>
    public float LineSpacing;

    /// <summary>字符间距（像素，TMP characterSpacing，字距/字间空白）；0 = 字体默认。</summary>
    public float CharSpacing;

    /// <summary>段落间距（像素，TMP paragraphSpacing）；0 = 字体默认。</summary>
    public float ParagraphSpacing;

    /// <summary>自动高度：true = 矩形高度跟随文本实际高度（换行成多行时对齐基准才正确）；
    /// false = 用 Height 固定，多行文本会以该高度为基准居中/顶对齐。</summary>
    public bool AutoHeight = true;

    /// <summary>固定高度（仅在 AutoHeight 关闭时生效）。</summary>
    public float Height = 30f;

    /// <summary>绕 Z 轴旋转角度（度）。</summary>
    public float RotZ;

    /// <summary>本行基础色：模板里未被 &lt;color&gt; 包裹的部分用这个颜色（内置颜色调节工具）。</summary>
    public ColorCache TextColor = new(Color.white);

    // ==== 每行独立的文本外观；-1 / false = 跟随主区域 ====

    /// <summary>字体索引（FontManager.FontNames 的下标）；-1 = 跟随主区域。</summary>
    public int FontIndex = -1;

    /// <summary>对齐方式（TextAlignmentOptions 的 int 值）；-1 = 跟随主区域。</summary>
    public int Align = -1;

    /// <summary>字体样式（FontStyles 位标志）；-1 = 跟随主区域，0 = 常规。</summary>
    public int Style = -1;

    /// <summary>true = 该行使用独立的阴影/描边配置（Effect）；false = 跟随主槽位特效。</summary>
    public bool EffectOverride;
    public TextEffectConfig Effect = new();

    /// <summary>手改 JSON 造成的 null 字段回填。</summary>
    public void Normalize()
    {
        Template ??= "";
        // 手改 JSON 可能填了越界下标：渲染层按当前字体表取值时再做边界保护，
        // 这里只把明显非法的负值（-1 以外）拉回跟随主区域。
        if (FontIndex < -1) FontIndex = -1;
        if (FontSize < 0) FontSize = 0;
        if (StackTarget < 0 || StackTarget > 3) StackTarget = 0;
        if (WrapWidth < 0f) WrapWidth = 0f;
        if (WrapWidth > 4000f) WrapWidth = 4000f;
        if (LineSpacing < -100f) LineSpacing = -100f;
        if (LineSpacing > 200f) LineSpacing = 200f;
        if (CharSpacing < -100f) CharSpacing = -100f;
        if (CharSpacing > 200f) CharSpacing = 200f;
        if (ParagraphSpacing < -100f) ParagraphSpacing = -100f;
        if (ParagraphSpacing > 200f) ParagraphSpacing = 200f;
        if (Height < 1f) Height = 1f;
        if (Height > 2000f) Height = 2000f;
        if (RotZ < -180f) RotZ = -180f;
        if (RotZ > 180f) RotZ = 180f;
        Effect ??= new TextEffectConfig();
        TextColor ??= new ColorCache(Color.white);
    }

    public static void NormalizeAll(List<CustomTextLine> lines)
    {
        if (lines == null) return;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i] == null) lines.RemoveAt(i);
            else lines[i].Normalize();
        }
    }
}
