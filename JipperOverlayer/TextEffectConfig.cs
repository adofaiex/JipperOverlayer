using JipperOverlayer.Overlayer;
using UnityEngine;

namespace JipperOverlayer;

[System.Serializable]
public class TextEffectConfig
{
    // Underlay-based shadow.
    public bool ShadowEnabled = true;
    public float ShadowOffsetX = 1f;
    public float ShadowOffsetY = -1f;
    public ColorCache ShadowColor = new(new Color(0, 0, 0, 0.5f));
    public float ShadowSoftness = 0f;

    // TMP outline (Outline keyword).
    public bool OutlineEnabled = false;
    public float OutlineWidth = 0.01f;
    public float OutlineSoftness = 0f;
    public ColorCache OutlineColor = new(Color.black);

    /// <summary>深拷贝（开启某槽位的独立特效时从全局复制一份作为起点）。</summary>
    public TextEffectConfig Clone() => new TextEffectConfig
    {
        ShadowEnabled = ShadowEnabled,
        ShadowOffsetX = ShadowOffsetX,
        ShadowOffsetY = ShadowOffsetY,
        ShadowColor = ShadowColor != null ? new ColorCache((Color)ShadowColor) : null,
        ShadowSoftness = ShadowSoftness,
        OutlineEnabled = OutlineEnabled,
        OutlineWidth = OutlineWidth,
        OutlineSoftness = OutlineSoftness,
        OutlineColor = OutlineColor != null ? new ColorCache((Color)OutlineColor) : null,
    };
}

/// <summary>某个字体槽的独立文本特效。Override=false 时沿用全局 Settings.TextEffects。</summary>
[System.Serializable]
public class SlotTextEffectConfig
{
    public bool Override;
    public TextEffectConfig Effect = new();
}
