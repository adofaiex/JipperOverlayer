using JipperOverlayer.Overlayer.ExtendedOverlay;
using JipperOverlayer.Overlayer.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JipperOverlayer.Overlayer;

public class Overlay
{
    public static Overlay Instance;
    public IOverlayTextManager OverlayTextManager;
    public GameObject GameObject;
    public Canvas Canvas;
    public TextMeshProUGUI ProgressText;
    public TextMeshProUGUI AccuracyText;
    public TextMeshProUGUI XAccuracyText;
    public TextMeshProUGUI TimeText;
    public TextMeshProUGUI MapTimeText;
    public TextMeshProUGUI CheckpointText;
    public TextMeshProUGUI AttemptText;
    public TextMeshProUGUI BestText;
    public RectTransform ComboTransform;
    public TextMeshProUGUI ComboTitle;
    public TextMeshProUGUI ComboText;
    public RectTransform ComboTextTransform;
    internal RectTransform _comboTitleTransform;
    public TextMeshProUGUI BPMText;
    public TextMeshProUGUI[] JudgementTexts = new TextMeshProUGUI[4];
    public TextMeshProUGUI TimingScaleText;
    public TextMeshProUGUI DeathText;   // set by ExtendedOverlayModule.InitializeExtraTexts
    public TextMeshProUGUI StateText;   // set by ExtendedOverlayModule.InitializeExtraTexts
    public ProgressBar ProgressBar;
    public static readonly Color PurePerfectColor = new(1, 0.8549019607843137f, 0);
    public int[] Hit;
    private GameObject _mainContainer;
    private GameObject _bpmObject;
    private GameObject[] _judgementObjects = new GameObject[4];
    private GameObject _comboObject;
    private GameObject _timingScaleObject;
    private GameObject _attemptObject;
    private GameObject _progressBarObject;
    internal static scrEnableIfBeta BetaWatermark;
    internal static Vector2? BetaWatermarkOriginalPos;
    internal int LastTime = -1;
    internal int LastMapTime = -1;
    internal int StartTile;
    public int NoCheckStartTile;
    public int[] Checkpoints;
    internal float LastTileBpm = -1;
    internal float LastCurBpm = -1;
    internal bool SongPlaying;
    public float StartProgress;
    public bool AutoOnceEnabled;
    internal bool IsDeath;
    internal string MusicTimeCache;
    internal string MapTimeCache;
    public PlayCount.Hash LastHash;
    private float _lastSavedStartProgress = -1;
    private bool _lastSavedFromStart;
    public float LastMultiplier = 1f;
    internal string _musicTimeLabel;
    internal string _mapTimeLabel;
    public ExtendedOverlayModule ExtendedOverlay;
    internal static readonly StringBuilder _textSb = new(256);
    private static readonly StringBuilder _judgementSb = new(128);
    private static readonly StringBuilder _attemptSb = new(64);
    private static readonly StringBuilder _bpmSb = new(128);
    private static readonly StringBuilder _comboSb = new(16);
    private static readonly StringBuilder _timingSb = new(64);
    private static readonly StringBuilder _twSb = new(64);
    private float _lastTimingScale = -1f;
    private int _lastTwP = -1, _lastTwX = -1, _lastTwGr = -1, _lastTwGd = -1;
    private OverlayMono _mono;

    private static readonly IReadOnlyList<TextMeshProUGUI> _emptyTexts = Array.Empty<TextMeshProUGUI>();
    protected IReadOnlyList<TextMeshProUGUI> ExtraTexts => ExtendedOverlay?.ExtraTexts ?? _emptyTexts;

    public Overlay()
    {
        Instance = this;
        // ExtendedOverlay 模块常驻：它承载的扩展文本（FPS/Author/State/Death/Start/Timing）
        // 已拆成独立开关，不再是互斥的整体模式
        ExtendedOverlay = new ExtendedOverlayModule(this);
        GameObject = new GameObject("JipperOverlayer Overlay");
        Canvas = GameObject.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject.SetActive(false);
        InitializeStatus();
        InitializeBPM();
        InitializeJudgement();
        InitializeCombo();
        InitializeProgressBar();
        InitializeTimingScale();
        InitializeAttempt();
        ExtendedOverlay?.InitializeExtraTexts();
        OnChangePlayers();
        UpdateSize();
        _mono = GameObject.AddComponent<OverlayMono>();
        _mono.Overlay = this;
        _mono.enabled = false;
        RefreshTimeLabels();
        Object.DontDestroyOnLoad(GameObject);
        if (!GameRefs.IsPaused && GameRefs.IsGameWorld)
            Show(0);
    }

    public void OnChangePlayers()
    {
        Hit = VersionSafe.GetHitMarginsCount();
        SetupTextManager();
    }

    protected void SetupTextManager()
    {
        var s = Main.Settings;
        OverlayTextManager = VersionSafe.IsCoopMode()
            ? new OverlayTextManagerCoop(this)
            : new OverlayTextManagerNormal();
        // 全局小数位仅作用于扩展文本（FPS/开始进度）；主文本各有独立小数位设置
        ExtendedOverlay.DecimalPrecision = s.ExtendedDecimalPrecision;
    }

    protected void InitializeStatus()
    {
        var mainGo = new GameObject("Main"); _mainContainer = mainGo; var go = mainGo;
        var t = go.AddComponent<RectTransform>();
        t.SetParent(Canvas.transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0, 1);
        t.anchoredPosition = new Vector2(16, -16);
        t.sizeDelta = new Vector2(456, 100);
        SetupMainText("Progress", ref ProgressText);
        SetupMainText("Accuracy", ref AccuracyText);
        SetupMainText("XAccuracy", ref XAccuracyText);
        SetupMainText("MusicTime", ref TimeText);
        SetupMainText("MapTime", ref MapTimeText);
        SetupMainText("Checkpoint", ref CheckpointText);
        SetupMainText("Best", ref BestText);
    }

    internal void SetupMainText(string name, ref TextMeshProUGUI text)
    {
        var go = new GameObject(name);
        var t = go.AddComponent<RectTransform>();
        t.SetParent(_mainContainer.transform);
        t.anchorMin = t.anchorMax = new Vector2(0, 1);
        t.sizeDelta = new Vector2(456, 30);
        text = go.AddComponent<TextMeshProUGUI>();
        text.font = AssetLoader.FontAsset;
        text.fontSize = Main.Settings?.MainFontSize ?? 25;
        ShadowManager.ApplyShadow(text);
    }

    public void SetupLocationMain()
    {
        // 统一的栈式布局：全部 18 个可栈排元素共栈、共顺序（ExtendedDisplayOrder），
        // 未启用的元素自然跳过。原 GeneralDisplayOrder 路径与 ExtendedOverlay 路径本就是同一布局。
        ExtendedOverlay.SetupLocation();
    }

    public void SetupLocationJudgement()
    {
        bool coop = VersionSafe.IsCoopMode();
        int count = coop ? Math.Min(VersionSafe.GetPlayerCount(), 4) : 1;
        for (int i = 0; i < 4; i++)
        {
            if (JudgementTexts[i] == null) continue;
            bool active = i < count;
            JudgementTexts[i].enabled = active;
            _judgementObjects[i].SetActive(active);
            if (!active) continue;
            var rt = JudgementTexts[i].rectTransform;
            rt.sizeDelta = new Vector2(1000, 30);
            rt.anchoredPosition = coop
                ? new Vector2(i % 2 == 0 ? -250 : 250, 35 - 30 * (i / 2))
                : new Vector2(0, Main.Settings.JudgementLocationUp ? 85 : 5);
            JudgementTexts[i].alignment = TextAlignmentOptions.Center;
        }
    }
    protected void InitializeBPM()
    {
        var go = new GameObject("BPM");
        var t = go.AddComponent<RectTransform>();
        t.SetParent(Canvas.transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(1, 1);
        t.anchoredPosition = new Vector2(-16, -16);
        t.sizeDelta = new Vector2(456, 240);
        BPMText = go.AddComponent<TextMeshProUGUI>();
        BPMText.font = AssetLoader.FontAsset;
        BPMText.alignment = TextAlignmentOptions.TopRight;
        BPMText.lineSpacing = 30;
        BPMText.fontSize = Main.Settings?.BPMFontSize ?? 25;
        ShadowManager.ApplyShadow(BPMText);
        _bpmObject = go;
    }

    private void InitializeJudgement()
    {
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"Judgement{i + 1}");
            var t = go.AddComponent<RectTransform>();
            t.SetParent(Canvas.transform);
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0);
            t.sizeDelta = new Vector2(1000, 30);
            JudgementTexts[i] = go.AddComponent<TextMeshProUGUI>();
            JudgementTexts[i].font = AssetLoader.FontAsset;
            JudgementTexts[i].fontSize = Main.Settings?.JudgeFontSize ?? 25;
            JudgementTexts[i].color = new Color(0.8509804f, 0.345098f, 1);
            ShadowManager.ApplyShadow(JudgementTexts[i]);
            _judgementObjects[i] = go;
        }
        SetupLocationJudgement();
    }

    protected void InitializeCombo()
    {
        var go = new GameObject("Combo");
        var t = go.AddComponent<RectTransform>();
        t.SetParent(Canvas.transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 1);
        t.sizeDelta = new Vector2(300, 200);
        ComboTransform = t;

        var title = new GameObject("ComboTitle");
        t = title.AddComponent<RectTransform>();
        t.SetParent(ComboTransform);
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.45f);
        t.pivot = new Vector2(0.5f, 0);
        t.sizeDelta = new Vector2(300, 0);
        _comboTitleTransform = t;
        ComboTitle = title.AddComponent<TextMeshProUGUI>();
        ComboTitle.font = AssetLoader.FontAsset;
        ComboTitle.fontSize = Main.Settings?.ComboTitleFontSize ?? 40;
        ComboTitle.text = Main.Settings.Labels.ComboTitle;
        ComboTitle.alignment = TextAlignmentOptions.Center;
        var fitter = title.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ShadowManager.ApplyShadow(ComboTitle);

        var val = new GameObject("ComboValue");
        t = val.AddComponent<RectTransform>();
        t.SetParent(ComboTransform);
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.45f);
        t.anchoredPosition = Vector2.zero;
        t.sizeDelta = new Vector2(300, 0);
        ComboTextTransform = t;
        ComboText = val.AddComponent<TextMeshProUGUI>();
        ComboText.font = AssetLoader.FontAsset;
        ComboText.fontSize = Main.Settings?.ComboValFontSize ?? 108;
        ComboText.alignment = TextAlignmentOptions.Top;
        fitter = val.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ShadowManager.ApplyShadow(ComboText);
        _comboObject = go;
    }

    protected void InitializeProgressBar()
    {
        // 进度条改为纯代码构建（原 bundle 里的 prefab 三个 Image 都只引用 Unity 内置
        // Background 精灵，无自定义贴图，代码重建可逐字段对齐）。
        // / The progress bar is now built entirely in code: the old prefab's three Images all
        // referenced only Unity's built-in Background sprite with no custom textures, so a
        // code rebuild matches it field for field.
        var go = AssetLoader.CreateProgressBar(Canvas.transform);
        var t = go.GetComponent<RectTransform>();
        t.anchoredPosition = new Vector2(0, -10);
        ProgressBar = new ProgressBar(t);
        _progressBarObject = go;
    }

    protected void InitializeTimingScale()
    {
        var go = new GameObject("TimingScale");
        var t = go.AddComponent<RectTransform>();
        t.SetParent(Canvas.transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0);
        t.sizeDelta = new Vector2(300, 30);
        TimingScaleText = go.AddComponent<TextMeshProUGUI>();
        TimingScaleText.font = AssetLoader.FontAsset;
        TimingScaleText.fontSize = Main.Settings?.TimingFontSize ?? 20;
        TimingScaleText.alignment = TextAlignmentOptions.Bottom;
        ShadowManager.ApplyShadow(TimingScaleText);
        _timingScaleObject = go;
    }

    protected void InitializeAttempt()
    {
        var go = new GameObject("Attempt");
        var t = go.AddComponent<RectTransform>();
        t.SetParent(Canvas.transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0);
        t.anchoredPosition = new Vector2(310, 35);
        t.sizeDelta = new Vector2(300, 30);
        AttemptText = go.AddComponent<TextMeshProUGUI>();
        AttemptText.font = AssetLoader.FontAsset;
        AttemptText.fontSize = Main.Settings?.AttemptFontSize ?? 25;
        AttemptText.alignment = TextAlignmentOptions.BottomLeft;
        ShadowManager.ApplyShadow(AttemptText);
        _attemptObject = go;
    }

    internal static float? _originalLevelNameSizeX;
    internal static Vector2? _originalLevelNamePos;
    internal static Vector3? _originalLevelNameScale;
    internal static string _originalLevelNameText;

    internal static void ResetLevelName()
    {
        var levelName = GameRefs.Controller?.txtLevelName;
        if (levelName != null)
        {
            if (_originalLevelNamePos != null)
            {
                var rt = levelName.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = _originalLevelNamePos.Value;
                    rt.localScale = _originalLevelNameScale ?? Vector3.one;
                    if (_originalLevelNameSizeX != null)
                        rt.sizeDelta = new Vector2(_originalLevelNameSizeX.Value, rt.sizeDelta.y);
                }
                // Restore the game's original-position reference to match.
                if (GameRefs.Controller != null)
                    GameRefs.Controller.txtLevelNameOriginalPosition = _originalLevelNamePos;
            }
            if (_originalLevelNameText != null)
                levelName.text = _originalLevelNameText;
        }
        _originalLevelNameSizeX = null;
        _originalLevelNamePos = null;
        _originalLevelNameScale = null;
        _originalLevelNameText = null;
    }

    static void ApplyLevelNamePatch()
    {
        var ln = GameRefs.Controller?.txtLevelName;
        if (ln == null) return;
        var rt = ln.GetComponent<RectTransform>();
        if (rt == null) return;

        if (_originalLevelNamePos == null)
        {
            _originalLevelNamePos = rt.anchoredPosition;
            _originalLevelNameScale = rt.localScale;
            _originalLevelNameSizeX = rt.sizeDelta.x;
            _originalLevelNameText = ln.text;
        }

        float size = Main.Settings.Size;
        rt.anchoredPosition = new Vector2(0, -20 - 7 * size);
        rt.localScale = new Vector3(0.5f * size, 0.5f * size);
        rt.sizeDelta = new Vector2(Math.Abs(_originalLevelNameSizeX.Value) * 2.5f, rt.sizeDelta.y);
        ln.text = ln.text.Replace('\n', ' ');
        // Keep the game's internal original-position reference in sync so that
        // SetDefaultText events (which compute a delta from that baseline) don't
        // yank the title back to its pre-patch position.
        if (GameRefs.Controller != null)
            GameRefs.Controller.txtLevelNameOriginalPosition = rt.anchoredPosition;
    }

    public void UpdateSize()
    {
        var t = GameObject.transform;
        float size = Main.Settings.Size;
        var scale = new Vector3(size, size, 1);
        for (int i = 0; i < t.childCount; i++) t.GetChild(i).localScale = scale;
        if (TimingScaleText) TimingScaleText.rectTransform.anchoredPosition = new Vector2(0, 90 + 40 * size);
        if (Main.Settings.PatchLevelName) ApplyLevelNamePatch();
        if (ComboTransform) ComboTransform.anchoredPosition = new Vector2(0, -43 - 14 * size);
    }

    public void ApplyFontToAll()
    {
        var s = Main.Settings;
        ShadowManager.ClearCache();

        var mainFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.Main));
        if (ProgressText) ProgressText.font = mainFont;
        if (AccuracyText) AccuracyText.font = mainFont;
        if (XAccuracyText) XAccuracyText.font = mainFont;
        if (TimeText) TimeText.font = mainFont;
        if (MapTimeText) MapTimeText.font = mainFont;
        if (CheckpointText) CheckpointText.font = mainFont;
        if (BestText) BestText.font = mainFont;

        var bpmFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.BPM));
        if (BPMText) BPMText.font = bpmFont;

        var judgeFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.Judgement));
        foreach (var jt in JudgementTexts) if (jt) jt.font = judgeFont;

        var ctFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.ComboTitle));
        if (ComboTitle) ComboTitle.font = ctFont;

        var cvFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.ComboVal));
        if (ComboText) ComboText.font = cvFont;

        var timingFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.Timing));
        if (TimingScaleText) TimingScaleText.font = timingFont;

        var attemptFont = FontManager.GetFont(s.GetFontIndexForSlot(Settings.FontSlot.Attempt));
        if (AttemptText) AttemptText.font = attemptFont;

        foreach (var t in ExtraTexts)
            if (t) t.font = mainFont;
        foreach (var t in new[] { ProgressText, AccuracyText, XAccuracyText, TimeText, MapTimeText, CheckpointText, BestText,
            BPMText, TimingScaleText, AttemptText })
        { if (t) ShadowManager.ApplyShadow(t); }
        foreach (var jt in JudgementTexts)
        { if (jt) ShadowManager.ApplyShadow(jt); }
        foreach (var t in ExtraTexts)
        { if (t) ShadowManager.ApplyShadow(t); }
        if (ComboTitle) ShadowManager.ApplyShadow(ComboTitle);
        if (ComboText) ShadowManager.ApplyShadow(ComboText);
        ApplyFontSizes();
    }

    public void ApplyFontSizes()
    {
        var s = Main.Settings;
        int mainSize = s.MainFontSize;
        if (ProgressText) ProgressText.fontSize = mainSize;
        if (AccuracyText) AccuracyText.fontSize = mainSize;
        if (XAccuracyText) XAccuracyText.fontSize = mainSize;
        if (TimeText) TimeText.fontSize = mainSize;
        if (MapTimeText) MapTimeText.fontSize = mainSize;
        if (CheckpointText) CheckpointText.fontSize = mainSize;
        if (BestText) BestText.fontSize = mainSize;
        if (BPMText) BPMText.fontSize = s.BPMFontSize;
        foreach (var jt in JudgementTexts) if (jt) jt.fontSize = s.JudgeFontSize;
        if (ComboTitle) ComboTitle.fontSize = s.ComboTitleFontSize;
        if (ComboText) ComboText.fontSize = s.ComboValFontSize;
        if (TimingScaleText) TimingScaleText.fontSize = s.TimingFontSize;
        if (AttemptText) AttemptText.fontSize = s.AttemptFontSize;
        foreach (var t in ExtraTexts)
            if (t) t.fontSize = mainSize;
    }

    public void ApplyAlignment()
    {
        var s = Main.Settings;
        var mainAlign = (TextAlignmentOptions)s.MainAlign;
        if (BPMText) BPMText.alignment = (TextAlignmentOptions)s.BPMAlign;
        var judgeAlign = (TextAlignmentOptions)s.JudgeAlign;
        if (JudgementTexts[0]) JudgementTexts[0].alignment = judgeAlign;
        if (ComboTitle) ComboTitle.alignment = (TextAlignmentOptions)s.ComboAlign;
        if (ComboText) ComboText.alignment = (TextAlignmentOptions)s.ComboValAlign;
        if (TimingScaleText) TimingScaleText.alignment = (TextAlignmentOptions)s.TimingAlign;
        if (AttemptText) AttemptText.alignment = (TextAlignmentOptions)s.AttemptAlign;
        if (ProgressText) ProgressText.alignment = mainAlign;
        if (AccuracyText) AccuracyText.alignment = mainAlign;
        if (XAccuracyText) XAccuracyText.alignment = mainAlign;
        if (TimeText) TimeText.alignment = mainAlign;
        if (MapTimeText) MapTimeText.alignment = mainAlign;
        if (CheckpointText) CheckpointText.alignment = mainAlign;
        if (BestText) BestText.alignment = mainAlign;
        foreach (var t in ExtraTexts)
            if (t) t.alignment = mainAlign;
    }

    public void ApplyFontStyle()
    {
        var s = Main.Settings;
        var mainStyle = (FontStyles)s.MainStyle;
        if (ProgressText) ProgressText.fontStyle = mainStyle;
        if (AccuracyText) AccuracyText.fontStyle = mainStyle;
        if (XAccuracyText) XAccuracyText.fontStyle = mainStyle;
        if (TimeText) TimeText.fontStyle = mainStyle;
        if (MapTimeText) MapTimeText.fontStyle = mainStyle;
        if (CheckpointText) CheckpointText.fontStyle = mainStyle;
        if (BestText) BestText.fontStyle = mainStyle;
        if (BPMText) BPMText.fontStyle = (FontStyles)s.BPMStyle;
        var judgeStyle = (FontStyles)s.JudgeStyle;
        foreach (var jt in JudgementTexts) if (jt) jt.fontStyle = judgeStyle;
        if (ComboTitle) ComboTitle.fontStyle = (FontStyles)s.ComboStyle;
        if (ComboText) ComboText.fontStyle = (FontStyles)s.ComboValStyle;
        if (TimingScaleText) TimingScaleText.fontStyle = (FontStyles)s.TimingStyle;
        if (AttemptText) AttemptText.fontStyle = (FontStyles)s.AttemptStyle;
        foreach (var t in ExtraTexts)
            if (t) t.fontStyle = mainStyle;
    }

    public void ApplyPositionOffsets()
    {
        var s = Main.Settings;
        // Reset to default anchored positions
        if (_mainContainer)
            _mainContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -16);
        if (_bpmObject)
            _bpmObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(-16, -16);
        bool coopJudge = VersionSafe.IsCoopMode();
        int judgeCount = coopJudge ? Math.Min(VersionSafe.GetPlayerCount(), 4) : 1;
        for (int i = 0; i < 4; i++)
        {
            if (!_judgementObjects[i] || i >= judgeCount) continue;
            JudgementTexts[i].rectTransform.anchoredPosition = coopJudge
                ? new Vector2(i % 2 == 0 ? -250 : 250, 35 - 30 * (i / 2))
                : new Vector2(0, s.JudgementLocationUp ? 85 : 5);
        }
        if (ComboTransform)
            ComboTransform.anchoredPosition = new Vector2(0, -43 - 14 * s.Size);
        if (_timingScaleObject)
            TimingScaleText.rectTransform.anchoredPosition = new Vector2(0, 90 + 40 * s.Size);
        if (_attemptObject)
            _attemptObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(VersionSafe.IsCoopMode() && VersionSafe.GetPlayerCount() > 1 ? 550 : 310, 35);
        if (_progressBarObject)
            _progressBarObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -10);

        if (!s.CustomPositionsEnabled) return;
        var o = Main.Settings;
        // Offsets are in canvas reference units (1920x1080), so apply them in
        // anchoredPosition space to scale with the CanvasScaler across resolutions.
        if (_mainContainer)
            _mainContainer.GetComponent<RectTransform>().anchoredPosition += new Vector2(o.MainOffsetX, o.MainOffsetY);
        if (_bpmObject)
            _bpmObject.GetComponent<RectTransform>().anchoredPosition += new Vector2(o.BPMOffsetX, o.BPMOffsetY);
        for (int i = 0; i < 4; i++)
        {
            if (!_judgementObjects[i]) continue;
            bool coop = VersionSafe.IsCoopMode() && VersionSafe.GetPlayerCount() > 1;
            float ox, oy;
            if (i == 0 && !coop) { ox = o.JudgeOffsetX; oy = o.JudgeOffsetY; }
            else { ox = i == 0 ? o.P1JudgeOffsetX : i == 1 ? o.P2JudgeOffsetX : i == 2 ? o.P3JudgeOffsetX : o.P4JudgeOffsetX;
                oy = i == 0 ? o.P1JudgeOffsetY : i == 1 ? o.P2JudgeOffsetY : i == 2 ? o.P3JudgeOffsetY : o.P4JudgeOffsetY; }
            JudgementTexts[i].rectTransform.anchoredPosition += new Vector2(ox, oy);
        }
        if (ComboTransform)
            ComboTransform.anchoredPosition += new Vector2(o.ComboOffsetX, o.ComboOffsetY);
        if (_timingScaleObject)
            TimingScaleText.rectTransform.anchoredPosition += new Vector2(o.TimingOffsetX, o.TimingOffsetY);
        if (_attemptObject)
        {
            bool coop = VersionSafe.IsCoopMode() && VersionSafe.GetPlayerCount() > 1;
            _attemptObject.GetComponent<RectTransform>().anchoredPosition += new Vector2(coop ? o.AttemptCoopOffsetX : o.AttemptOffsetX, coop ? o.AttemptCoopOffsetY : o.AttemptOffsetY);
        }
        if (_progressBarObject)
            _progressBarObject.GetComponent<RectTransform>().anchoredPosition += new Vector2(o.ProgBarOffsetX, o.ProgBarOffsetY);
    }

    public void RefreshVisibility()
    {
        var s = Main.Settings;
        if (_mainContainer) _mainContainer.SetActive(s.AnyStackedTextVisible);
        if (_bpmObject) { _bpmObject.SetActive(s.ShowBPM); if (s.ShowBPM && GameObject.activeSelf) UpdateBPM(); }
        for (int i = 0; i < 4; i++) if (_judgementObjects[i]) _judgementObjects[i].SetActive(s.ShowJudgement && i < (VersionSafe.IsCoopMode() && VersionSafe.GetPlayerCount() > 1 ? Math.Min(VersionSafe.GetPlayerCount(), 4) : 1)); if (s.ShowJudgement) { SetupLocationJudgement(); if (GameObject.activeSelf) UpdateJudgement(); }
        if (_comboObject) { _comboObject.SetActive(s.ShowCombo); if (s.ShowCombo && GameObject.activeSelf) UpdateCombo(Features.GameLifecycleHelper.ComboCount, false); }
        if (_timingScaleObject) { _timingScaleObject.SetActive(s.ShowTimingScale); if (s.ShowTimingScale && GameObject.activeSelf) UpdateTimingScale(); }
        if (_attemptObject) { _attemptObject.SetActive(s.ShowAttempt || s.ShowFullAttempt); if (_attemptObject.activeSelf) UpdateAttempts(); }
        if (_progressBarObject) { _progressBarObject.SetActive(s.ShowProgressBar); if (s.ShowProgressBar && GameObject.activeSelf) UpdateProgressBar(); }
        if (GameObject != null && GameObject.activeSelf)
        {
            SetupLocationMain();
            if (s.PatchBetaWatermark) AdjustBetaWatermark(s.Size);
            else ResetBetaWatermark();
            if (s.PatchLevelName) UpdateSize();
            else ResetLevelName();
            if (s.RepositionAutoText) RepositionAutoText(_mainContainer != null && _mainContainer.activeSelf, s.Size);
            else ResetAutoTextPosition();
        }
        ApplyPositionOffsets();
        ApplyAlignment();
        ApplyFontStyle();
        ApplyFontSizes();
        RefreshTimeLabels();
    }

    private static void AdjustBetaWatermark(float size)
    {
        if (BetaWatermark == null || !BetaWatermark.gameObject.activeInHierarchy) return;
        var rt = BetaWatermark.GetComponent<RectTransform>();
        if (rt == null) return;
        if (BetaWatermarkOriginalPos == null)
            BetaWatermarkOriginalPos = rt.anchoredPosition;
        var pos = rt.anchoredPosition;
        pos.y = BetaWatermarkOriginalPos.Value.y - (Main.Settings.ShowBPM ? 110f * size : 0);
        rt.anchoredPosition = pos;
    }

    internal static void ResetBetaWatermark()
    {
        if (BetaWatermark == null || BetaWatermarkOriginalPos == null) return;
        var rt = BetaWatermark.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchoredPosition = BetaWatermarkOriginalPos.Value;
    }

    internal static int[] CollectCheckpoints()
    {
        var floors = GameRefs.LevelMaker?.listFloors;
        if (floors == null) return Array.Empty<int>();
        int count = 0;
        for (int i = 0; i < floors.Count; i++)
            if (floors[i].GetComponent<ffxCheckpoint>()) count++;
        int[] result = new int[count];
        int idx = 0;
        for (int i = 0; i < floors.Count; i++)
            if (floors[i].GetComponent<ffxCheckpoint>()) result[idx++] = floors[i].seqID;
        return result;
    }



    public void UpdateAccuracy(int index = -1)
    {
        if (!GameObject.activeSelf) return;
        OverlayTextManager?.UpdateAccuracy(this, index);
    }

    public void UpdateProgress(scrPlanet planet = null)
    {
        var s = Main.Settings;
        if (!GameObject.activeSelf) return;
        OverlayTextManager?.CacheProgress(planet);
        if (s.ShowProgress) OverlayTextManager?.UpdateProgress(this);
        if (s.ShowCheckpoint) UpdateCheckPointText();
        if (s.ShowProgressBar) UpdateProgressBar();
        if (s.ShowBest) OverlayTextManager?.UpdateBest(this);
        ExtendedOverlay?.CheckPurePerfect();
        ExtendedOverlay?.UpdateState();
        ExtendedOverlay?.UpdateDeath();
        ExtendedOverlay?.UpdateStart();
        ExtendedOverlay?.UpdateColors();
    }

    public void UpdateProgressBar()
    {
        try { if (ProgressBar?.LineTransform != null) OverlayTextManager?.UpdateProgressBar(this); }
        catch (Exception e) { Loader.Warning($"ProgressBar: {e.Message}"); }
    }

    public void UpdateCheckPointText()
    {
        if (Checkpoints == null || Checkpoints.Length == 0) return;
        OverlayTextManager?.UpdateCheckpoint(this);
    }

    public void UpdateAttempts()
    {
        var s = Main.Settings;
        var labels = s.Labels;
        var order = s.AttemptLineOrder;
        int attemptCount = PlayCount.GetData(LastHash)?.GetAttempts(StartProgress) ?? 0;
        int fullAttemptCount = PlayCount.GetData(LastHash)?.GetAttempts() ?? 0;
        bool showA = s.ShowAttempt;
        bool showF = s.ShowFullAttempt;
        _attemptSb.Clear();
        for (int i = 0; i < order.Length; i++)
        {
            int line = order[i];
            if (line == 0 && showA)
            {
                if (_attemptSb.Length > 0) _attemptSb.Append('\n');
                _attemptSb.Append($"{labels.Attempt} {attemptCount}");
            }
            else if (line == 1 && showF)
            {
                if (_attemptSb.Length > 0) _attemptSb.Append('\n');
                _attemptSb.Append($"{labels.FullAttempt} {fullAttemptCount}");
            }
        }
        AttemptText.SetText(_attemptSb);
    }

    private static void AppendJudgementLine(StringBuilder sb, int[] h)
    {
        // 全部按运行时解析的语义下标取值：r148 与 r150 的 HitMargin 取值位移不同，
        // 直接写死下标会让判定数字整体错位（如 r150 的 h[3] 是 PerfectMinus 而非 Perfect）。
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.FailOverload));
        sb.Append(" <color=red>");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.TooEarly));
        sb.Append(" <color=#FF6F4E>");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.VeryEarly));
        sb.Append(" <color=#A0FF4E>");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.EarlyPerfect));
        sb.Append(" <color=#60FF4E>");
        sb.Append(HitMarginCompat.SumCorePerfect(h) + HitMarginCompat.Get(h, HitMarginCompat.Auto));
        sb.Append("</color> ");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.LatePerfect));
        sb.Append("</color> ");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.VeryLate));
        sb.Append("</color> ");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.TooLate));
        sb.Append("</color> ");
        sb.Append(HitMarginCompat.Get(h, HitMarginCompat.FailMiss));
    }

    public void UpdateJudgement()
    {
        if (!GameObject.activeSelf || Hit == null) return;
        bool isCoop = VersionSafe.IsCoopMode();
        int playerCount = VersionSafe.GetPlayerCount();
        // r150 基础游戏原生提供 XPerfect；r148 依赖外部 XPerfect mod
        bool useXPerfect = Main.Settings.ShowXPerfectInJudgement && HitMarginCompat.XPerfectDisplayAvailable;

        if (isCoop && playerCount > 1)
        {
            int count = Math.Min(playerCount, 4);
            for (int p = 0; p < count; p++)
            {
                if (JudgementTexts[p] == null) continue;
                var h = VersionSafe.GetHitMarginsCountForPlayer(p);
                string hex = VersionSafe.GetPlayerColorHex(p);
                string prefix = $"<color=#{hex}>P{p + 1}</color> ";
                string suffix = $" <color=#{hex}>P{p + 1}</color>";
                JudgementTexts[p].text = BuildJudgementString(h, useXPerfect, prefix, suffix, p);
            }
        }
        else
        {
            JudgementTexts[0].text = BuildJudgementString(Hit, useXPerfect);
        }
    }

    private string BuildJudgementString(int[] h, bool useXPerfect, string prefix = "", string suffix = "", int player = 0)
    {
        _judgementSb.Clear();
        _judgementSb.Append(prefix);

        if (!useXPerfect)
        {
            AppendJudgementLine(_judgementSb, h);
        }
        else
        {
            // r150 基础游戏原生统计 XPerfect/PerfectMinus/PerfectPlus；
            // r148 无此判定，回退到外部 XPerfect mod 的计数。
            HitMarginCompat.GetPerfectBreakdown(h, player, out int plus, out int x, out int minus);

            // 完全复用原版的嵌套颜色结构，仅替换绿色数字为 + X -
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.FailOverload));
            _judgementSb.Append(" <color=red>");
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.TooEarly));
            _judgementSb.Append(" <color=#FF6F4E>");
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.VeryEarly));
            _judgementSb.Append(" <color=#A0FF4E>");
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.EarlyPerfect));
            _judgementSb.Append(" <color=#60FF4E>");

            // 中心完美三分量的顺序必须与时间轴一致：稍快(-)在左、X 居中、稍慢(+)在右，
            // 与整行 TooEarly(最左)…TooLate(最右) 的排列约定相同。
            // r150：PerfectMinus=稍快、PerfectPlus=稍慢（SelectHitMarginByTimeBoundary 按
            // 带符号时间差判定：负=早、正=晚）。旧代码沿袭外部 XPerfect mod 打成 plus→x→minus，
            // 会让稍快的计数落到右侧槽位。
            _judgementSb.Append(minus);                         // -Perfect（稍快，绿）
            _judgementSb.Append("</color> <color=#").Append(GameCompat.XPerfectHex).Append(">");
            _judgementSb.Append(x);                             // X-Perfect（r150 跟随游戏配色，默认白）
            _judgementSb.Append("</color> <color=#60FF4E>");
            _judgementSb.Append(plus);                          // +Perfect（稍慢，绿）
            int autoCount = HitMarginCompat.Get(h, HitMarginCompat.Auto);
            if (Main.Settings.ShowAutoInXPerfect && autoCount > 0)
            {
                _judgementSb.Append(" <color=#FF8000>");
                _judgementSb.Append(autoCount);
                _judgementSb.Append("</color>");
            }
            _judgementSb.Append("</color> ");                  // 关闭绿色，栈顶回到黄绿
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.LatePerfect));
            _judgementSb.Append("</color> ");                  // 关闭黄绿，栈顶回到橙
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.VeryLate));
            _judgementSb.Append("</color> ");                  // 关闭橙，栈顶回到红
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.TooLate));
            _judgementSb.Append("</color> ");                  // 关闭红，栈顶回到默认紫
            _judgementSb.Append(HitMarginCompat.Get(h, HitMarginCompat.FailMiss));
        }

        _judgementSb.Append(suffix);
        return _judgementSb.ToString();
    }

    public void UpdateTime()
    {
        // 统一走模块实现：它是原两份实现的超集（10fps 节流、缓存、无音频时回退地图时间），
        // 唯一可见差异是时间显示带一位小数（FormatWithDecimals）
        ExtendedOverlay.UpdateTime();
    }


    public void UpdateCombo(int combo, bool bump)
    {
        if (!GameObject.activeSelf) return;
        _comboSb.Clear();
        _comboSb.Append(combo);
        ComboText.SetText(_comboSb);
        ComboText.color = UpdateComboColor(combo);
        bool reversed = Main.Settings.ComboLineReversed;
        if (bump) { if (_mono) _mono.StartComboBump(); }
        else
        {
            if (_mono) _mono.StopComboBump();
            ComboText.fontSize = Main.Settings.ComboValFontSize;
            if (reversed)
            {
                ComboTextTransform.anchoredPosition = new Vector2(0, 43.505f);
                if (_comboTitleTransform) _comboTitleTransform.anchoredPosition = new Vector2(0, -40f);
            }
            else
            {
                ComboTextTransform.anchoredPosition = Vector2.zero;
                if (_comboTitleTransform) _comboTitleTransform.anchoredPosition = new Vector2(0, 43.505f);
            }
        }
    }

    public Color UpdateComboColor(int combo)
    {
        var s = Main.Settings;
        // ExtendedOverlay 配色（按完美率渐变）只在开启宽松连击时接管，普通模式保持原渐变
        if (s.AllowELCombo) return ExtendedOverlay.UpdateComboColor(combo);
        if (combo > s.ComboColorMax) combo = s.ComboColorMax;
        return s.Colors.GetComboColor((float)combo / s.ComboColorMax);
    }

    // 连击标题切换（COMBO → 备用文本）属于宽松连击的表现，仅在相应功能开启时生效
    public void OnNonPerfectHit()
    {
        var s = Main.Settings;
        if (s.AllowELCombo || s.AllowOrangeCombo) ExtendedOverlay.OnNonPerfectHit();
    }

    public void UpdateBPM()
    {
        // 统一走模块实现：它是普通版的重集——CheckPseudo 关闭时输出与普通版一致，
        // 开启时附加伪 BPM 检测与 KPS 着色
        ExtendedOverlay.UpdateBPM();
    }

    /// <summary>判定时间窗显示值（整毫秒）是否变化；变化时记录新值。按显示值比较，显示未变就不触发 TMP 重建。</summary>
    internal bool TimingWindowChanged(in TimingWindowCalculator.Result tw)
    {
        int x = tw.XPerfectValid ? (int)Math.Round(tw.XPerfectMs) : -1;
        int p = (int)Math.Round(tw.PerfectMs), gr = (int)Math.Round(tw.GreatMs), gd = (int)Math.Round(tw.GoodMs);
        if (p == _lastTwP && gr == _lastTwGr && gd == _lastTwGd && x == _lastTwX) return false;
        _lastTwP = p; _lastTwGr = gr; _lastTwGd = gd; _lastTwX = x;
        return true;
    }

    /// <summary>构造追加到 BPM 下方的判定时间窗行（每行一个判定）。</summary>
    internal string BuildTimingWindowLines(in TimingWindowCalculator.Result tw)
    {
        var s = Main.Settings;
        _twSb.Clear();
        if (tw.XPerfectValid)
            _twSb.Append("\n<color=#").Append(GameCompat.XPerfectHex).Append(">").Append(s.Labels.XPerfectLabel).Append("</color> | ±").Append(Math.Round(tw.XPerfectMs)).Append("ms");
        _twSb.Append("\n<color=#60FF4E>").Append(s.Labels.PerfectLabel).Append("</color> | ±").Append(Math.Round(tw.PerfectMs)).Append("ms");
        _twSb.Append("\n<color=#A0FF4E>").Append(s.Labels.GreatLabel).Append("</color> | ±").Append(Math.Round(tw.GreatMs)).Append("ms");
        _twSb.Append("\n<color=#FF6F4E>").Append(s.Labels.GoodLabel).Append("</color> | ±").Append(Math.Round(tw.GoodMs)).Append("ms");
        return _twSb.ToString();
    }

    public void DirtyBpmCache() { LastTileBpm = LastCurBpm = -1; }

    public static string BuildBpmText(int[] order, string hex, Settings s, double tileBpm, double curBpm, double kps, string kpsPrefix = "", string kpsSuffix = "")
    {
        _bpmSb.Clear();
        var labels = s.Labels;
        var vis = s.BpmLineVisibility;
        for (int i = 0; i < order.Length; i++)
        {
            int id = order[i];
            if (vis == null || id >= vis.Length || !vis[id]) continue;
            if (_bpmSb.Length > 0) _bpmSb.Append('\n');
            switch (id)
            {
                case 0: // Tile BPM
                    _bpmSb.Append($"<color=white>{labels.TBPM} | <color=#{hex}>{Math.Round(tileBpm, 2)}</color></color>");
                    break;
                case 1: // Current BPM
                    _bpmSb.Append($"<color=white>{labels.CBPM} |</color> {Math.Round(curBpm, 2)}");
                    break;
                case 2: // KPS
                    _bpmSb.Append($"<color=white>{labels.KPS} |</color> {kpsPrefix}{Math.Round(kps, 2)}{kpsSuffix}");
                    break;
            }
        }
        return _bpmSb.ToString();
    }

    internal void RefreshTimeLabels()
    {
        var s = Main.Settings;
        _musicTimeLabel = $"<color=white>{s.Labels.MusicTime} |</color>";
        _mapTimeLabel = $"<color=white>{s.Labels.MapTime} |</color>";
    }

    private static scrShowIfDebug _autoText;
    private static Vector2? _autoTextOriginalPos;

    private static void RepositionAutoText(bool needRoom, float size = 1)
    {
        if (_autoText == null)
        {
            var all = Resources.FindObjectsOfTypeAll<scrShowIfDebug>();
            foreach (var s in all)
            {
                if (!s.gameObject.scene.IsValid()) continue;
                if (!VersionSafe.GetHideWithNoAuto(s))
                    continue;
                _autoText = s;
                break;
            }
        }
        if (_autoText == null) return;
        var rt = _autoText.GetComponent<RectTransform>();
        if (rt == null) return;
        if (_autoTextOriginalPos == null)
            _autoTextOriginalPos = rt.anchoredPosition;
        var pos = rt.anchoredPosition;
        pos.x = needRoom ? 300f * size : _autoTextOriginalPos.Value.x;
        rt.anchoredPosition = pos;
    }

    internal static void ResetAutoTextPosition()
    {
        if (_autoText != null && _autoTextOriginalPos != null)
        {
            var rt = _autoText.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = _autoTextOriginalPos.Value;
        }
        _autoText = null;
        _autoTextOriginalPos = null;
    }

    public void UpdateTimingScale()
    {
        if (!GameObject.activeSelf || GameRefs.CurrentFloor == null) return;
        float cur = (float)Math.Round(GameRefs.CurrentFloor.marginScale * 100, 2);
        if (Math.Abs(cur - _lastTimingScale) < 0.001f) return;
        _lastTimingScale = cur;
        _timingSb.Clear();
        _timingSb.Append(Main.Settings.Labels.TimingScale);
        _timingSb.Append(" - ");
        _timingSb.Append(cur);
        _timingSb.Append('%');
        TimingScaleText.SetText(_timingSb);
    }

    /// <summary>自定义标签编辑后强制重绘全部文本：先清空各更新路径的"值未变则跳过"节流，再逐个刷新。</summary>
    internal void RefreshAllTexts()
    {
        if (ComboTitle)
            ComboTitle.text = ExtendedOverlay is { IsAltComboTitle: true }
                ? Main.Settings.Labels.ComboTitleAlt
                : Main.Settings.Labels.ComboTitle;
        RefreshTimeLabels();
        LastTime = -1;
        LastMapTime = -1;
        _lastTimingScale = -1;
        DirtyBpmCache();
        OverlayTextManager?.DirtyTextCaches();
        ExtendedOverlay?.DirtyTextCaches();
        ExtendedOverlay?.UpdateAuthor();
        ExtendedOverlay?.RefreshTiming();
        UpdateProgress();     // 进度/检查点/进度条/最佳 + ExtendedOverlay State/Death/Start/Colors
        UpdateTime();
        UpdateAccuracy();
        UpdateBPM();
        UpdateJudgement();
        UpdateCombo(Features.GameLifecycleHelper.ComboCount, false);
        UpdateTimingScale();
        UpdateAttempts();
    }

    public void Show(int floor, bool suppressNativeUI = false)
    {
        var s = Main.Settings;
        ExtendedOverlay?.OnShow(floor);
        // 每次显示都同步自定义标签——覆盖层隐藏期间编辑的标签也能生效。
        // 扩展叠加层下与 OnShow 同条件：checkpoint 续命刻意保留备用标题，不能在此覆盖。
        if (ComboTitle && (ExtendedOverlay == null || GameRefs.CheckpointsUsed == 0))
            ComboTitle.text = s.Labels.ComboTitle;
        if (_lastSavedStartProgress != -1 && _lastSavedFromStart)
        {
            if (!AutoOnceEnabled) PlayCount.SetBest(LastHash, _lastSavedStartProgress, OverlayTextManager.GetProgress(), LastMultiplier);
            _lastSavedStartProgress = -1;
        }
        var hash = PlayCount.GetMapHash();
        if (LastHash != hash) { LastHash = hash; Checkpoints = null; MapTimeCache = null; }
        MusicTimeCache = null;
        if (GameRefs.EditorInstance != null) { if (GameRefs.CheckpointsUsed == 0) NoCheckStartTile = floor; }
        else if (!GCS.practiceMode) NoCheckStartTile = 0;
        else NoCheckStartTile = floor;
        AutoOnceEnabled = GameRefs.IsAuto || GameRefs.IsNoFail;
        StartTile = floor;
        var floors = GameRefs.LevelMaker?.listFloors;
        _lastSavedStartProgress = StartProgress = floors != null && floors.Count > 0 ? (float)(floor + 1) / floors.Count : 0f;
        _lastSavedFromStart = floor == 0;
        // 尝试/最佳记录的倍速键只用歌曲音高：行星速度会随 BPM 事件在关卡中变化，
        // 混入会导致同一地图产生多套统计 key（数据错误）。
        LastMultiplier = (float)GameRefs.SongPitch;
        if (!AutoOnceEnabled) PlayCount.AddAttempts(LastHash, StartProgress);
        SetupTextManager();
        ApplyFontToAll();
        GameObject.SetActive(true);
        if (_mono) _mono.enabled = true;
        SongPlaying = false; IsDeath = false;

        // 任意可栈排元素开启即需要布局（原条件漏了 Accuracy/XAccuracy/MapTime，
        // 只开精度时栈不会被摆位——顺手修正；XScore/潜力值同样并入）
        if (s.AnyStackedTextVisible)
            SetupLocationMain();
        OverlayTextManager.SeedProgress(StartProgress);
        if (s.ShowProgress || s.ShowProgressBar || s.ShowBest)
        {
            if (s.ShowProgress) OverlayTextManager.UpdateProgress(this);
            if (s.ShowProgressBar) UpdateProgressBar();
            if (s.ShowBest) OverlayTextManager.UpdateBest(this);
        }
        if (s.ShowJudgement) { SetupLocationJudgement(); UpdateJudgement(); }
        if (s.ShowCombo) UpdateCombo(0, false);
        if (s.ShowBPM) UpdateBPM();
        if (!suppressNativeUI && s.PatchBetaWatermark) AdjustBetaWatermark(s.Size);
        if (s.PatchLevelName) ApplyLevelNamePatch();
        if (s.ShowTimingScale) UpdateTimingScale();
        if (s.ShowAttempt) UpdateAttempts();
        ApplyPositionOffsets();
        ApplyAlignment();
        ApplyFontStyle();
        Features.GameLifecycleHelper.ComboCount = 0;
        if (!suppressNativeUI && s.RepositionAutoText) RepositionAutoText(s.AnyStackedTextVisible, s.Size);
        RefreshTimeLabels();
    }

    public void Death()
    {
        IsDeath = true;
        if (AutoOnceEnabled || _lastSavedStartProgress == -1 || !_lastSavedFromStart) return;
        PlayCount.SetBest(LastHash, _lastSavedStartProgress, OverlayTextManager.GetProgress(), LastMultiplier);
        PlayCount.Save();
        _lastSavedStartProgress = -1;
        OverlayTextManager.SetBest(OverlayTextManager.GetProgress());
    }

    public void Clear()
    {
        if (AutoOnceEnabled || _lastSavedStartProgress == -1 || !_lastSavedFromStart) return;
        PlayCount.SetBest(LastHash, _lastSavedStartProgress, 1, LastMultiplier);
        _lastSavedStartProgress = -1;
        OverlayTextManager.SetBest(1);
    }

    public void Hide()
    {
        ExtendedOverlay?.OnHide();
        if (Main.Settings.PatchBetaWatermark) ResetBetaWatermark();
        if (Main.Settings.RepositionAutoText) RepositionAutoText(false);
        _autoText = null;
        _autoTextOriginalPos = null;
        ResetLevelName();
        if (GameObject == null || !GameObject.activeSelf) return;
        GameObject.SetActive(false);
        if (_mono) _mono.enabled = false;
        try
        {
            if (!AutoOnceEnabled && _lastSavedStartProgress != -1 && _lastSavedFromStart)
            {
                PlayCount.SetBest(LastHash, _lastSavedStartProgress, OverlayTextManager.GetProgress(), LastMultiplier);
                _lastSavedStartProgress = -1;
            }
            if (StartProgress == OverlayTextManager.GetProgress() && !AutoOnceEnabled)
                PlayCount.RemoveAttempts(LastHash, StartProgress);
        }
        catch (Exception e) { Loader.Warning($"Hide: {e.Message}"); }
        PlayCount.Save();
        StartProgress = StartTile = NoCheckStartTile = -1;
        OverlayTextManager = null;
    }

    public void Destroy()
    {
        ResetLevelName();
        if (Main.Settings.PatchBetaWatermark) ResetBetaWatermark();
        Object.Destroy(GameObject);
        GC.SuppressFinalize(this);
    }
}
