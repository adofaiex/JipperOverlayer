using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JipperOverlayer.Overlayer.Localization;
using TMPro;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public static class FontManager
{
    public class FontEntry
    {
        public string name;
        public TMP_FontAsset font;
        public string sourceFontName;
        /// <summary>模组自己创建/拥有的字体资产（可安全做字号缩放与字形烘焙）。
        /// 直接借用游戏共享 fontTMP 的条目为 false——改它的 faceInfo 或往图集塞字形会影响游戏 UI。
        /// / A font asset created/owned by the mod (safe to size-scale and bake glyphs into).
        /// An entry that borrows the game's shared fontTMP is false — mutating its faceInfo or
        /// atlas would affect the game's own UI.</summary>
        public bool Owned = true;
    }

    public static List<FontEntry> FontList;
    public static string[] FontNames;

    public static void ScanFonts()
    {
        FontList = [];

        // 1. 内嵌默认字体 / Embedded default font
        if (AssetLoader.FontAsset != null)
            FontList.Add(new FontEntry { name = "Default Font", font = AssetLoader.FontAsset, sourceFontName = "Default Font" });

        // 2. Game Font objects — convert to TMP (skips fonts with path-like names from other mods)
        var allFonts = Resources.FindObjectsOfTypeAll<Font>();
        foreach (var f in allFonts)
        {
            if (f == null || string.IsNullOrEmpty(f.name)) continue;
            if (f.name.Contains("\\") || f.name.Contains("/")) continue;
            bool exists = false;
            foreach (var e in FontList)
                if (e.sourceFontName == f.name) { exists = true; break; }
            if (exists) continue;
            try
            {
                var tmpFont = TMP_FontAsset.CreateFontAsset(f);
                if (tmpFont != null)
                    FontList.Add(new FontEntry { name = f.name, font = tmpFont, sourceFontName = f.name });
            }
            catch (Exception e) { Loader.Warning($"FontManager: Skip game font '{f.name}': {e.Message}"); }
        }

        // 3. Custom fonts from CustomFonts directory
        try
        {
            string customDir = Path.Combine(Loader.ModPath, "CustomFonts");
            if (!Directory.Exists(customDir))
                Directory.CreateDirectory(customDir);
            ScanCustomDir(customDir, "*.ttf");
            ScanCustomDir(customDir, "*.otf");
        }
        catch (Exception e) { Loader.Warning($"CustomFonts: {e.Message}"); }

        AddAdofaiFontEntry();

        FontNames = new string[FontList.Count];
        for (int i = 0; i < FontList.Count; i++)
            FontNames[i] = FontList[i].name;

        // Resolve saved font name → index (handles list changes between sessions)
        if (Main.Settings != null && !string.IsNullOrEmpty(Main.Settings.FontName))
        {
            int idx = FindFontIndex(Main.Settings.FontName);
            if (idx >= 0) Main.Settings.FontIndex = idx;
            else Main.Settings.FontIndex = 0;
        }

        // Link CJK fallback
        TMP_FontAsset cjk = null;
        try { cjk = RDConstants.data.chineseFontTMPro; } catch { }
        if (cjk != null)
        {
            foreach (var entry in FontList)
            {
                if (entry.font == null || entry.font == cjk || !entry.Owned) continue;
                entry.font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!entry.font.fallbackFontAssetTable.Contains(cjk))
                    entry.font.fallbackFontAssetTable.Add(cjk);
            }
        }

        Loader.Log($"FontManager: {FontList.Count} fonts");
        BakeOverlayGlyphs();
        MatchFontSizes();
    }

    static void AddAdofaiFontEntry()
    {
        try
        {
            var tmp = RDString.fontData.fontTMP;
            if (tmp == null) return;
            foreach (var e in FontList)
                if (e.font == tmp || e.name == "ADOFAI Font") return; // 同资产或同名行都跳过，避免选择器出现两行一样的名字
            FontList.Add(new FontEntry { name = "ADOFAI Font", font = tmp, sourceFontName = "ADOFAI Font", Owned = false });
            Loader.Log("FontManager: added ADOFAI Font (borrowed, not owned)");
        }
        catch (Exception e) { Loader.Warning($"FontManager: ADOFAI font unavailable: {e.Message}"); }
    }

    static void MatchFontSizes()
    {
        // r136 的 TextCore 没有 FaceInfo 属性，直接访问会 MissingMethod/Property；
        // 用版本哨兵跳过，旧版本不做字号匹配。
        if (!VersionSafe.IsV141OrLater) return;
        var reference = AssetLoader.FontAsset;
        if (reference == null) return;
        float refRatio;
        try { refRatio = GetCapHeightRatio(reference); } catch { return; }
        if (refRatio <= 0) return;
        float refScale;
        try { refScale = reference.faceInfo.scale; } catch { refScale = 1f; }
        if (refScale <= 0) refScale = 1f;

        foreach (var entry in FontList)
        {
            if (entry == null || entry.font == null || !entry.Owned) continue;
            if (entry.font == reference) continue;
            float cap;
            try { cap = GetCapHeightRatio(entry.font); } catch { continue; }
            if (cap <= 0) continue;
            float target = refRatio * refScale / cap;
            target = Mathf.Clamp(target, 0.5f, 2f);
            try
            {
                var fi = entry.font.faceInfo;
                if (Math.Abs(fi.scale - target) < 0.001f) continue;
                fi.scale = target;
                entry.font.faceInfo = fi;
                Loader.Log($"FontManager: scaled '{entry.name}' x{target:F3} (cap {cap:F4} -> ref {refRatio:F4})");
            }
            catch (Exception e) { Loader.Warning($"FontManager: scale '{entry.name}' failed: {e.Message}"); }
        }
    }

    static float GetCapHeightRatio(TMP_FontAsset font)
    {
        var fi = font.faceInfo;
        if (fi.pointSize <= 0) return 0;
        float cap = fi.capLine;
        if (cap <= 0 && font.HasCharacter('H', false, true) && font.characterLookupTable.TryGetValue('H', out var ch) && ch?.glyph != null)
            cap = ch.glyph.metrics.horizontalBearingY;
        return cap > 0 ? cap / fi.pointSize : 0;
    }

    static void ScanCustomDir(string dir, string pattern)
    {
        foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly))
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string entryName = "Custom: " + fileName;

                bool exists = false;
                foreach (var e in FontList)
                    if (e.name.Equals(entryName, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (exists)
                {
                    Loader.Log($"FontManager: Custom font '{fileName}' already loaded, skipping");
                    continue;
                }

                Font font = new Font(file);
                TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(font);
                if (tmpFont != null)
                {
                    FontList.Add(new FontEntry { name = entryName, font = tmpFont, sourceFontName = fileName });
                    Loader.Log($"FontManager: Loaded custom font '{fileName}'");
                }
                else
                {
                    Loader.Warning($"FontManager: Failed to create TMP_FontAsset from '{fileName}'");
                }
            }
            catch (Exception e) { Loader.Warning($"FontManager: Skip {Path.GetFileName(file)}: {e.Message}"); }
        }
    }

    public static TMP_FontAsset GetFont(int index)
    {
        if (FontList == null || index < 0 || index >= FontList.Count)
            return AssetLoader.FontAsset;
        return FontList[index].font ?? AssetLoader.FontAsset;
    }

    public static int FindFontIndex(string fontName)
    {
        if (string.IsNullOrEmpty(fontName) || FontList == null) return 0;
        for (int i = 0; i < FontList.Count; i++)
            if (FontList[i].name == fontName) return i;
        return 0;
    }

    // ===== Dynamic glyph pre-baking =====
    // Every TMP font here is a dynamic atlas; the first render of an unbaked glyph
    // triggers a main-thread re-rack + texture rebuild mid-game. Baking the bounded
    // overlay charset up front (labels in all 3 languages + ASCII) removes those
    // runtime spikes on boot and on language switches. Chars a font can't produce
    // (e.g. Hangul in a Latin-only ttf) simply stay unbaked and fall back normally.

    public static void BakeOverlayGlyphs()
    {
        try
        {
            string chars = CollectBakeChars();
            if (string.IsNullOrEmpty(chars)) return;

            if (!TryFindAddCharactersMethod(out MethodInfo method))
            {
                Loader.Warning("FontManager: no usable TMP.TryAddCharacters overload found, glyph baking skipped");
                return;
            }

            int baked = 0;
            if (FontList != null)
                foreach (var entry in FontList)
                    if (entry.Owned) // 借用的游戏共享字体不烘焙：往它图集里塞字形会影响游戏自身 UI
                        baked += BakeFont(entry.font, chars, method);
            try { baked += BakeFont(RDConstants.data.chineseFontTMPro, chars, method); } catch { } // shared CJK fallback
            Loader.Log($"FontManager: pre-baked {chars.Length} chars into {baked} font(s)");
        }
        catch (Exception e) { Loader.Warning($"FontManager: glyph baking failed: {e.Message}"); }
    }

    // TMP's TryAddCharacters signature varies across versions (string / uint[] / IEnumerable<char>,
    // each optionally with `out string` and/or a trailing bool). Enumerate overloads at runtime instead
    // of hard-coding one shape, preferring the string form when present.
    static bool TryFindAddCharactersMethod(out MethodInfo method)
    {
        method = null;
        foreach (var m in typeof(TMP_FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "TryAddCharacters") continue;
            var ps = m.GetParameters();
            if (ps.Length == 0) continue;
            Type first = ps[0].ParameterType;
            if (first != typeof(string) && first != typeof(uint[]) && first != typeof(IEnumerable<char>)) continue;
            bool sawBool = false, sawOut = false, valid = true;
            for (int i = 1; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                if (pt == typeof(bool)) { if (sawBool) { valid = false; break; } sawBool = true; }
                else if (pt.IsByRef) { if (sawOut) { valid = false; break; } sawOut = true; }
                else { valid = false; break; }
            }
            if (!valid) continue;
            if (method == null || IsPreferred(first, method.GetParameters()[0].ParameterType))
                method = m;
        }
        return method != null;

        static bool IsPreferred(Type candidate, Type current)
        {
            if (candidate == typeof(string) && current != typeof(string)) return true;
            if (candidate == typeof(uint[]) && current == typeof(IEnumerable<char>)) return true;
            return false;
        }
    }

    static int BakeFont(TMP_FontAsset font, string chars, MethodInfo method)
    {
        if (font == null) return 0;
        try
        {
            // Static atlases can't grow at runtime; TryAddCharacters would warn+log on them — skip silently.
            try
            {
                var mode = font.GetType().GetProperty("atlasPopulationMode")?.GetValue(font);
                string ms = mode?.ToString();
                if (ms != null && ms != "Dynamic" && ms != "DynamicOS") return 0;
            }
            catch { }

            // Let oversized sets spill to a second atlas texture instead of failing/blocking.
            try { font.GetType().GetProperty("isMultiAtlasTexturesEnabled")?.SetValue(font, true); } catch { }

            var ps = method.GetParameters();
            var args = new object[ps.Length];
            Type first = ps[0].ParameterType;
            args[0] = first == typeof(string) ? chars
                    : first == typeof(uint[]) ? ToUnicodes(chars)
                    : chars.ToCharArray(); // IEnumerable<char> overloads
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].ParameterType.IsByRef ? null : false;
            method.Invoke(font, args);
            return 1;
        }
        catch (Exception e) { Loader.Warning($"FontManager: bake failed for '{font.name}': {e.Message}"); return 0; }
    }

    static uint[] ToUnicodes(string chars)
    {
        var list = new uint[chars.Length];
        for (int i = 0; i < chars.Length; i++) list[i] = chars[i];
        return list;
    }

    static string CollectBakeChars()
    {
        var set = new HashSet<char>();
        for (char c = (char)0x20; c <= 0x7E; c++) set.Add(c); // ASCII printable
        foreach (char c in Tr.CollectOverlayCharacters()) set.Add(c);
        if (Main.Settings?.Labels != null) // user-customized labels may add out-of-LUT chars
            foreach (var field in typeof(LabelConfig).GetFields())
                if (field.FieldType == typeof(string) && field.GetValue(Main.Settings.Labels) is string s)
                    foreach (char c in s) set.Add(c);
        return string.Concat(set);
    }
}
