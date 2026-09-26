using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class ColorCache {
    public float r;
    [JsonIgnore] public string rString;
    public float g;
    [JsonIgnore] public string gString;
    public float b;
    [JsonIgnore] public string bString;
    public float a;
    [JsonIgnore] public string aString;
    [JsonIgnore] public string oldHexString;
    [JsonIgnore] public string hexString;

    public ColorCache() { }

    public ColorCache(Color color) {
        r = color.r; g = color.g; b = color.b; a = color.a;
    }

    public bool SettingGUI(string label, Color defaultColor) {
        bool changed = false;
        oldHexString ??= hexString = GetHexString();

        // Hex
        GUILayout.BeginHorizontal();
        GUILayout.Label("Hex", GUILayout.Width(30));
        string newHex = GUILayout.TextField(hexString ?? GetHexString(), GUILayout.Width(65));
        if (newHex != hexString) { hexString = newHex; if (CheckHexString()) { changed = true; rString = gString = bString = aString = null; } }
        GUILayout.EndHorizontal();

        rString ??= r.ToString("F3"); gString ??= g.ToString("F3"); bString ??= b.ToString("F3"); aString ??= a.ToString("F3");

        // R
        GUILayout.BeginHorizontal();
        GUILayout.Label("R", GUILayout.Width(14));
        string nr = GUILayout.TextField(rString, GUILayout.Width(45));
        if (nr != rString) { rString = nr; if (float.TryParse(nr, out float rv) && Math.Abs(rv - r) > 0.001f) { r = Mathf.Clamp01(rv); changed = true; rString = null; } }
        float rs = GUILayout.HorizontalSlider(r, 0, 1);
        if (Math.Abs(rs - r) > 0.001f) { r = rs; changed = true; rString = null; }
        GUILayout.EndHorizontal();

        // G
        GUILayout.BeginHorizontal();
        GUILayout.Label("G", GUILayout.Width(14));
        string ng = GUILayout.TextField(gString, GUILayout.Width(45));
        if (ng != gString) { gString = ng; if (float.TryParse(ng, out float gv) && Math.Abs(gv - g) > 0.001f) { g = Mathf.Clamp01(gv); changed = true; gString = null; } }
        float gs = GUILayout.HorizontalSlider(g, 0, 1);
        if (Math.Abs(gs - g) > 0.001f) { g = gs; changed = true; gString = null; }
        GUILayout.EndHorizontal();

        // B
        GUILayout.BeginHorizontal();
        GUILayout.Label("B", GUILayout.Width(14));
        string nb = GUILayout.TextField(bString, GUILayout.Width(45));
        if (nb != bString) { bString = nb; if (float.TryParse(nb, out float bv) && Math.Abs(bv - b) > 0.001f) { b = Mathf.Clamp01(bv); changed = true; bString = null; } }
        float bs = GUILayout.HorizontalSlider(b, 0, 1);
        if (Math.Abs(bs - b) > 0.001f) { b = bs; changed = true; bString = null; }
        GUILayout.EndHorizontal();

        // A
        GUILayout.BeginHorizontal();
        GUILayout.Label("A", GUILayout.Width(14));
        string na = GUILayout.TextField(aString, GUILayout.Width(45));
        if (na != aString) { aString = na; if (float.TryParse(na, out float av) && Math.Abs(av - a) > 0.001f) { a = Mathf.Clamp01(av); changed = true; aString = null; } }
        float a_s = GUILayout.HorizontalSlider(a, 0, 1);
        if (Math.Abs(a_s - a) > 0.001f) { a = a_s; changed = true; aString = null; }
        GUILayout.EndHorizontal();

        // Color preview (solid color using DrawTexture)
        GUILayout.Space(2);
        GUILayout.BeginHorizontal();
        GUILayout.Space(14);
        var prevColor = GUI.color;
        GUI.color = this;
        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(20));
        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;
        GUILayout.EndHorizontal();
        GUILayout.Space(2);

        if (changed) {
            oldHexString = hexString = GetHexString();
        }
        return changed;
    }

    bool CheckHexString() {
        if (string.IsNullOrEmpty(hexString)) { hexString = oldHexString; return false; }
        hexString = hexString.Trim().TrimStart('#');
        if (hexString.Length is not (6 or 8)) return false;
        try {
            r = Convert.ToInt32(hexString.Substring(0, 2), 16) / 255f;
            g = Convert.ToInt32(hexString.Substring(2, 2), 16) / 255f;
            b = Convert.ToInt32(hexString.Substring(4, 2), 16) / 255f;
            a = hexString.Length == 8 ? Convert.ToInt32(hexString.Substring(6, 2), 16) / 255f : 1;
            return true;
        } catch { return false; }
    }

    string GetHexString() {
        var sb = new StringBuilder();
        sb.Append(Normalize(r).ToString("X2"));
        sb.Append(Normalize(g).ToString("X2"));
        sb.Append(Normalize(b).ToString("X2"));
        if (a < 1) sb.Append(Normalize(a).ToString("X2"));
        return sb.ToString();
    }

    static int Normalize(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (float.IsPositiveInfinity(v)) return 255;
        if (float.IsNegativeInfinity(v)) return 0;
        return v switch { <= 0 => 0, >= 1 => 255, _ => (int)Math.Round(v * 255) };
    }

    public static implicit operator Color(ColorCache c)
    {
        if (c == null) return Color.white;
        var col = default(Color);
        col.r = Safe01(c.r);
        col.g = Safe01(c.g);
        col.b = Safe01(c.b);
        col.a = Safe01(c.a);
        return col;
    }

    public void Sanitize()
    {
        r = Safe01(r); g = Safe01(g); b = Safe01(b); a = Safe01(a);
    }

    static float Safe01(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (float.IsPositiveInfinity(v)) return 1;
        if (float.IsNegativeInfinity(v)) return 0;
        return Mathf.Clamp01(v);
    }
}
