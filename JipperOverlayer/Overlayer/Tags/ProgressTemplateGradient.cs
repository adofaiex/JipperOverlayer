using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>Editable snapshot of the progress palette: 0=FFFFFF|50=FFAA66|100=DFC1FF.</summary>
internal sealed class ProgressTemplateGradient
{
    // TemplateEngine retains each argument string with the parsed template. Cache parsed palettes
    // alongside it, so editing a field can release old palettes without reparsing every frame.
    private static readonly ConditionalWeakTable<string, ProgressTemplateGradient> Cache = new();
    private readonly ColorPerDictionary _colors;

    private ProgressTemplateGradient(ColorPerDictionary colors) => _colors = colors;

    internal static ColorPerDictionary Resolve(string stops)
        => string.IsNullOrWhiteSpace(stops) ? null : Cache.GetValue(stops, Parse)._colors;

    internal static string Export(ColorPerDictionary colors)
    {
        var stops = new List<string>();
        if (colors?.List != null)
        {
            foreach (var stop in colors.List)
            {
                if (stop == null || float.IsNaN(stop.Progress) || float.IsInfinity(stop.Progress)) continue;
                string percent = (Mathf.Clamp01(stop.Progress) * 100d).ToString("G9", CultureInfo.InvariantCulture);
                stops.Add(percent + "=" + Hex(stop));
            }
        }
        // With no stops the built-in palette is a constant. A separate max entry otherwise
        // preserves the optional exact-100% override without changing interpolation below 100%.
        if (stops.Count == 0) stops.Add("0=" + Hex(colors?.GetColor(0f) ?? Color.white));
        else if (colors.PerfectColor != null) stops.Add("max=" + Hex(colors.PerfectColor));
        return string.Join("|", stops);
    }

    private static ProgressTemplateGradient Parse(string stops)
    {
        var points = new List<(float, Color)>();
        Color? atMax = null;
        foreach (string stop in stops.Split('|'))
        {
            int split = stop.IndexOf('=');
            if (split < 1) return new ProgressTemplateGradient(null);
            string key = stop.Substring(0, split).Trim();
            string hex = stop.Substring(split + 1).Trim().TrimStart('#');
            if ((hex.Length != 6 && hex.Length != 8) || !ColorUtility.TryParseHtmlString("#" + hex, out var color))
                return new ProgressTemplateGradient(null);
            if (string.Equals(key, "max", StringComparison.OrdinalIgnoreCase)) atMax = color;
            else if (double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)
                && percent >= 0 && percent <= 100)
                points.Add(((float)(percent / 100), color));
            else return new ProgressTemplateGradient(null);
        }
        if (points.Count == 0) return new ProgressTemplateGradient(null);
        return new ProgressTemplateGradient(atMax.HasValue
            ? new ColorPerDictionary(points, atMax.Value) : new ColorPerDictionary(points));
    }

    private static string Hex(Color color)
    {
        string rgba = ColorUtility.ToHtmlStringRGBA(color);
        return rgba.EndsWith("FF", StringComparison.OrdinalIgnoreCase) ? rgba.Substring(0, 6) : rgba;
    }
}
