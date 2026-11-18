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
    /// <summary>按参考字体视觉尺寸缩放过的 CJK 回退克隆（模组自有，不碰游戏共享资产）。</summary>
    static TMP_FontAsset _cjkScaled;

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
            // issue #7：默认字体缺 CJK 字形时回退到游戏的 CJK 资产，但该资产相对
            // 点号的视觉尺寸（capHeight/pointSize 比例）偏小，回退出来的中文明显小一圈。
            // 按 MatchFontSizes 同一套 cap-height 口径缩放一份模组自有克隆作回退——
            // 不能直接改共享资产的 faceInfo，那会影响游戏自身的中文 UI 渲染。
            TMP_FontAsset fallback = cjk;
            if (VersionSafe.IsV141OrLater)
            {
                try
                {
                    var reference = AssetLoader.FontAsset;
                    float refRatio = reference != null ? GetCapHeightRatio(reference) : 0f;
                    float refScale;
                    try { refScale = reference.faceInfo.scale; } catch { refScale = 1f; }
                    if (refScale <= 0) refScale = 1f;
                    float cjkCap = GetCapHeightRatio(cjk);
                    if (reference != null && refRatio > 0 && cjkCap > 0)
                    {
                        // cap 齐平只保证回退 CJK 与拉丁大写字同高——对表意字来说仍然
                        // 偏小（方块字的视觉基准应高于拉丁 cap，参照游戏原生 UI 的
                        // 中西配比）。1.3x 是「中文与数字混排自然大小」的经验补偿。
                        const float CjkVisualBoost = 1.3f;
                        float target = Mathf.Clamp(refRatio * refScale / cjkCap * CjkVisualBoost, 0.5f, 3f);
                        _cjkScaled ??= UnityEngine.Object.Instantiate(cjk);
                        if (_cjkScaled != null)
                        {
                            _cjkScaled.name = "JipperCJKFallback";
                            var fi = _cjkScaled.faceInfo;
                            if (Math.Abs(fi.scale - target) >= 0.001f)
                            {
                                fi.scale = target;
                                _cjkScaled.faceInfo = fi;
                                Loader.Log($"FontManager: CJK fallback scaled x{target:F3} (cap {cjkCap:F4} -> ref {refRatio:F4})");
                            }
                            fallback = _cjkScaled;
                        }
                    }
                }
                catch (Exception e) { Loader.Warning($"FontManager: CJK fallback scaling skipped: {e.Message}"); }
            }
            foreach (var entry in FontList)
            {
                if (entry.font == null || entry.font == cjk || entry.font == fallback || !entry.Owned) continue;
                entry.font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                // 关键：AssetLoader.Load 已把【原始 cjk】挂进默认字体的回退表（表首），
                // TMP 回退按表序命中第一个有字形的资产——追加克隆永远排在原始 cjk
                // 之后、不被用到（issue #7 三档缩放「无效」的根因）。这里把表里的
                // 原始 cjk 原位替换为缩放克隆，克隆才是实际渲染来源。
                int at = entry.font.fallbackFontAssetTable.IndexOf(cjk);
                if (at >= 0) entry.font.fallbackFontAssetTable[at] = fallback;
                else if (!entry.font.fallbackFontAssetTable.Contains(fallback))
                    entry.font.fallbackFontAssetTable.Add(fallback);
            }
        }

        Loader.Log($"FontManager: {FontList.Count} fonts");
        BakeOverlayGlyphs();
        CalibrateCjkFallbackScale();
        MatchFontSizes();
    }


    /// <summary>烘焙后校准：CJK 字体的 capLine 量的是它拉丁字形的大写高，表意字
    /// （方块字）的实际墨高不等于 capLine（实测 ink 0.5663 vs cap 0.5222，参考字体
    /// 0.7000），按 cap 推算的缩放永远偏小。且本游戏 TMP 的回退渲染路径不读
    /// faceInfo.scale——1.34/1.74/1.42 三档 scale 实测渲染完全不变——所以不靠
    /// scale：这里直接量一个已烘焙表意字的真实墨高，把克隆里全部已烘焙字形的
    /// metrics（宽/高/边距/步进）与 faceInfo 行高按倍率放大（scale 归 1 兜底）。
    /// 字形四边形尺寸由 metrics 决定，是渲染最底层，任何 TMP 版本都绕不开。
    /// 局限：运行期动态新增的字形（启动烘焙集之外）不放大，重扫描/重启即校准。</summary>
    static void CalibrateCjkFallbackScale()
    {
        if (!VersionSafe.IsV141OrLater || _cjkScaled == null) return;
        try
        {
            var reference = AssetLoader.FontAsset;
            float refRatio = reference != null ? GetCapHeightRatio(reference) : 0f;
            if (refRatio <= 0) return;
            TMP_Character ch = null;
            foreach (var c in new[] { '最', '中', '国', '文', '大' })
                if (_cjkScaled.characterLookupTable.TryGetValue(c, out var tc) && tc?.glyph != null) { ch = tc; break; }
            if (ch == null) { Loader.Warning("FontManager: CJK fallback calibration skipped: no baked ideograph"); return; }
            float pt = _cjkScaled.faceInfo.pointSize;
            float ink = ch.glyph.metrics.horizontalBearingY; // baseline → 字形顶部
            if (pt <= 0 || ink <= 0) return;
            float inkRatio = ink / pt;                       // 表意字墨高比例（字体固有，与 scale 无关）
            // 目标：表意字墨高 = 参考 cap 高 × 1.15（中文略高于拉丁 cap 的自然配比）。
            float m = Mathf.Clamp(refRatio * 1.15f / inkRatio, 0.5f, 3f);
            var fi = _cjkScaled.faceInfo;
            fi.scale = 1f; // 单一机制兜底：哪条路径读 scale 都不会二次叠加
            fi.lineHeight *= m;
            fi.ascentLine *= m;
            fi.descentLine *= m;
            fi.capLine *= m;
            _cjkScaled.faceInfo = fi;
            int n = 0;
            foreach (var kv in _cjkScaled.characterLookupTable)
            {
                var g = kv.Value?.glyph;
                if (g == null) continue;
                var mt = g.metrics;
                g.metrics = new UnityEngine.TextCore.GlyphMetrics(mt.width * m, mt.height * m,
                    mt.horizontalBearingX * m, mt.horizontalBearingY * m, mt.horizontalAdvance * m);
                n++;
            }
            Loader.Log($"FontManager: CJK fallback calibrated x{m:F3} (ideograph ink {inkRatio:F4}, {n} glyphs resized)");
        }
        catch (Exception e) { Loader.Warning($"FontManager: CJK fallback calibration skipped: {e.Message}"); }
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
            // CJK 字形烘到缩放克隆上（仅模组自己用），不再往游戏共享 CJK 图集里加页
            try { baked += BakeFont(_cjkScaled != null ? _cjkScaled : RDConstants.data.chineseFontTMPro, chars, method); } catch { }
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
