using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Util;

internal static class ShadowManager
{
    internal static readonly Shader ShaderRef = ProbeShaderRef();

    static Shader ProbeShaderRef()
    {
        try
        {
            return PatchManager.CreateStaticPropertyGetter<Shader>(typeof(ShaderUtilities), "ShaderRef_MobileSDF")();
        }
        catch (Exception e)
        {
            // 旧/新 Unity 版本可能改名/移除该属性；阴影应退化为普通字体材质，
            // 不能让 ShadowManager 的静态初始化直接中断 Overlay 构造。
            Loader.Warning($"Shadow: ShaderRef_MobileSDF unavailable ({e.Message})");
            return null;
        }
    }

    private static readonly Dictionary<MaterialKey, Material> MaterialCache = new();
    private static System.Reflection.MemberInfo _cachedMaterialMember;
    private static bool _cachedMaterialLogged;

    public static void ClearCache() => MaterialCache.Clear();

    /// <summary>材质缓存键：字体 + 特效参数。同一字体在不同槽位可以用不同特效，
    /// 所以键必须带上参数，否则后应用的那个槽位会覆盖前一个的材质。</summary>
    private readonly struct MaterialKey : IEquatable<MaterialKey>
    {
        private readonly TMP_FontAsset _font;
        private readonly bool _shadowOn, _outlineOn;
        private readonly float _offX, _offY, _softness, _outlineWidth, _outlineSoftness;
        private readonly Color _shadowColor, _outlineColor;

        public MaterialKey(TMP_FontAsset font, TextEffectConfig fx)
        {
            _font = font;
            _shadowOn = fx != null && fx.ShadowEnabled;
            _outlineOn = fx != null && fx.OutlineEnabled;
            _offX = fx?.ShadowOffsetX ?? 0f;
            _offY = fx?.ShadowOffsetY ?? 0f;
            _softness = fx?.ShadowSoftness ?? 0f;
            _outlineWidth = fx?.OutlineWidth ?? 0f;
            _outlineSoftness = fx?.OutlineSoftness ?? 0f;
            _shadowColor = fx?.ShadowColor != null ? (Color)fx.ShadowColor : Color.clear;
            _outlineColor = fx?.OutlineColor != null ? (Color)fx.OutlineColor : Color.clear;
        }

        public bool Equals(MaterialKey other)
            => ReferenceEquals(_font, other._font)
               && _shadowOn == other._shadowOn && _outlineOn == other._outlineOn
               && _offX.Equals(other._offX) && _offY.Equals(other._offY)
               && _softness.Equals(other._softness)
               && _outlineWidth.Equals(other._outlineWidth)
               && _outlineSoftness.Equals(other._outlineSoftness)
               && _shadowColor.Equals(other._shadowColor) && _outlineColor.Equals(other._outlineColor);

        public override int GetHashCode()
        {
            // net481 没有 HashCode.Combine，手动混合。
            unchecked
            {
                int hash = _font != null ? _font.GetHashCode() : 0;
                hash = (hash * 397) ^ (_shadowOn ? 1 : 0);
                hash = (hash * 397) ^ (_outlineOn ? 1 : 0);
                hash = (hash * 397) ^ _offX.GetHashCode();
                hash = (hash * 397) ^ _offY.GetHashCode();
                hash = (hash * 397) ^ _softness.GetHashCode();
                hash = (hash * 397) ^ _outlineWidth.GetHashCode();
                hash = (hash * 397) ^ _outlineSoftness.GetHashCode();
                hash = (hash * 397) ^ _shadowColor.GetHashCode();
                hash = (hash * 397) ^ _outlineColor.GetHashCode();
                return hash;
            }
        }
    }

    public static void ApplyShadow(TextMeshProUGUI text) => Apply(text, Main.Settings?.TextEffects);

    /// <summary>按给定特效配置应用阴影/描边。fx 为 null 时退回全局配置。</summary>
    public static void ApplyShadow(TextMeshProUGUI text, TextEffectConfig fx) => Apply(text, fx);

    // Apply the text-effect settings (shadow + outline) to a text. The material is cached per
    // (font, effect parameters); callers must ClearCache() when the settings change so the cached
    // material is rebuilt with the new parameters.
    private static void Apply(TextMeshProUGUI text, TextEffectConfig fx)
    {
        try
        {
            var font = text.font;
            if (font == null) return;

            if (fx == null) fx = Main.Settings?.TextEffects;
            bool shadowOn = fx != null && fx.ShadowEnabled;
            bool outlineOn = fx != null && fx.OutlineEnabled;

            // Nothing requested: leave the text on its plain font material.
            if (!shadowOn && !outlineOn)
            {
                var plain = GetFontMaterial(font);
                if (plain != null) text.fontSharedMaterial = plain;
                return;
            }

            var key = new MaterialKey(font, fx);
            if (!MaterialCache.TryGetValue(key, out var mat))
            {
                var fontMat = GetFontMaterial(font);
                if (fontMat == null)
                {
                    Loader.Warning($"Shadow: Cannot get material from font '{font.name}', skipping");
                    return;
                }
                mat = new Material(fontMat);
                if (ShaderRef != null) mat.shader = ShaderRef;

                if (shadowOn)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    mat.SetColor(ShaderUtilities.ID_UnderlayColor, fx.ShadowColor);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, fx.ShadowOffsetX);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, fx.ShadowOffsetY);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
                    mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, fx.ShadowSoftness);
                }

                if (outlineOn)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, fx.OutlineColor);
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, fx.OutlineWidth);
                    mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, fx.OutlineSoftness);
                }

                MaterialCache[key] = mat;
            }
            text.fontSharedMaterial = mat;
        }
        catch (Exception e) { Loader.Warning($"Shadow error: {e.Message}"); }
    }

    private static Material GetFontMaterial(TMP_FontAsset font)
    {
        if (_cachedMaterialMember == null)
        {
            var t = font.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            _cachedMaterialMember = (System.Reflection.MemberInfo)t.GetProperty("material", flags) ?? t.GetField("material", flags);
        }

        Material result = null;
        if (_cachedMaterialMember is System.Reflection.PropertyInfo pi)
        {
            var val = pi.GetValue(font);
            if (val != null) result = (Material)val;
        }
        else if (_cachedMaterialMember is System.Reflection.FieldInfo fi)
        {
            var val = fi.GetValue(font);
            if (val != null) result = (Material)val;
        }

        if (!_cachedMaterialLogged)
        {
            _cachedMaterialLogged = true;
            string foundBy = _cachedMaterialMember != null
                ? $"{_cachedMaterialMember.MemberType} \"{_cachedMaterialMember.Name}\""
                : "none";
            Loader.Log($"Overlay: Font material resolved via {foundBy}");
        }
        return result;
    }
}
