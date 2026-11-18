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

    private static readonly Dictionary<MaterialKey, CachedMat> MaterialCache = new();

    /// <summary>缓存的材质连同它派生自的字体材质。TMP 在写入含新字形（尤其 CJK fallback）的
    /// 文本后会重建字体图集/材质，font.material 换成新对象；旧缓存材质虽然还在表里，但已经
    /// 基于过期材质，继续用会显示成字体默认样子（丢失阴影/描边）。命中时校验源材质是否仍是
    /// 当前 font.material，变了就重建。</summary>
    private readonly struct CachedMat
    {
        public readonly Material Mat;
        public readonly Material SourceFontMat;
        public CachedMat(Material mat, Material source) { Mat = mat; SourceFontMat = source; }
    }
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

    /// <summary>确保 text 用的是该特效对应的材质；若被 TMP 换掉，就丢弃旧缓存项并用当前
    /// font.material 重建。判据是「text 当前材质 != 期望材质」，不依赖 font.material 引用是否变化。
    /// 正常态只是一次引用比较，零开销。</summary>
    public static void EnsureMaterial(TextMeshProUGUI text, TextEffectConfig fx)
    {
        try
        {
            var font = text.font;
            if (font == null) return;
            if (fx == null) fx = Main.Settings?.TextEffects;
            bool shadowOn = fx != null && fx.ShadowEnabled;
            bool outlineOn = fx != null && fx.OutlineEnabled;

            if (!shadowOn && !outlineOn)
            {
                var plain = GetFontMaterial(font);
                if (plain != null && !ReferenceEquals(text.fontSharedMaterial, plain))
                    text.fontSharedMaterial = plain;
                return;
            }

            var key = new MaterialKey(font, fx);
            if (MaterialCache.TryGetValue(key, out var cached) && ReferenceEquals(text.fontSharedMaterial, cached.Mat))
                return;
            // 材质被 TMP 换掉或缓存项过期：用当前字体材质重建这一项。
            MaterialCache.Remove(key);
            Apply(text, fx);
        }
        catch (Exception e) { Loader.Warning($"Shadow Ensure error: {e.Message}"); }
    }

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

            var fontMatNow = GetFontMaterial(font);
            if (fontMatNow == null)
            {
                Loader.Warning($"Shadow: Cannot get material from font '{font.name}', skipping");
                return;
            }
            var key = new MaterialKey(font, fx);
            // 命中还要看源字体材质是否仍是当前这一个：TMP 重建图集后旧缓存材质已过期。
            if (!MaterialCache.TryGetValue(key, out var cached) || !ReferenceEquals(cached.SourceFontMat, fontMatNow))
            {
                var fontMat = fontMatNow;
                Material mat = new Material(fontMat);
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

                cached = new CachedMat(mat, fontMat);
                MaterialCache[key] = cached;
            }
            text.fontSharedMaterial = cached.Mat;
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
