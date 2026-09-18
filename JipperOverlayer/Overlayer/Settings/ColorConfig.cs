using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class ColorConfig
{
    public ColorPerDictionary Progress = new([(0f, Color.white), (1f, new Color(0.8745f, 0.7098f, 1f))]);
    public ColorPerDictionary Accuracy = new([(0.98f, Color.magenta), (1f, Color.white)], new Color(1, 0.8549f, 0));
    public ColorPerDictionary XAccuracy = new([(0.98f, Color.magenta), (1f, Color.white)], new Color(1, 0.8549f, 0));
    public ColorPerDictionary MusicTime = new([(1f, Color.white)]);
    public ColorPerDictionary MapTime = new([(1f, Color.white)]);
    public ColorPerDictionary XScore = new([(0.98f, Color.white), (1f, Color.white)], new Color(1, 0.8549f, 0));
    public ColorPerDictionary Best = new([(0f, Color.white), (1f, new Color(0.8745f, 0.7098f, 1f))]);
    public ColorPerDictionary Bpm = new([(0f, Color.white), (1f, Color.magenta)]);
    public ColorPerDictionary Combo = new([(0f, new Color(0.8745f, 0.7098f, 1f)), (1f, new Color(0.7176f, 0.3490f, 1f))]);
    public ColorPerDictionary ProgressBar = new([(1f, new Color(0.9216f, 0.8039f, 0.9765f))]);
    public ColorPerDictionary ProgressBarBackground = new([(1f, Color.white)]);
    public ColorPerDictionary ProgressBarBorder = new([(1f, Color.black)]);

    // Extended overlay colors — gradients
    public ColorPerDictionary JCombo = new([(0f, Color.red), (0.2f, new Color(0.9882f, 1, 0.302f)), (1f, new Color(0.3725f, 1, 0.3119f))]);
    public ColorPerDictionary JDeath = new([(0f, Color.red), (1f, Color.green)]);
    public ColorPerDictionary JTiming = new([(0f, Color.red), (1f, Color.green)]);

    // Extended overlay colors — single
    public ColorCache JStateWaiting = new(Color.white);
    public ColorCache JStateAutoTile = new(new Color(1, 0.5f, 0));
    public ColorCache JStateAuto = new(new Color(0.1058824f, 1f, 0));
    public ColorCache JStatePerfectPlay = new(new Color(1, 0.8549f, 0));
    public ColorCache JStateComplete = new(Color.white);
    public ColorCache JStateClear = new(Color.white);
    public ColorCache JStateNoMiss = new(Color.white);
    public ColorCache JStatePerfectionist = new(Color.white);
    public ColorCache JFps = new(Color.white);
    public ColorCache JAuthor = new(Color.white);
    public ColorCache JStart = new(Color.white);

    public Color GetProgressColor(float t) { return Progress.GetColor(t); }
    public string GetProgressHex(float t, bool alpha = false) { return Progress.GetHex(t, alpha); }
    public string GetBpmHex(float t, bool alpha = false) { return Bpm.GetHex(t, alpha); }
    public string GetAccuracyHex(float t, bool perfect, bool alpha = true)
        => perfect ? ColorUtility.ToHtmlStringRGBA(SettingsGold) : Accuracy.GetHex(t, alpha);
    public string GetXAccuracyHex(float t, bool perfect, bool alpha = true)
        => perfect ? ColorUtility.ToHtmlStringRGBA(SettingsGold) : XAccuracy.GetHex(t, alpha);
    private static readonly Color SettingsGold = new(1, 0.8549f, 0);
    public void EnsureSorted() {
        Progress.EnsureSorted(); Accuracy.EnsureSorted(); XAccuracy.EnsureSorted();
        MusicTime.EnsureSorted(); MapTime.EnsureSorted(); Best.EnsureSorted(); XScore.EnsureSorted();
        Bpm.EnsureSorted(); Combo.EnsureSorted(); ProgressBar.EnsureSorted();
        ProgressBarBackground.EnsureSorted(); ProgressBarBorder.EnsureSorted();
        JCombo.EnsureSorted(); JDeath.EnsureSorted(); JTiming.EnsureSorted();
    }
    public Color GetAccuracyColor(float t, bool perfect) { return perfect ? new Color(1, 0.8549f, 0) : Accuracy.GetColor(t); }
    public Color GetXAccuracyColor(float t, bool perfect) { return perfect ? new Color(1, 0.8549f, 0) : XAccuracy.GetColor(t); }
    public Color GetMusicTimeColor(float t) { return MusicTime.GetColor(t); }
    public Color GetMapTimeColor(float t) { return MapTime.GetColor(t); }
    public Color GetBestColor(float t) { return Best.GetColor(t); }
    public Color GetBpmColor(float t) { return Bpm.GetColor(t); }
    public Color GetComboColor(float t) { return Combo.GetColor(t); }
    public Color GetProgressBarColor(float t) { return ProgressBar.GetColor(t); }
    public Color GetProgressBarBackgroundColor(float t) { return ProgressBarBackground.GetColor(t); }
    public Color GetProgressBarBorderColor(float t) { return ProgressBarBorder.GetColor(t); }

    public void Save()
    {
        try { File.WriteAllText(Path.Combine(Loader.ModPath, "colors.json"), JsonConvert.SerializeObject(this, Formatting.Indented)); }
        catch (Exception e) { Loader.Warning($"Save colors failed: {e.Message}"); }
    }

    void EnsureDefaults()
    {
        if (JStateWaiting.a == 0) JStateWaiting = new(Color.white);
        if (JStateAutoTile.a == 0) JStateAutoTile = new(new Color(1, 0.5f, 0));
        if (JStateAuto.a == 0) JStateAuto = new(new Color(0.1058824f, 1f, 0));
        if (JStatePerfectPlay.a == 0) JStatePerfectPlay = new(new Color(1, 0.8549f, 0));
        if (JStateComplete.a == 0) JStateComplete = new(Color.white);
        if (JStateClear.a == 0) JStateClear = new(Color.white);
        if (JStateNoMiss.a == 0) JStateNoMiss = new(Color.white);
        if (JStatePerfectionist.a == 0) JStatePerfectionist = new(Color.white);
        if (JFps.a == 0) JFps = new(Color.white);
        if (JAuthor.a == 0) JAuthor = new(Color.white);
        if (JStart.a == 0) JStart = new(Color.white);
    }

    public static ColorConfig Load()
    {
        try {
            string p = Path.Combine(Loader.ModPath, "colors.json");
            if (File.Exists(p)) {
                var jsonSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                var cc = JsonConvert.DeserializeObject<ColorConfig>(File.ReadAllText(p), jsonSettings);
                if (cc != null) { cc.EnsureSorted(); cc.EnsureDefaults(); return cc; }
            }
        }
        catch (Exception e) { Loader.Warning($"Load colors failed: {e.Message}"); }
        return new ColorConfig();
    }
}
