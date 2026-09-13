using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JipperOverlayer.Overlayer;

/// <summary>
/// 资源加载：默认字体与进度条圆角精灵都内嵌于主 DLL，字体首次运行释放到 ModPath\assets\；
/// 进度条改为纯代码构建。原 AssetBundle 管线已移除——bundle 里实际只有一份 OTF 字体和一个
/// 只引用 Unity 内置 Background 精灵的 prefab，不再值得为它维护 Unity 编辑器工程与三份
/// bundle 产物。内置精灵在玩家版无法用 Resources.GetBuiltinResource 取到（会打日志
/// "The resource UI/Skin/UISprite.psd could not be loaded"），因此改为自带原始 RGBA 字节
/// 并在运行时重建 Sprite，彻底摆脱对游戏 Unity 内置资源的依赖。
/// / Resource loading: the default font and the progress-bar corner sprite are both embedded
/// in the main DLL (the font is extracted to ModPath\assets\ on first run) and the progress
/// bar is built entirely in code. The old AssetBundle pipeline is gone — the bundle only ever
/// carried one OTF plus a prefab whose Images referenced nothing but Unity's built-in
/// Background sprite. That built-in sprite cannot be fetched in a player build via
/// Resources.GetBuiltinResource (it logs "The resource UI/Skin/UISprite.psd could not be
/// loaded"), so this DLL now carries the raw RGBA bytes and rebuilds the Sprite at runtime,
/// removing the dependency on the game's built-in UI resources altogether.
/// </summary>
public class AssetLoader
{
    /// <summary>内嵌默认字体的资源名（与 csproj 的 LogicalName 一致）。
    /// / Resource name of the embedded default font (matches the csproj LogicalName).</summary>
    const string FontResource = "JipperOverlayer.Assets.MAPLESTORY_OTF_BOLD.OTF";

    /// <summary>释放到磁盘时的文件名，用户可直接替换该文件。
    /// / On-disk file name; users can replace this file directly.</summary>
    const string FontFileName = "MAPLESTORY_OTF_BOLD.OTF";

    public static TMP_FontAsset FontAsset;

    /// <summary>内嵌进度条圆角精灵的原始 RGBA 资源名（与 csproj 的 LogicalName 一致）。
    /// / Resource name of the embedded progress-bar corner sprite's raw RGBA
    /// (matches the csproj LogicalName).</summary>
    const string BarSpriteResource = "JipperOverlayer.Assets.ProgressBarSprite.rgba";

    static Sprite _barSprite;
    static bool _barSpriteFailed;

    // 原 prefab 三个 Image 的圆角全部来自同一个精灵：m_Sprite 均为
    // {fileID: 10907, guid: 0000000000000000f000000000000000}，即 Unity 内置 Background
    // 精灵——32×32 圆角图，四边 border≈10px、PPU=200（内置精灵元数据：m_Border 四边
    // 9.9487~9.9732、m_PixelsToUnits = 200、m_Pivot = (0.5, 0.5)）。Image 的
    // m_Type=1(Sliced) 靠它把四角原样绘制、只拉伸中间段——精灵丢了就退化成直角矩形。
    // 内置精灵在玩家版取不到（Resources.GetBuiltinResource 会打
    // "The resource UI/Skin/UISprite.psd could not be loaded"），所以这里自带原始 RGBA
    // 字节并在运行时重建：32×32 贴图、border 取 10 使中间段恰为 12px，与内置精灵一致。
    // / All three of the original prefab's Images took their rounded corners from one sprite:
    // m_Sprite was {fileID: 10907, guid: 0000000000000000f000000000000000}, i.e. Unity's
    // built-in Background sprite — a 32×32 rounded texture, border ≈10px on every side,
    // PPU 200 (built-in metadata: m_Border 9.9487–9.9732 per side, m_PixelsToUnits = 200,
    // m_Pivot = (0.5, 0.5)). Image.m_Type = 1 (Sliced) relies on it to draw the corners
    // unscaled and stretch only the middle — lose the sprite and the bar degrades to sharp
    // rectangles. That built-in sprite cannot be fetched in a player build
    // (Resources.GetBuiltinResource logs "The resource UI/Skin/UISprite.psd could not be
    // loaded"), so this class carries the raw RGBA bytes and rebuilds it at runtime: a 32×32
    // texture with a border of 10, leaving exactly the original 12px centre slice.
    const int BarSpriteSize = 32;
    const float BarSpriteBorder = 10f;
    const float BarSpritePpu = 200f;

    public static void Load()
    {
        string assetsDir = Path.Combine(Loader.ModPath, "assets");
        string fontPath = Path.Combine(assetsDir, FontFileName);

        try { ExtractEmbeddedFont(assetsDir, fontPath); }
        catch (Exception e) { Loader.Warning($"AssetLoader: font extraction failed: {e.Message}"); }

        if (File.Exists(fontPath))
        {
            try
            {
                // new Font(path) 是唯一能从磁盘 OTF 建出动态字体的公开途径，
                // 因此内嵌资源仍要落盘一次（已存在则不覆盖，用户替换优先）。
                // / new Font(path) is the only public way to build a dynamic font from a
                // disk OTF, so the embedded asset still lands on disk once — existing
                // files are never overwritten, so user replacements win.
                var font = new Font(fontPath);
                if (font != null) FontAsset = TMP_FontAsset.CreateFontAsset(font);
            }
            catch (Exception e) { Loader.Warning($"AssetLoader: font load failed: {e.Message}"); }
        }
        else
        {
            Loader.Warning($"AssetLoader: font not found at: {fontPath}");
        }

        if (FontAsset != null)
        {
            try
            {
                FontAsset.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                var cjk = RDConstants.data.chineseFontTMPro;
                if (cjk != null && !FontAsset.fallbackFontAssetTable.Contains(cjk))
                    FontAsset.fallbackFontAssetTable.Add(cjk);
            }
            catch (Exception e) { Loader.Warning($"AssetLoader: fallback font link failed: {e.Message}"); }
        }
        else
        {
            CreateFallbackFont();
        }

        Loader.Log($"AssetLoader: font ready ({FontAsset != null}), assets dir: {assetsDir}");
    }

    /// <summary>
    /// 把内嵌字体释放到 assetsDir——仅在该文件缺失时写入，经 .tmp + Move 原子落盘，
    /// 中途崩溃不会留下残缺 OTF（截断的字体文件会毒化之后每次加载）。
    /// / Extract the embedded font into assetsDir — only when the file is missing, written
    /// via .tmp + Move so a crash mid-write never leaves a truncated OTF behind (a
    /// truncated font file would poison every later load).
    /// </summary>
    static void ExtractEmbeddedFont(string assetsDir, string fontPath)
    {
        if (File.Exists(fontPath)) return;

        Directory.CreateDirectory(assetsDir);
        Assembly asm = typeof(AssetLoader).Assembly;
        using Stream rs = asm.GetManifestResourceStream(FontResource);
        if (rs == null)
        {
            // 内嵌资源缺失是构建问题（csproj 的 EmbeddedResource 没生效），不是用户问题。
            // / A missing embedded resource is a BUILD problem (the csproj EmbeddedResource
            // did not take effect), not a user problem.
            Loader.Error($"AssetLoader: embedded resource missing: {FontResource}");
            return;
        }

        string tmp = fontPath + ".tmp";
        using (Stream fs = File.Create(tmp)) rs.CopyTo(fs);
        if (File.Exists(fontPath)) File.Delete(tmp); // 并发释放已写好 / raced another extract
        else File.Move(tmp, fontPath);
        Loader.Log($"AssetLoader: extracted default font to {fontPath}");
    }

    static void CreateFallbackFont()
    {
        try { FontAsset = RDConstants.data.chineseFontTMPro; }
        catch { FontAsset = null; }
        Loader.Log($"AssetLoader: using fallback font: {FontAsset?.name}");
    }

    public static void Unload()
    {
        try
        {
            if (FontAsset != null && FontAsset != RDConstants.data.chineseFontTMPro)
                FontAsset = null;
        }
        catch { FontAsset = null; }
    }

    // ===== 进度条：原 prefab 的等价代码构建 =====
    // 原 bundle 里的 ProgressBar.prefab 只有三个 Image，且 m_Sprite 全部指向同一个 Unity
    // 内置 Background 精灵（fileID 10907），没有任何自定义贴图——因此可以在代码里精确重建，
    // 顺带删掉整个 Unity 编辑器工程。几何数值与原 prefab 逐字段对齐。
    // / Code-built equivalent of the old prefab. ProgressBar.prefab held only three Images
    // whose m_Sprite all pointed at the same built-in Background sprite (fileID 10907) with no
    // custom textures, so it rebuilds exactly in code — which lets the whole Unity editor
    // project go away. Geometry mirrors the original prefab field by field.

    /// <summary>
    /// 构建进度条层级：borderLine（黑，最底层）→ background（白）→ line（随进度拉伸）。
    /// 子物体顺序与原 prefab 的 m_Children 一致，保证绘制层级不变。
    /// / Build the progress-bar hierarchy: borderLine (black, bottom) → background (white)
    /// → line (stretched by progress). Child order matches the original prefab's m_Children
    /// so the draw order is unchanged.
    /// </summary>
    public static GameObject CreateProgressBar(Transform parent)
    {
        var root = new GameObject("ProgressBar");
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1);
        rootRt.pivot = new Vector2(0.5f, 1);
        rootRt.anchoredPosition = new Vector2(0, -30);
        rootRt.sizeDelta = new Vector2(642, 18);

        // 原 prefab 的子物体顺序：borderLine, background, line
        CreateBarImage("borderLine", rootRt, new Color(0, 0, 0, 1),
            new Vector2(642, 18), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        CreateBarImage("background", rootRt, new Color(1, 1, 1, 1),
            new Vector2(638, 14), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        // line 左对齐、pivot 在左侧中点：UpdateProgressBar 直接改 sizeDelta.x 来拉伸。
        // / line is left-anchored with its pivot on the left edge so UpdateProgressBar can
        // stretch it by writing sizeDelta.x directly.
        CreateBarImage("line", rootRt, new Color(0.92156863f, 0.8039216f, 0.9764706f, 1),
            new Vector2(100, 14), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0));

        return root;
    }

    static void CreateBarImage(string name, Transform parent, Color color, Vector2 size,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = color;
        // 原 prefab 的 m_Type: 1 = Sliced，四角由精灵的 border 区域原样绘制。
        // / The original prefab had m_Type: 1 (Sliced), so the corners are drawn from the
        // sprite's border region.
        img.type = Image.Type.Sliced;
        img.sprite = GetBarSprite();
    }

    /// <summary>
    /// 构建进度条圆角精灵：读内嵌的 32×32 RGBA 原始字节，还原成与原 prefab 所用内置
    /// Background 精灵等价的 Sprite（border 四边 10px、PPU 200、pivot 居中）。
    /// 结果缓存，三个 Image 共用同一份。资源缺失时返回 null（Sliced 退化为直角矩形，
    /// 不报错）——这是构建问题，会打错误日志。
    /// / Build the progress-bar corner sprite: read the embedded 32×32 RGBA bytes and
    /// reconstruct the equivalent of the built-in Background sprite the original prefab used
    /// (10px border on every side, PPU 200, centred pivot). Cached and shared by all three
    /// Images. Returns null if the resource is missing (Sliced then degrades to plain rects
    /// rather than throwing) — that is a build problem and is logged as an error.
    /// </summary>
    static Sprite GetBarSprite()
    {
        // Unity 重载的 == 会把「已销毁对象」判为 null：纹理一旦被卸载就重建，避免静默退回
        // 直角矩形——那正是本次要修的 bug。资源缺失/构建失败是永久性问题，只报一次。
        // / Unity's overloaded == reports a destroyed object as null, so a texture that got
        // unloaded is rebuilt rather than silently falling back to sharp rectangles — exactly
        // the bug being fixed here. A missing resource or a failed build is permanent and is
        // reported only once.
        if (_barSprite != null) return _barSprite;
        if (_barSpriteFailed) return null;

        byte[] rgba = null;
        try
        {
            using Stream rs = typeof(AssetLoader).Assembly.GetManifestResourceStream(BarSpriteResource);
            if (rs != null)
            {
                rgba = new byte[rs.Length];
                int read = 0;
                while (read < rgba.Length)
                {
                    int n = rs.Read(rgba, read, rgba.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read != rgba.Length) rgba = null;
            }
        }
        catch (Exception e) { Loader.Warning($"AssetLoader: bar sprite read failed: {e.Message}"); }

        int expected = BarSpriteSize * BarSpriteSize * 4;
        if (rgba == null || rgba.Length != expected)
        {
            // 内嵌资源缺失/长度不符是构建问题（csproj 的 EmbeddedResource 没生效）。
            // / A missing or wrong-sized embedded resource is a BUILD problem (the csproj
            // EmbeddedResource did not take effect).
            Loader.Error($"AssetLoader: embedded bar sprite missing or wrong size " +
                         $"({rgba?.Length ?? 0} != {expected}); progress bar renders as plain rects");
            _barSpriteFailed = true;
            return null;
        }

        try
        {
            // 用 SetPixels32 而不是 LoadRawTextureData：前者的行序有明确文档（数组从
            // 左下角开始、行优先），内嵌字节正是按该顺序（视觉底行在前）排布的，
            // 不会因平台/图形 API 的原始数据布局差异而上下翻转。
            // / SetPixels32 rather than LoadRawTextureData: its row order is documented
            // unambiguously (array starts at the bottom-left, row major), which is exactly how
            // the embedded bytes are laid out (visual bottom row first), so the sprite can
            // never end up flipped by a platform/graphics-API raw-layout difference.
            var pixels = new Color32[BarSpriteSize * BarSpriteSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                int o = i * 4;
                pixels[i] = new Color32(rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]);
            }

            var tex = new Texture2D(BarSpriteSize, BarSpriteSize, TextureFormat.RGBA32, false);
            tex.name = "JipperProgressBarSprite";
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            // border = (left, bottom, right, top)，与内置精灵四边等宽。
            // / border = (left, bottom, right, top); equal on all four sides like the built-in.
            var border = new Vector4(BarSpriteBorder, BarSpriteBorder, BarSpriteBorder, BarSpriteBorder);
            _barSprite = Sprite.Create(tex, new Rect(0, 0, BarSpriteSize, BarSpriteSize),
                new Vector2(0.5f, 0.5f), BarSpritePpu, 0, SpriteMeshType.FullRect, border);
            _barSprite.name = "JipperProgressBarSprite";
            _barSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        catch (Exception e)
        {
            Loader.Warning($"AssetLoader: bar sprite build failed ({e.Message}); progress bar renders as plain rects");
            _barSprite = null;
            _barSpriteFailed = true;
        }

        return _barSprite;
    }
}
