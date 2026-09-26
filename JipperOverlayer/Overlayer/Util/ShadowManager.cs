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

    private static readonly Dictionary<TMP_FontAsset, Material> MaterialCache = new();
    private static System.Reflection.MemberInfo _cachedMaterialMember;
    private static bool _cachedMaterialLogged;

    public static void ClearCache() => MaterialCache.Clear();

    public static void ApplyShadow(TextMeshProUGUI text) => Apply(text);

    // Apply the global text-effect settings (shadow + outline) to a text. Parameters come from
    // Main.Settings.TextEffects so all overlay texts share one configurable look. Material is
    // cached per font; callers must ClearCache() when the settings change so the cached material
    // is rebuilt with the new parameters.
    private static void Apply(TextMeshProUGUI text)
    {
        try
        {
            var font = text.font;
            if (font == null) return;

            var fx = Main.Settings?.TextEffects;
            bool shadowOn = fx != null && fx.ShadowEnabled;
            bool outlineOn = fx != null && fx.OutlineEnabled;

            // Nothing requested: leave the text on its plain font material.
            if (!shadowOn && !outlineOn)
            {
                var plain = GetFontMaterial(font);
                if (plain != null) text.fontSharedMaterial = plain;
                return;
            }

            if (!MaterialCache.TryGetValue(font, out var mat))
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

                MaterialCache[font] = mat;
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
