using System;
using System.Collections.Generic;
using JipperOverlayer.Overlayer.Localization;
using Newtonsoft.Json;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class ColorPerDictionary {
    public ColorCache PerfectColor;
    public List<ProgressColorCache> List = [];
    [JsonIgnore] public bool Expanded;
    [JsonIgnore] public ProgressColorCache ExpandedCache;
    private static readonly GUIStyle _foldoutStyle = new()
    {
        fixedWidth = 18f, normal = new GUIStyleState { textColor = Color.white }, fontSize = 14, margin = new RectOffset(4, 2, 4, 4)
    };

    public ColorPerDictionary() { }

    public ColorPerDictionary(IEnumerable<(float, Color)> collection) {
        foreach (var item in collection) Add(item);
    }

    public ColorPerDictionary(ColorCache perfectColor) : this() { PerfectColor = perfectColor; }

    public ColorPerDictionary(Color color) : this(new ColorCache(color)) { }

    public ColorPerDictionary(IEnumerable<(float, Color)> collection, Color color) : this(collection) {
        PerfectColor = new ColorCache(color);
    }

    // Call after deserialization to ensure list is sorted
    public void EnsureSorted() {
        // 旧配置/手改 JSON 可能把 List 置 null，或留下 NaN/Infinity 的进度点。
        // 先做有限值过滤再排序，避免二分与插值把坏值带进每帧显示路径。
        List ??= new List<ProgressColorCache>();
        for (int i = List.Count - 1; i >= 0; i--)
        {
            var stop = List[i];
            if (stop == null || !IsFinite(stop.Progress))
                List.RemoveAt(i);
            else
            {
                stop.Progress = Mathf.Clamp01(stop.Progress);
                stop.Sanitize();
            }
        }
        List.Sort((a, b) => a.Progress.CompareTo(b.Progress));
        InvalidateHexLut();
    }

    static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    public Color GetColor(float key, bool noCache = false) {
        if (float.IsNaN(key)) key = 0;
        else if (float.IsInfinity(key)) key = key > 0 ? 1 : 0;
        if (key < 0) key = 0;
        if (key > 1) key = 1;
        if (!noCache && _lastKey == key && _lastColor.HasValue) return _lastColor.Value;
        Color result;
        if (PerfectColor != null && key == 1) result = PerfectColor;
        else if (List.Count == 0) result = PerfectColor ?? Color.white;
        else {
            int index = BinarySearch(key);
            if (index == 0) result = List[0];
            else if (index == List.Count) result = List[List.Count - 1];
            else if (List[index].Progress == key) result = List[index];
            else {
                float s = List[index - 1].Progress;
                float e = List[index].Progress;
                // 用户可以拖出两个相同百分比的色点；此时插值分母为 0，
                // 旧代码会产生 NaN 颜色并污染整段渐变。退化为后一个色点。
                result = e - s > 1e-6f
                    ? Color.Lerp(List[index - 1], List[index], (key - s) / (e - s))
                    : List[index];
            }
        }
        if (!noCache) {
            _lastKey = key;
            _lastColor = result;
        }
        return result;
    }

    float _lastKey = -1f;
    Color? _lastColor;

    // ===== Pre-baked hex LUT — replaces per-frame ColorUtility.ToHtmlString* calls =====
    private const int HexLutSize = 256;
    [JsonIgnore] private string[] _hexLutRgb;
    [JsonIgnore] private string[] _hexLutRgba;
    [JsonIgnore] private bool _hexLutDirty = true;

    /// <summary>Returns a pre-baked lower-case hex string ("rrggbb" or "rrggbbaa") for <paramref name="key"/> in [0,1].</summary>
    public string GetHex(float key, bool includeAlpha = false)
    {
        EnsureHexLut();
        if (float.IsNaN(key)) key = 0;
        else if (float.IsInfinity(key)) key = key > 0 ? 1 : 0;
        else if (key < 0) key = 0;
        else if (key > 1) key = 1;
        var lut = includeAlpha ? _hexLutRgba : _hexLutRgb;
        return lut[(int)(key * (HexLutSize - 1) + 0.5f)];
    }

    private void EnsureHexLut()
    {
        if (!_hexLutDirty && _hexLutRgb != null) return;
        _hexLutRgb = new string[HexLutSize];
        _hexLutRgba = new string[HexLutSize];
        for (int i = 0; i < HexLutSize; i++)
        {
            float t = i / (float)(HexLutSize - 1);
            Color c = GetColor(t);
            // Bake via ColorUtility so output is byte-for-byte identical to the old runtime path.
            _hexLutRgb[i] = ColorUtility.ToHtmlStringRGB(c);
            _hexLutRgba[i] = ColorUtility.ToHtmlStringRGBA(c);
        }
        _hexLutDirty = false;
    }

    private void InvalidateHexLut()
    {
        _hexLutDirty = true;
        // GetColor 的单项缓存也必须失效：EnsureHexLut 烘焙时会调用 GetColor，
        // 旧缓存会让编辑后的同一条 stop 继续返回旧颜色。
        _lastKey = -1f;
        _lastColor = null;
    }

    int BinarySearch(float value) {
        if (List.Count == 0) return 0;
        int lo = 0, hi = List.Count - 1;
        while (lo <= hi) {
            int m = (lo + hi) / 2;
            if (List[m].Progress == value) return m;
            if (List[m].Progress < value) lo = m + 1;
            else hi = m - 1;
        }
        return lo;
    }

    public bool SettingGUI(Action onChanged, string text, Action onReset = null) {
        GUILayout.BeginHorizontal();
        Expanded = GUILayout.Toggle(Expanded, Expanded ? "▼" : "▷", _foldoutStyle);
        if (GUILayout.Button(text, GUI.skin.label)) Expanded = !Expanded;
        GUILayout.FlexibleSpace();
        if (onReset != null && GUILayout.Button("R", GUILayout.MinWidth(20))) { onReset(); onChanged?.Invoke(); }
        GUILayout.EndHorizontal();
        if (!Expanded) return false;

        bool changed = false;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.BeginVertical();

        if (GUILayout.Button(Tr.Get(Tr.Key.AddColorStop))) {
            List.Add(new ProgressColorCache(UnityEngine.Random.value, new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value)));
            SortList();
            onChanged?.Invoke(); changed = true;
        }

        for (int i = 0; i < List.Count; i++) {
            var cache = List[i];

            GUILayout.BeginHorizontal();
            bool exp = ExpandedCache == cache;
            exp = GUILayout.Toggle(exp, exp ? "▼" : "▷", _foldoutStyle);
            // Color swatch (solid color)
            var swatchRect = GUILayoutUtility.GetRect(16, 14);
            var prevSwatchColor = GUI.color;
            GUI.color = cache;
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);
            GUI.color = prevSwatchColor;
            if (GUILayout.Button(cache.Progress * 100 + "%", GUI.skin.label)) exp = !exp;
            if (ExpandedCache == cache != exp) ExpandedCache = exp ? cache : null;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (!exp) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.BeginVertical();

            // Percent slider
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tr.Get(Tr.Key.Percent), GUILayout.Width(50));
            float oldP = cache.Progress;
            float p = GUILayout.HorizontalSlider(cache.Progress, 0, 1);
            if (Math.Abs(p - oldP) > 0.001f) {
                cache.Progress = p;
                SortList();
                onChanged?.Invoke(); changed = true;
            }
            GUILayout.Label(cache.Progress.ToString("F3"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Color editor
            if (cache.SettingGUI(cache.Progress.ToString(), cache)) {
                onChanged?.Invoke(); changed = true;
            }

            bool deleted = false;
            if (GUILayout.Button(Tr.Get(Tr.Key.Delete))) {
                List.RemoveAt(i);
                i--;
                onChanged?.Invoke(); changed = true; deleted = true;
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (deleted) continue;
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        if (changed) InvalidateHexLut();
        return changed;
    }

    void SortList() {
        List.Sort((a, b) => a.Progress.CompareTo(b.Progress));
        InvalidateHexLut();
    }

    public void Add((float, Color) item) {
        List.Add(new ProgressColorCache(item.Item1, item.Item2));
        SortList();
    }
}
