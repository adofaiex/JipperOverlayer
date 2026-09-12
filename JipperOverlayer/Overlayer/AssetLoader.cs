using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JipperOverlayer.Overlayer;

/// <summary>
/// 资源加载：默认字体内嵌于主 DLL，首次运行释放到 ModPath\assets\；进度条改为纯代码构建。
/// 原 AssetBundle 管线已移除——bundle 里实际只有一份 OTF 字体和一个全部使用
/// Unity 内置 UISprite 的 prefab，不再值得为它维护 Unity 编辑器工程与三份 bundle 产物。
/// / Resource loading: the default font is embedded in the main DLL and extracted to
/// ModPath\assets\ on first run; the progress bar is now built entirely in code. The old
/// AssetBundle pipeline is gone — the bundle only ever carried one OTF plus a prefab whose
/// images all used Unity's built-in UISprite, so it no longer justified a Unity editor
/// project and three committed bundle blobs.
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

    static Sprite _uiSprite;
    static bool _uiSpriteResolved;

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
    // 原 bundle 里的 ProgressBar.prefab 只有三个 Image，且 m_Sprite 全部指向 Unity 内置
    // 的 UISprite（fileID 10907），没有任何自定义贴图——因此可以在代码里精确重建，
    // 顺带删掉整个 Unity 编辑器工程。几何数值与原 prefab 逐字段对齐。
    // / Code-built equivalent of the old prefab. ProgressBar.prefab held only three Images
    // whose m_Sprite all pointed at Unity's built-in UISprite (fileID 10907) with no custom
    // textures, so it rebuilds exactly in code — which lets the whole Unity editor project
    // go away. Geometry mirrors the original prefab field by field.

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
        // 原 prefab 的 m_Type: 1 = Sliced。内置 UISprite 取不到时 sprite 为 null，
        // Sliced 会自动退化为简单矩形绘制，不会报错。
        // / The original prefab had m_Type: 1 (Sliced). When the built-in UISprite can't be
        // resolved the sprite stays null and Sliced degrades to a plain rect — no error.
        img.type = Image.Type.Sliced;
        img.sprite = GetUISprite();
    }

    /// <summary>
    /// 取 Unity 内置的 UISprite（原 prefab 用的就是它）。不同 Unity 版本对内置 UI 资源的
    /// 暴露方式不同，取不到就返回 null，进度条退化为直角矩形而不是报错。
    /// / Fetch Unity's built-in UISprite (exactly what the original prefab used). Built-in UI
    /// resource exposure varies across Unity versions; returning null degrades the bar to
    /// sharp rectangles instead of throwing.
    /// </summary>
    static Sprite GetUISprite()
    {
        if (_uiSpriteResolved) return _uiSprite;
        _uiSpriteResolved = true;
        try { _uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd"); }
        catch (Exception e) { Loader.Warning($"AssetLoader: built-in UISprite unavailable ({e.Message}); progress bar renders as plain rects"); }
        if (_uiSprite == null)
            Loader.Warning("AssetLoader: built-in UISprite not found; progress bar renders as plain rects");
        return _uiSprite;
    }
}
