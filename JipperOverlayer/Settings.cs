using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using JipperOverlayer.Overlayer;
using JipperOverlayer.Overlayer.Features;
using JipperOverlayer.Overlayer.Util;
using JipperOverlayer.Overlayer.Localization;

namespace JipperOverlayer;

public class Settings
{
    public bool ShowProgress = true, ShowAccuracy, ShowXAccuracy = true;
    public bool ShowMusicTime = true, ShowMapTime, ShowMapTimeIfNotMusic = true;
    public bool ShowCheckpoint, ShowBest, ShowProgressBar = true;
    public bool ShowBPM = true, ShowCombo = true, ShowJudgement = true, ShowTimingScale = true;
    public bool ShowTimingWindow = true;
    public bool ShowAttempt = true, ShowFullAttempt = true;
    public float Size = 1f;
    public TextEffectConfig TextEffects = new();
    public bool JudgementLocationUp, EnableAutoCombo = true;
    public float BpmColorMax = 8000f;
    public int ComboColorMax = 1000;
    public bool ShowFPS = true, ShowAuthor = true, ShowState = true;
    // 原 扩展叠加层的文本样式差异，拆成独立开关：
    //   DetailedProgress —— 进度显示「当前/总数 [-剩余] (%)」富格式（否则只显示百分比）
    //   TimeDecimals     —— 音乐/地图时间带一位小数
    public bool DetailedProgress, TimeDecimals;

    // ==== 文本类型与 XScore ====
    public TimingTextType TimingTextType = TimingTextType.BothInOneLine;
    public XScoreTextType XScoreTextType = XScoreTextType.MaxMinus;
    public PotentialTextType AccuracyTextType = PotentialTextType.Current;
    public PotentialTextType XAccuracyTextType = PotentialTextType.Current;
    public PotentialTextType XScorePotentialType = PotentialTextType.Current;
    /// <summary>XScore 文本（仅 r149+：游戏原生 XPerfect 计分，X=2、Perfect±=1）</summary>
    public bool ShowXScore;

    // ==== 每文本独立小数位 ====
    public int ProgressDecimal = 2, AccuracyDecimal = 2, XAccuracyDecimal = 2, BestDecimal = 2, TimingDecimal = 5;

    /// <summary>任一 ExtendedOverlay 扩展文本开启。拆掉总开关后，用它决定「ExtendedOverlay 风格」的连带表现
    /// （富格式进度文本、扩展文本的小数精度等），避免普通模式用户被动接受这些变化。</summary>
    public bool ExtendedStyleActive => ShowFPS || ShowAuthor || ShowState || ShowDeath || ShowStart || ShowTiming;

    /// <summary>任一挂在主容器下的栈式文本开启（含 XScore 与三个潜力值文本）。
    /// 主容器一旦 SetActive(false)，其下所有文本都不渲染；布局也只在任一元素开启时才跑。
    /// 新增显示元素必须并入此处，否则会出现「开关打开、文本却永远不显示」。</summary>
    public bool AnyStackedTextVisible => ShowProgress || ShowAccuracy || ShowXAccuracy || ShowMusicTime
        || ShowMapTime || ShowCheckpoint || ShowBest || ShowXScore || ExtendedStyleActive;
    public float FPSRefreshRate = 0.2f;
    public int ExtendedDecimalPrecision = 2;
    public bool HideDebugText = true, ShowDeath = true, ShowStart = true, ShowTiming = true;
    public bool RemoveNotRequireInAuto = true, CheckPseudo = true, AllowELCombo = true, AllowOrangeCombo = true;
    public bool ComboTitleAltOnNonPerfect = false;
    public bool PatchBetaWatermark = true, PatchLevelName = true, RepositionAutoText = true;
    public Language CurrentLanguage;
    public int FontIndex;
    public string FontName;
    // Per-region font indices (-1 = use global FontIndex)
    public int MainFontIndex = -1;
    public int BPMFontIndex = -1;
    public int JudgeFontIndex = -1;
    public int ComboTitleFontIndex = -1;
    public int ComboValFontIndex = -1;
    public int TimingFontIndex = -1;
    public int AttemptFontIndex = -1;
    // Per-region font sizes (ignored in combo bump, used as base size)
    public int MainFontSize = 25;
    public int BPMFontSize = 25;
    public int JudgeFontSize = 25;
    public int ComboTitleFontSize = 40;
    public int ComboValFontSize = 108;
    public int TimingFontSize = 20;
    public int AttemptFontSize = 25;

    public enum FontSlot
    {
        Main, BPM, Judgement, ComboTitle, ComboVal, Timing, Attempt
    }

    public int GetRawSlotFontIndex(FontSlot slot) => slot switch
    {
        FontSlot.Main => MainFontIndex,
        FontSlot.BPM => BPMFontIndex,
        FontSlot.Judgement => JudgeFontIndex,
        FontSlot.ComboTitle => ComboTitleFontIndex,
        FontSlot.ComboVal => ComboValFontIndex,
        FontSlot.Timing => TimingFontIndex,
        FontSlot.Attempt => AttemptFontIndex,
        _ => -1,
    };

    public void SetSlotFontIndex(FontSlot slot, int index)
    {
        switch (slot)
        {
            case FontSlot.Main: MainFontIndex = index; break;
            case FontSlot.BPM: BPMFontIndex = index; break;
            case FontSlot.Judgement: JudgeFontIndex = index; break;
            case FontSlot.ComboTitle: ComboTitleFontIndex = index; break;
            case FontSlot.ComboVal: ComboValFontIndex = index; break;
            case FontSlot.Timing: TimingFontIndex = index; break;
            case FontSlot.Attempt: AttemptFontIndex = index; break;
        }
    }

    public int GetFontIndexForSlot(FontSlot slot)
    {
        int idx = GetRawSlotFontIndex(slot);
        return idx >= 0 ? idx : FontIndex;
    }

    public int GetFontSize(FontSlot slot) => slot switch
    {
        FontSlot.Main => MainFontSize,
        FontSlot.BPM => BPMFontSize,
        FontSlot.Judgement => JudgeFontSize,
        FontSlot.ComboTitle => ComboTitleFontSize,
        FontSlot.ComboVal => ComboValFontSize,
        FontSlot.Timing => TimingFontSize,
        FontSlot.Attempt => AttemptFontSize,
        _ => 25,
    };

    public static string GetSlotLabel(FontSlot slot) => slot switch
    {
        FontSlot.Main => Tr.Get(Tr.Key.AlignMain),
        FontSlot.BPM => Tr.Get(Tr.Key.AlignBpm),
        FontSlot.Judgement => Tr.Get(Tr.Key.AlignJudge),
        FontSlot.ComboTitle => Tr.Get(Tr.Key.AlignCombo),
        FontSlot.ComboVal => Tr.Get(Tr.Key.AlignComboVal),
        FontSlot.Timing => Tr.Get(Tr.Key.AlignTiming),
        FontSlot.Attempt => Tr.Get(Tr.Key.AlignAttempt),
        _ => slot.ToString(),
    };

    public bool CustomPositionsEnabled;
    public int MainAlign = 257, BPMAlign = 260, JudgeAlign = 1026;
    public int ComboAlign = 514, ComboValAlign = 258;
    public int TimingAlign = 1026, AttemptAlign = 1025;
    public int MainStyle, BPMStyle, JudgeStyle, ComboStyle, ComboValStyle, TimingStyle, AttemptStyle;
    public float MainPX = 0.008f, MainPY = 0.985f;
    public float BPMPX = 0.992f, BPMPY = 0.985f;
    public float JudgePX = 0.5f, JudgePY = 0.005f;
    public float P1JudgePX = 0.37f, P1JudgePY = 0.032f;
    public float P2JudgePX = 0.63f, P2JudgePY = 0.032f;
    public float P3JudgePX = 0.37f, P3JudgePY = 0.005f;
    public float P4JudgePX = 0.63f, P4JudgePY = 0.005f;
    public float ComboPX = 0.5f, ComboPY = 0.947f;
    public float TimingPX = 0.5f, TimingPY = 0.12f;
    public float AttmptPX = 0.661f, AttmptPY = 0.032f;
    public float ProgBarPX = 0.5f, ProgBarPY = 0.991f;
    public float MainOffsetX, MainOffsetY, BPMOffsetX, BPMOffsetY, JudgeOffsetX, JudgeOffsetY;
    public float P1JudgeOffsetX, P1JudgeOffsetY, P2JudgeOffsetX, P2JudgeOffsetY;
    public float P3JudgeOffsetX, P3JudgeOffsetY, P4JudgeOffsetX, P4JudgeOffsetY;
    public float ComboOffsetX, ComboOffsetY, TimingOffsetX, TimingOffsetY;
    public float AttemptOffsetX, AttemptOffsetY, AttemptCoopOffsetX, AttemptCoopOffsetY, ProgBarOffsetX, ProgBarOffsetY;
    public int ConfigVersion;
    public bool ShowXPerfectInJudgement;
    public bool ShowAutoInXPerfect;
    // 统一后的唯一显示顺序（原 GeneralDisplayOrder 已并入：两个模式本就是同一个栈式布局）
    public int[] ExtendedDisplayOrder = [10, 11, 0, 1, 2, 3, 4, 5, 6, 12, 13, 14, 15, 16, 17, 18, 19, 20];
    public int[] BpmLineOrder = [0, 1, 2];
    public bool[] BpmLineVisibility = [true, true, true];
    public int[] AttemptLineOrder = [0, 1];
    public bool ComboLineReversed;

    [JsonIgnore] public ColorConfig Colors;
    [JsonIgnore] public LabelConfig Labels;

    public void OnGUI()
    {
        DrawGeneralSection();
        DrawDisplaySection();
        DrawTextSettings();
        DrawTextEffectsSection();
        DrawLabelsSection();
        // 每个 IMGUI 帧只在 Layout 阶段（或有实际修改时）刷新一次可见性，
        // 而不是每个 GUI 事件都全量重排。
        if (GUI.changed || Event.current.type == EventType.Layout)
            Overlay.Instance?.RefreshVisibility();
    }

    void DrawGeneralSection()
    {
        if (GUILayout.Button($"{( _generalFold ? "▼" : "▷")} {Tr.Get(Tr.Key.General)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _generalFold = !_generalFold;
        if (!_generalFold) return;

        Size = Slide(Tr.Get(Tr.Key.Size), Size, 0, 2, () => Overlayer.Overlay.Instance?.UpdateSize());

        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr.Get(Tr.Key.LangLabel), GUILayout.Width(100));
        var langs = new[] { "English", "한국어", "中文" };
        int langIdx = (int)CurrentLanguage;
        int newLang = GUILayout.SelectionGrid(langIdx, langs, 3);
        if (newLang != langIdx) { CurrentLanguage = (Overlayer.Localization.Language)newLang; Overlayer.Overlay.Instance?.RefreshVisibility(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (GUILayout.Button($"{( _fontFold ? "▼" : "▷")} {Tr.Get(Tr.Key.Font)}", GUI.skin.label, GUILayout.ExpandWidth(false)))
            _fontFold = !_fontFold;
        if (_fontFold && FontManager.FontNames != null)
        {
            GUILayout.Label("  -- Global --");
            for (int i = 0; i < FontManager.FontNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                bool sel = FontIndex == i;
                bool now = GUILayout.Toggle(sel, GUIContent.none, GUILayout.ExpandWidth(false));
                GUILayout.Label(FontManager.FontNames[i], GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                if (now && !sel) { FontIndex = i; FontName = FontManager.FontNames[i]; Overlayer.Overlay.Instance?.ApplyFontToAll(); }
            }

            GUILayout.Space(5);

            // Per-region font overrides
            foreach (FontSlot slot in Enum.GetValues(typeof(FontSlot)))
                DrawFontSlotSelector(slot);
        }

        CustomPositionsEnabled = Tog(Tr.Get(Tr.Key.CustomPositions), CustomPositionsEnabled);
        if (CustomPositionsEnabled)
        {
            PosGroup("Main/BPM", () =>
            {
                PosSlide2(Tr.Get(Tr.Key.PosMain), ref MainOffsetX, ref MainOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosBPM), ref BPMOffsetX, ref BPMOffsetY);
            });
            PosGroup(Tr.Get(Tr.Key.PosJudge), () =>
            {
                PosSlide2(Tr.Get(Tr.Key.PosJudge), ref JudgeOffsetX, ref JudgeOffsetY);
                if (VersionSafe.IsV141OrLater)
                {
                    PosSlide2(Tr.Get(Tr.Key.PosP1), ref P1JudgeOffsetX, ref P1JudgeOffsetY);
                    PosSlide2(Tr.Get(Tr.Key.PosP2), ref P2JudgeOffsetX, ref P2JudgeOffsetY);
                    PosSlide2(Tr.Get(Tr.Key.PosP3), ref P3JudgeOffsetX, ref P3JudgeOffsetY);
                    PosSlide2(Tr.Get(Tr.Key.PosP4), ref P4JudgeOffsetX, ref P4JudgeOffsetY);
                }
            });
            PosGroup(Tr.Get(Tr.Key.JudgementOther), () =>
            {
                PosSlide2(Tr.Get(Tr.Key.PosCombo), ref ComboOffsetX, ref ComboOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosTiming), ref TimingOffsetX, ref TimingOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosAttempt), ref AttemptOffsetX, ref AttemptOffsetY);
                if (VersionSafe.IsV141OrLater)
                    PosSlide2($"{Tr.Get(Tr.Key.PosAttempt)}\n{Tr.Get(Tr.Key.Coop)}", ref AttemptCoopOffsetX, ref AttemptCoopOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosProgBar), ref ProgBarOffsetX, ref ProgBarOffsetY);
            });
            if (GUILayout.Button(Tr.Get(Tr.Key.ResetPositions), GUILayout.ExpandWidth(false)))
                ResetCustomPos();
        }

        GUILayout.Space(5);
    }

    void DrawDisplaySection()
    {
        if (GUILayout.Button($"{( _displayFold ? "▼" : "▷")} {Tr.Get(Tr.Key.Display)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _displayFold = !_displayFold;
        if (!_displayFold) return;

        DrawDisplaySub("progress", Tr.Get(Tr.Key.ProgressAccuracy), () =>
        {
            ShowProgress = Tog(Tr.Get(Tr.Key.ShowProgress), ShowProgress);
            if (ShowProgress)
            {
                bool prevDetail = DetailedProgress;
                DetailedProgress = Tog(Tr.Get(Tr.Key.DetailedProgress), DetailedProgress);
                if (prevDetail != DetailedProgress) Overlayer.Overlay.Instance?.UpdateProgress();
                Colors.Progress.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateProgress()), Tr.Get(Tr.Key.ProgressColor),
                    () => { Colors.Progress = new([(0f, Color.white), (1f, new Color(0.8745f, 0.7098f, 1f))]); Colors.Save(); });
            }

            ShowAccuracy = Tog(Tr.Get(Tr.Key.ShowAccuracy), ShowAccuracy);
            if (ShowAccuracy)
            {
                Colors.Accuracy.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateAccuracy()), Tr.Get(Tr.Key.AccuracyColor),
                    () => { Colors.Accuracy = new([(0.98f, Color.magenta), (1f, Color.white)], new Color(1, 0.8549f, 0)); Colors.Save(); });
                AccuracyDecimal = DecSlide("accDec", Tr.Key.DecimalPrecision, AccuracyDecimal, 4,
                    () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                AccuracyTextType = EnumSel(Tr.Key.PotentialDisplay, AccuracyTextType, TextTypeNames.Name, () =>
                {
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.SetupLocationMain(); o.UpdateAccuracy(); }
                });
                HelpLabel(Tr.Get(Tr.Key.HelpPotential));
            }

            ShowXAccuracy = Tog(Tr.Get(Tr.Key.ShowXAccuracy), ShowXAccuracy);
            if (ShowXAccuracy)
            {
                Colors.XAccuracy.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateAccuracy()), Tr.Get(Tr.Key.XaccuracyColor),
                    () => { Colors.XAccuracy = new([(0.98f, Color.magenta), (1f, Color.white)], new Color(1, 0.8549f, 0)); Colors.Save(); });
                XAccuracyDecimal = DecSlide("xaccDec", Tr.Key.DecimalPrecision, XAccuracyDecimal, 4,
                    () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                XAccuracyTextType = EnumSel(Tr.Key.PotentialDisplay, XAccuracyTextType, TextTypeNames.Name, () =>
                {
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.SetupLocationMain(); o.UpdateAccuracy(); }
                });
                HelpLabel(Tr.Get(Tr.Key.HelpPotential));
            }

            // XScore 文本：仅 r149+（游戏原生 XPerfect 计分），r148 无此数据
            bool xscoreOk = HitMarginCompat.HasNativeXPerfect;
            bool prevXs = ShowXScore;
            if (!xscoreOk) GUI.enabled = false;
            ShowXScore = Tog(Tr.Get(Tr.Key.ShowXScore), ShowXScore);
            if (!xscoreOk) { GUI.enabled = true; if (ShowXScore != prevXs) ShowXScore = prevXs; }
            if (xscoreOk && ShowXScore)
            {
                Colors.XScore.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateAccuracy()), Tr.Get(Tr.Key.XScoreColor),
                    () => { Colors.XScore = new([(0.98f, Color.white), (1f, Color.white)], new Color(1, 0.8549f, 0)); Colors.Save(); });
                HelpLabel(Tr.Get(Tr.Key.HelpXScore));
                // 两个选择器各管一件事：分数格式管数字怎么写，潜力值显示管当前/潜力怎么排
                XScoreTextType = EnumSel(Tr.Key.ScoreFormat, XScoreTextType, TextTypeNames.Name, () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                XScorePotentialType = EnumSel(Tr.Key.PotentialDisplay, XScorePotentialType, TextTypeNames.Name, () =>
                {
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.SetupLocationMain(); o.UpdateAccuracy(); }
                });
                HelpLabel(Tr.Get(Tr.Key.HelpPotential));
                // 切换开关会改变主容器/栈的可见性，必须走完整刷新（RefreshVisibility 内含 SetupLocationMain）
                if (ShowXScore != prevXs) Overlayer.Overlay.Instance?.RefreshVisibility();
            }
        });

        DrawDisplaySub("time", Tr.Get(Tr.Key.TimeSection), () =>
        {
            ShowMusicTime = Tog(Tr.Get(Tr.Key.ShowMusicTime), ShowMusicTime);
            if (ShowMusicTime) Colors.MusicTime.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateTime()), Tr.Get(Tr.Key.MusicTimeColor),
                () => { Colors.MusicTime = new([(1f, Color.white)]); Colors.Save(); });

            ShowMapTime = Tog(Tr.Get(Tr.Key.ShowMapTime), ShowMapTime);
            if (ShowMapTime) Colors.MapTime.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateTime()), Tr.Get(Tr.Key.MapTimeColor),
                () => { Colors.MapTime = new([(1f, Color.white)]); Colors.Save(); });

            ShowMapTimeIfNotMusic = Tog(Tr.Get(Tr.Key.ShowMapIfNo), ShowMapTimeIfNotMusic);
            bool prevDec = TimeDecimals;
            TimeDecimals = Tog(Tr.Get(Tr.Key.TimeDecimals), TimeDecimals);
            if (prevDec != TimeDecimals)
            {
                // 时间格式切换后必须作废已缓存的格式化总时长，否则缓存还是旧格式
                var o = Overlayer.Overlay.Instance;
                if (o != null) { o.MusicTimeCache = null; o.MapTimeCache = null; o.UpdateTime(); }
            }
        });

        DrawDisplaySub("progbar", Tr.Get(Tr.Key.ProgressBarBest), () =>
        {
            ShowCheckpoint = Tog(Tr.Get(Tr.Key.ShowCheckpoint), ShowCheckpoint);
            ShowBest = Tog(Tr.Get(Tr.Key.ShowBest), ShowBest);
            if (ShowBest)
            {
                Colors.Best.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateProgress()), Tr.Get(Tr.Key.BestColor),
                    () => { Colors.Best = new([(0f, Color.white), (1f, new Color(0.8745f, 0.7098f, 1f))]); Colors.Save(); });
                BestDecimal = DecSlide("bestDec", Tr.Key.DecimalPrecision, BestDecimal, 4,
                    () => Overlayer.Overlay.Instance?.OverlayTextManager?.UpdateBest(Overlayer.Overlay.Instance));
            }

            ShowProgressBar = Tog(Tr.Get(Tr.Key.ShowProgressBar), ShowProgressBar);
            if (ShowProgressBar)
            {
                Colors.ProgressBar.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateProgressBar()), Tr.Get(Tr.Key.ProgressBarColor),
                    () => { Colors.ProgressBar = new([(1f, new Color(0.9216f, 0.8039f, 0.9765f))]); Colors.Save(); });
                Colors.ProgressBarBackground.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateProgressBar()), Tr.Get(Tr.Key.ProgressBarBgColor),
                    () => { Colors.ProgressBarBackground = new([(1f, Color.white)]); Colors.Save(); });
                Colors.ProgressBarBorder.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateProgressBar()), Tr.Get(Tr.Key.ProgressBarBorderColor),
                    () => { Colors.ProgressBarBorder = new([(1f, Color.black)]); Colors.Save(); });
            }
        });

        DrawDisplaySub("combo", Tr.Get(Tr.Key.ComboSection), () =>
        {
            ShowCombo = Tog(Tr.Get(Tr.Key.ShowCombo), ShowCombo);
            if (ShowCombo)
            {
                EnableAutoCombo = Tog(Tr.Get(Tr.Key.EnableAutoCombo), EnableAutoCombo);
                ComboColorMax = (int)Slide(Tr.Get(Tr.Key.ComboColorMax), ComboColorMax, 1, 5000, () => { });
                Colors.Combo.SettingGUI(ColorChanged(null), Tr.Get(Tr.Key.ComboColor),
                    () => { Colors.Combo = new([(0f, new Color(0.8745f, 0.7098f, 1f)), (1f, new Color(0.7176f, 0.3490f, 1f))]); Colors.Save(); });
                bool prevReversed = ComboLineReversed;
                ComboLineReversed = Tog(Tr.Get(Tr.Key.ComboLineReversed), ComboLineReversed);
                ComboTitleAltOnNonPerfect = Tog(Tr.Get(Tr.Key.ComboTitleAltOnNonPerfect), ComboTitleAltOnNonPerfect);
                if (prevReversed != ComboLineReversed) Overlayer.Overlay.Instance?.RefreshVisibility();
            }
        });

        DrawDisplaySub("bpm", Tr.Get(Tr.Key.BpmSection), () =>
        {
            ShowBPM = Tog(Tr.Get(Tr.Key.ShowBpm), ShowBPM);
            if (ShowBPM)
            {
                BpmColorMax = Slide(Tr.Get(Tr.Key.BpmColorMax), BpmColorMax, 100, 20000, () => { });
                Colors.Bpm.SettingGUI(ColorChanged(() => Overlayer.Overlay.Instance?.UpdateBPM()), Tr.Get(Tr.Key.BpmColor),
                    () => { Colors.Bpm = new([(0f, Color.white), (1f, Color.magenta)]); Colors.Save(); });
                DrawBpmLineOrder();
                bool prevShowTimingWindow = ShowTimingWindow;
                ShowTimingWindow = Tog(Tr.Get(Tr.Key.ShowTimingWindow), ShowTimingWindow);
                if (prevShowTimingWindow != ShowTimingWindow)
                {
                    // 切换判定时间窗时使 BPM 缓存失效并立即重绘，否则要等 BPM 变化才生效
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.DirtyBpmCache(); o.UpdateBPM(); }
                }
            }
        });

        DrawDisplaySub("judge", Tr.Get(Tr.Key.JudgementOther), () =>
        {
            ShowJudgement = Tog(Tr.Get(Tr.Key.ShowJudgement), ShowJudgement);
            if (ShowJudgement) JudgementLocationUp = Tog(Tr.Get(Tr.Key.JudgementUp), JudgementLocationUp);
            // r150 基础游戏原生支持 XPerfect，r148 依赖外部 XPerfect mod——两种来源都允许显示开关
            if (HitMarginCompat.XPerfectDisplayAvailable && ShowJudgement) ShowXPerfectInJudgement = Tog(Tr.Get(Tr.Key.ShowXPerfectInJudgement), ShowXPerfectInJudgement);
            if (HitMarginCompat.XPerfectDisplayAvailable && ShowJudgement && ShowXPerfectInJudgement) ShowAutoInXPerfect = Tog(Tr.Get(Tr.Key.ShowAutoInXPerfect), ShowAutoInXPerfect);
            ShowTimingScale = Tog(Tr.Get(Tr.Key.ShowTimingScale), ShowTimingScale);
            ShowAttempt = Tog(Tr.Get(Tr.Key.ShowAttempt), ShowAttempt);
            ShowFullAttempt = Tog(Tr.Get(Tr.Key.ShowFullAttempt), ShowFullAttempt);
            DrawAttemptLineOrder();
        });

        // ==== 扩展文本（原 ExtendedOverlay 分区并入：与 progress/time 等同为独立显示类型） ====
        DrawDisplaySub("jDisplay", Tr.Get(Tr.Key.DisplayOptions), () =>
        {
            ShowFPS = TogR(Tr.Key.ShowFps, ShowFPS);
            if (ShowFPS)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(36);
                FPSRefreshRate = Slide(Tr.Get(Tr.Key.FPSRefreshRate), FPSRefreshRate, 0.05f, 1f, () => { });
                GUILayout.EndHorizontal();
                DrawJColorFoldout("jFps", Tr.Get(Tr.Key.FpsColor), Colors.JFps,
                    () => { Colors.JFps = new(Color.white); Colors.Save(); });
            }
            ShowAuthor = TogR(Tr.Key.ShowAuthor, ShowAuthor);
            if (ShowAuthor) DrawJColorFoldout("jAuthor", Tr.Get(Tr.Key.AuthorColor), Colors.JAuthor,
                () => { Colors.JAuthor = new(Color.white); Colors.Save(); });
            ShowState = TogR(Tr.Key.ShowState, ShowState);
            if (ShowState)
            {
                DrawJColorFoldout("jStWaiting", Tr.Get(Tr.Key.StateDefaultColor), Colors.JStateWaiting,
                    () => { Colors.JStateWaiting = new(Color.white); Colors.Save(); });
                DrawJColorFoldout("jStAutoTile", Tr.Get(Tr.Key.StateAutoTileColor), Colors.JStateAutoTile,
                    () => { Colors.JStateAutoTile = new(new Color(1, 0.5f, 0)); Colors.Save(); });
                DrawJColorFoldout("jStAuto", Tr.Get(Tr.Key.StateAutoColor), Colors.JStateAuto,
                    () => { Colors.JStateAuto = new(new Color(0.1058824f, 1f, 0)); Colors.Save(); });
                DrawJColorFoldout("jStPerfect", Tr.Get(Tr.Key.StatePerfectColor), Colors.JStatePerfectPlay,
                    () => { Colors.JStatePerfectPlay = new(new Color(1, 0.8549f, 0)); Colors.Save(); });
                DrawJColorFoldout("jStComplete", Tr.Get(Tr.Key.StateCompleteColor), Colors.JStateComplete,
                    () => { Colors.JStateComplete = new(Color.white); Colors.Save(); });
                DrawJColorFoldout("jStClear", Tr.Get(Tr.Key.StateClearColor), Colors.JStateClear,
                    () => { Colors.JStateClear = new(Color.white); Colors.Save(); });
                DrawJColorFoldout("jStNoMiss", Tr.Get(Tr.Key.StateNoMissColor), Colors.JStateNoMiss,
                    () => { Colors.JStateNoMiss = new(Color.white); Colors.Save(); });
                DrawJColorFoldout("jStPerf", Tr.Get(Tr.Key.StatePerfectionistColor), Colors.JStatePerfectionist,
                    () => { Colors.JStatePerfectionist = new(Color.white); Colors.Save(); });
            }
            ShowDeath = TogR(Tr.Key.ShowDeath, ShowDeath);
            if (ShowDeath) Colors.JDeath.SettingGUI(ColorChanged(() => Overlay.Instance?.UpdateProgress()), Tr.Get(Tr.Key.DeathColor),
                () => { Colors.JDeath = new([(0f, Color.red), (1f, Color.green)]); Colors.Save(); });
            ShowStart = TogR(Tr.Key.ShowStart, ShowStart);
            if (ShowStart) DrawJColorFoldout("jStart", Tr.Get(Tr.Key.StartColor), Colors.JStart,
                () => { Colors.JStart = new(Color.white); Colors.Save(); });
            ShowTiming = TogR(Tr.Key.ShowTiming, ShowTiming);
            if (ShowTiming)
            {
                Colors.JTiming.SettingGUI(ColorChanged(() => Overlay.Instance?.UpdateTime()), Tr.Get(Tr.Key.TimingColor),
                    () => { Colors.JTiming = new([(0f, Color.red), (1f, Color.green)]); Colors.Save(); });
                TimingDecimal = DecSlide("timingDec", Tr.Key.DecimalPrecision, TimingDecimal, 5,
                    () => Overlay.Instance?.ExtendedOverlay?.RefreshTiming());
                TimingTextType = EnumSel(Tr.Key.TimingDisplay, TimingTextType, TextTypeNames.Name, () =>
                {
                    var o = Overlay.Instance;
                    if (o != null) { o.SetupLocationMain(); o.ExtendedOverlay?.RefreshTiming(); }
                });
                HelpLabel(Tr.Get(Tr.Key.HelpTimingMode));
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tr.Get(Tr.Key.DecimalPrecision), GUILayout.Width(120));
            ExtendedDecimalPrecision = (int)GUILayout.HorizontalSlider(ExtendedDecimalPrecision, 0, 5);
            if (!_slideFields.TryGetValue("DecimalPrecision", out var dpText))
                _slideFields["DecimalPrecision"] = dpText = ExtendedDecimalPrecision.ToString();
            string newDpText = GUILayout.TextField(dpText, GUILayout.Width(55));
            if (newDpText != dpText)
            {
                _slideFields["DecimalPrecision"] = newDpText;
                if (int.TryParse(newDpText, out int dpParsed))
                    ExtendedDecimalPrecision = Mathf.Clamp(dpParsed, 0, 5);
            }
            else if (newDpText == dpText && ExtendedDecimalPrecision.ToString() != dpText)
                _slideFields["DecimalPrecision"] = ExtendedDecimalPrecision.ToString();
            GUILayout.EndHorizontal();
            var o = Overlay.Instance;
            // 全局小数位滑条现只管扩展文本（FPS/开始进度等）；主文本各有独立小数位
            if (o?.ExtendedOverlay != null) o.ExtendedOverlay.DecimalPrecision = ExtendedDecimalPrecision;
        });

        DrawDisplaySub("jBehavior", Tr.Get(Tr.Key.BehaviorOptions), () =>
        {
            bool prevHide = HideDebugText;
            HideDebugText = Tog(Tr.Get(Tr.Key.HideDebugText), HideDebugText);
            if (prevHide != HideDebugText) PatchManager.RefreshPatches();
            RemoveNotRequireInAuto = Tog(Tr.Get(Tr.Key.RemoveAutoReq), RemoveNotRequireInAuto);
            CheckPseudo = Tog(Tr.Get(Tr.Key.CheckPseudo), CheckPseudo);
            bool prevEL = AllowELCombo;
            AllowELCombo = Tog(Tr.Get(Tr.Key.AllowELCombo), AllowELCombo);
            if (prevEL != AllowELCombo) { PatchManager.RefreshPatches(); if (!AllowELCombo) AllowOrangeCombo = false; }
            if (AllowELCombo)
            {
                AllowOrangeCombo = Tog(Tr.Get(Tr.Key.AllowOrangeCombo), AllowOrangeCombo, 20);
                Colors.JCombo.SettingGUI(ColorChanged(null), Tr.Get(Tr.Key.JComboColor),
                    () => { Colors.JCombo = new([(0f, Color.red), (0.2f, new Color(0.9882f, 1, 0.302f)), (1f, new Color(0.3725f, 1, 0.3119f))]); Colors.Save(); });
            }
        });

        GUILayout.Space(5);
        // 显示顺序：全部 13 个可栈排元素共栈共顺序（总开关移除后两套顺序合一）
        DrawOrderSection("stackOrder", ExtendedDisplayOrder, true);

        GUILayout.Space(3);
        PatchBetaWatermark = Tog(Tr.Get(Tr.Key.PatchBetaWatermark), PatchBetaWatermark);
        PatchLevelName = Tog(Tr.Get(Tr.Key.PatchLevelName), PatchLevelName);
        bool prevRepos = RepositionAutoText;
        RepositionAutoText = Tog(Tr.Get(Tr.Key.RepositionAutoText), RepositionAutoText);
        if (prevRepos != RepositionAutoText) PatchManager.RefreshPatches();
    }

    /// <summary>带重排的开关：变化时刷新补丁与栈布局。
    /// 这些开关门控着 Harmony 补丁（Timing 取数、State 联动、调试文本等），
    /// 注册时的开关状态决定补丁是否挂载，切换后必须 RefreshPatches 才会生效。</summary>
    bool TogR(Tr.Key key, bool value)
    {
        bool old = value;
        bool nv = Tog(Tr.Get(key), value);
        if (nv != old)
        {
            PatchManager.RefreshPatches();
            var o = Overlay.Instance;
            if (o != null) { o.SetupLocationMain(); o.RefreshVisibility(); }
        }
        return nv;
    }

    void DrawDisplaySub(string key, string label, Action content)
    {
        bool expanded = _expandedDisplaySub == key;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button($"{(expanded ? "▼" : "▷")} {label}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _expandedDisplaySub = expanded ? null : key;
        GUILayout.EndHorizontal();
        if (expanded)
        {
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Space(36);
            GUILayout.BeginVertical();
            content();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

    void DrawOrderSection(string key, int[] order, bool isExtendedOverlay)
    {
        bool expanded = _expandedOrder == key;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button($"{(expanded ? "▼" : "▷")} {Tr.Get(Tr.Key.DisplayOrder)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _expandedOrder = expanded ? null : key;
        GUILayout.EndHorizontal();
        if (!expanded) return;

        GUILayout.BeginHorizontal();
        GUILayout.Space(36);
        GUILayout.BeginVertical();

        var list = new List<int>(order);
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            var elem = (DisplayElement)list[i];

            GUILayout.BeginHorizontal();
            // ▲
            if (i > 0 && GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(20)))
            {
                (list[i], list[i - 1]) = (list[i - 1], list[i]);
                changed = true;
            }
            else GUILayout.Space(24);

            // ▼
            if (i < list.Count - 1 && GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(20)))
            {
                (list[i], list[i + 1]) = (list[i + 1], list[i]);
                changed = true;
            }
            else GUILayout.Space(24);

            GUILayout.Label($"  {i + 1}. {GetElementName(elem)}");
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button(Tr.Get(Tr.Key.ResetOrder), GUILayout.ExpandWidth(false)))
        {
            list.Clear();
            list.AddRange(GetDefaultExtendedOverlayOrder());
            changed = true;
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        if (changed)
        {
            ExtendedDisplayOrder = list.ToArray();
            Save();
            var o = Overlay.Instance;
            if (o != null) { o.SetupLocationMain(); o.RefreshVisibility(); }
        }
    }

    static int[] GetDefaultExtendedOverlayOrder() => [10, 11, 0, 1, 2, 3, 4, 5, 6, 12, 13, 14, 15, 16, 17, 18, 19, 20];

    /// <summary>把默认顺序里存在、但用户数组里缺失的元素追加到末尾。
    /// 旧版配置保存的顺序数组不含后加的元素（如 XScore / 潜力值），
    /// 不补齐 SetupLocation 的 foreach 就永远遍历不到它们——表现为开关打开却始终不显示。</summary>
    static int[] AppendMissingExtendedOverlayElements(int[] order)
    {
        var defaults = GetDefaultExtendedOverlayOrder();
        var have = new HashSet<int>(order);
        var missing = defaults.Where(x => !have.Contains(x)).ToArray();
        if (missing.Length == 0) return order;
        var result = new int[order.Length + missing.Length];
        order.CopyTo(result, 0);
        missing.CopyTo(result, order.Length);
        return result;
    }

    static string GetElementName(DisplayElement elem) => elem switch
    {
        DisplayElement.Progress => Tr.Get(Tr.Key.ElemProgress),
        DisplayElement.Accuracy => Tr.Get(Tr.Key.ElemAccuracy),
        DisplayElement.XAccuracy => Tr.Get(Tr.Key.ElemXAccuracy),
        DisplayElement.MusicTime => Tr.Get(Tr.Key.ElemMusicTime),
        DisplayElement.MapTime => Tr.Get(Tr.Key.ElemMapTime),
        DisplayElement.Checkpoint => Tr.Get(Tr.Key.ElemCheckpoint),
        DisplayElement.Best => Tr.Get(Tr.Key.ElemBest),
        DisplayElement.BPM => Tr.Get(Tr.Key.ElemBPM),
        DisplayElement.Attempt => Tr.Get(Tr.Key.ElemAttempt),
        DisplayElement.TimingScale => Tr.Get(Tr.Key.ElemTimingScale),
        DisplayElement.FPS => Tr.Get(Tr.Key.ElemFPS),
        DisplayElement.Author => Tr.Get(Tr.Key.ElemAuthor),
        DisplayElement.State => Tr.Get(Tr.Key.ElemState),
        DisplayElement.Death => Tr.Get(Tr.Key.ElemDeath),
        DisplayElement.Start => Tr.Get(Tr.Key.ElemStart),
        DisplayElement.Timing => Tr.Get(Tr.Key.ElemTiming),
        DisplayElement.AvgTiming => Tr.Get(Tr.Key.ElemAvgTiming),
        DisplayElement.XScore => Tr.Get(Tr.Key.ElemXScore),
        DisplayElement.PotentialAccuracy => Tr.Get(Tr.Key.ElemPAcc),
        DisplayElement.PotentialXAccuracy => Tr.Get(Tr.Key.ElemPXAcc),
        DisplayElement.PotentialXScore => Tr.Get(Tr.Key.ElemPXScore),
        _ => elem.ToString()
    };

    static bool IsValidExtendedOverlayElement(int id) => id switch
    {
        >= 10 and <= 20 => true,  // FPS..PotentialXScore
        >= 0 and <= 6 => true,    // Progress..Best
        _ => false,
    };

    void DrawBpmLineOrder()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label("─── " + Tr.Get(Tr.Key.BpmSection) + " " + Tr.Get(Tr.Key.DisplayOrder) + " ───");
        GUILayout.EndHorizontal();
        DrawReorderList(BpmLineOrder, id => id switch { 0 => Tr.Get(Tr.Key.BpmLineTile), 1 => Tr.Get(Tr.Key.BpmLineCur), _ => Tr.Get(Tr.Key.BpmLineKps) }, [0, 1, 2],
            arr => { BpmLineOrder = arr; Save(); var o = Overlay.Instance; o?.DirtyBpmCache(); o?.UpdateBPM(); },
            id => id < BpmLineVisibility.Length && BpmLineVisibility[id],
            (id, v) => { if (id < BpmLineVisibility.Length) { BpmLineVisibility[id] = v; Save(); var o = Overlay.Instance; o?.DirtyBpmCache(); o?.UpdateBPM(); } });
    }

    void DrawAttemptLineOrder()
    {
        DrawReorderList(AttemptLineOrder, id => id == 0 ? Tr.Get(Tr.Key.AttemptLineAttempt) : Tr.Get(Tr.Key.AttemptLineFull), [0, 1],
            arr => { AttemptLineOrder = arr; Save(); Overlay.Instance?.UpdateAttempts(); });
    }

    void DrawReorderList(int[] order, Func<int, string> getName, int[] defaultOrder, Action<int[]> onSave,
        Func<int, bool> isVisible = null, Action<int, bool> setVisible = null)
    {
        var list = new List<int>(order);
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            int id = list[i];

            GUILayout.BeginHorizontal();
            GUILayout.Space(36);

            // Visibility toggle
            bool hasVis = isVisible != null && setVisible != null;
            if (hasVis)
            {
                bool vis = isVisible(id);
                bool newVis = GUILayout.Toggle(vis, GUIContent.none, GUILayout.Width(16), GUILayout.Height(16));
                if (newVis != vis) { setVisible(id, newVis); }
            }
            else GUILayout.Space(16);

            // ▲
            if (i > 0 && GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(20)))
            {
                (list[i], list[i - 1]) = (list[i - 1], list[i]);
                changed = true;
            }
            else GUILayout.Space(24);

            // ▼
            if (i < list.Count - 1 && GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(20)))
            {
                (list[i], list[i + 1]) = (list[i + 1], list[i]);
                changed = true;
            }
            else GUILayout.Space(24);

            GUILayout.Label($"  {i + 1}. {getName(id)}");
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button(Tr.Get(Tr.Key.ResetOrder), GUILayout.ExpandWidth(false)))
        {
            list.Clear();
            list.AddRange(defaultOrder);
            changed = true;
        }

        if (changed)
        {
            onSave(list.ToArray());
        }
    }

    void PosGroup(string label, Action content)
    {
        bool expanded = _expandedPos == label;
        if (GUILayout.Button($"{(expanded ? "▼" : "▷")} {label}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _expandedPos = expanded ? null : label;
        if (!expanded) return;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.BeginVertical();
        content();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    void DrawTextSettings()
    {
        if (GUILayout.Button($"{( _alignFold ? "▼" : "▷")} {Tr.Get(Tr.Key.TextSettings)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _alignFold = !_alignFold;
        if (!_alignFold) return;
        DrawAlignment(Tr.Get(Tr.Key.AlignMain), ref MainAlign, ref MainStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignBpm), ref BPMAlign, ref BPMStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignJudge), ref JudgeAlign, ref JudgeStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignCombo), ref ComboAlign, ref ComboStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignComboVal), ref ComboValAlign, ref ComboValStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignTiming), ref TimingAlign, ref TimingStyle);
        DrawAlignment(Tr.Get(Tr.Key.AlignAttempt), ref AttemptAlign, ref AttemptStyle);
        GUILayout.Space(3);
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.ApplyAlignment))) { Overlayer.Overlay.Instance?.ApplyAlignment(); Overlayer.Overlay.Instance?.ApplyFontStyle(); }
        if (GUILayout.Button(Tr.Get(Tr.Key.AlignReset), GUILayout.Width(50))) { ResetAlignment(); ResetStyle(); Overlayer.Overlay.Instance?.ApplyAlignment(); Overlayer.Overlay.Instance?.ApplyFontStyle(); }
        GUILayout.EndHorizontal();
    }

    void DrawTextEffectsSection()
    {
        if (GUILayout.Button($"{( _textEffectsFold ? "▼" : "▷")} {Tr.Get(Tr.Key.TextEffects)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _textEffectsFold = !_textEffectsFold;
        if (!_textEffectsFold) return;

        var fx = TextEffects;
        bool changed = false;

        // Shadow
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        bool shadowOn = GUILayout.Toggle(fx.ShadowEnabled, Tr.Get(Tr.Key.TextEffectShadow), GUILayout.ExpandWidth(true));
        if (shadowOn != fx.ShadowEnabled) { fx.ShadowEnabled = shadowOn; changed = true; }
        GUILayout.EndHorizontal();

        if (fx.ShadowEnabled)
        {
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectShadowOffsetX), ref fx.ShadowOffsetX, -5f, 5f);
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectShadowOffsetY), ref fx.ShadowOffsetY, -5f, 5f);
            changed |= DrawColorCacheField(Tr.Get(Tr.Key.TextEffectShadowColor), fx.ShadowColor);
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectShadowSoftness), ref fx.ShadowSoftness, 0f, 1f);
        }

        // Outline
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        bool outlineOn = GUILayout.Toggle(fx.OutlineEnabled, Tr.Get(Tr.Key.TextEffectOutline), GUILayout.ExpandWidth(true));
        if (outlineOn != fx.OutlineEnabled) { fx.OutlineEnabled = outlineOn; changed = true; }
        GUILayout.EndHorizontal();

        if (fx.OutlineEnabled)
        {
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectOutlineWidth), ref fx.OutlineWidth, 0f, 0.5f);
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectOutlineSoftness), ref fx.OutlineSoftness, 0f, 1f);
            changed |= DrawColorCacheField(Tr.Get(Tr.Key.TextEffectOutlineColor), fx.OutlineColor);
        }

        if (changed)
        {
            Save();
            ShadowManager.ClearCache();
            Overlay.Instance?.ApplyFontToAll();
        }
    }

    bool DrawFloatField(string label, ref float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label(label, GUILayout.Width(140));
        var v = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(value.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();
        if (Math.Abs(v - value) > 1e-4)
        {
            value = v;
            return true;
        }
        return false;
    }

    // Render a ColorCache's RGBA sliders inline with a label. SettingGUI manages its own rows
    // (Hex → R → G → B → A → preview) so we just emit the label above it.
    static bool DrawColorCacheField(string label, ColorCache cc)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label(label);
        GUILayout.EndHorizontal();
        bool changed = cc.SettingGUI(label, Color.black);
        return changed;
    }

    void ResetCustomPos()
    {
        MainOffsetX = MainOffsetY = BPMOffsetX = BPMOffsetY = JudgeOffsetX = JudgeOffsetY = 0;
        P1JudgeOffsetX = P1JudgeOffsetY = P2JudgeOffsetX = P2JudgeOffsetY = 0;
        P3JudgeOffsetX = P3JudgeOffsetY = P4JudgeOffsetX = P4JudgeOffsetY = 0;
        ComboOffsetX = ComboOffsetY = TimingOffsetX = TimingOffsetY = 0;
        AttemptOffsetX = AttemptOffsetY = AttemptCoopOffsetX = AttemptCoopOffsetY = ProgBarOffsetX = ProgBarOffsetY = 0;
        _slideFields.Clear();
        Overlayer.Overlay.Instance?.ApplyPositionOffsets();
    }

    void ResetAlignment()
    {
        MainAlign = 257; BPMAlign = 260; JudgeAlign = 1026;
        ComboAlign = 514; ComboValAlign = 258;
        TimingAlign = 1026; AttemptAlign = 1025;
    }

    void ResetStyle()
    {
        MainStyle = BPMStyle = JudgeStyle = ComboStyle = ComboValStyle = TimingStyle = AttemptStyle = 0;
    }

    static bool Tog(string label, bool v, int indent = 0)
    {
        GUILayout.BeginHorizontal();
        if (indent > 0) GUILayout.Space(indent);
        v = GUILayout.Toggle(v, label, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        return v;
    }

    /// <summary>枚举选择器：点击循环切换到下一个值。按钮显示本地化含义
    /// （见 TextTypeNames），不再直接显示英文原始枚举名。</summary>
    static T EnumSel<T>(Tr.Key key, T value, Func<T, string> nameOf, Action onChange = null) where T : struct, Enum
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr.Get(key), GUILayout.Width(140));
        if (GUILayout.Button(nameOf(value), GUILayout.Width(180)))
        {
            var vals = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(vals, value);
            value = vals[(i + 1) % vals.Length];
            onChange?.Invoke();
        }
        GUILayout.EndHorizontal();
        return value;
    }

    static GUIStyle _helpStyle;
    /// <summary>设置项下的小字说明行（淡色、自动换行）——把各文本类型的语义直接声明在界面里。</summary>
    static void HelpLabel(string text)
    {
        if (_helpStyle == null)
        {
            _helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _helpStyle.normal.textColor *= new Color(1f, 1f, 1f, 0.7f);
        }
        GUILayout.Label(text, _helpStyle);
    }

    /// <summary>0..max 的整数小数位滑条（带可编辑文本框）。</summary>
    int DecSlide(string fieldKey, Tr.Key labelKey, int value, int max, Action onChange)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr.Get(labelKey), GUILayout.Width(140));
        int nv = (int)GUILayout.HorizontalSlider(value, 0, max);
        if (!_slideFields.TryGetValue(fieldKey, out var text))
            _slideFields[fieldKey] = text = value.ToString();
        string newText = GUILayout.TextField(text, GUILayout.Width(55));
        if (newText != text)
        {
            _slideFields[fieldKey] = newText;
            if (int.TryParse(newText, out int parsed))
                nv = Mathf.Clamp(parsed, 0, max);
        }
        else if (value.ToString() != text)
            _slideFields[fieldKey] = value.ToString();
        GUILayout.EndHorizontal();
        if (nv != value) onChange?.Invoke();
        return nv;
    }

    static float Slide(string label, float v, float min, float max, Action onChange)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(120));
        float nv = GUILayout.HorizontalSlider(v, min, max);
        if (!_slideFields.TryGetValue(label, out var text))
            _slideFields[label] = text = v.ToString("F2");
        string newText = GUILayout.TextField(text, GUILayout.Width(55));
        if (newText != text)
        {
            // 保留用户输入的原始文本（含清空、负号等中间态），能解析才应用——
            // 与 PosSlide2 相同的模式，避免输入被立即回弹。
            _slideFields[label] = newText;
            if (float.TryParse(newText, out float parsed))
            {
                float clamped = Mathf.Clamp(parsed, min, max);
                if (Math.Abs(clamped - v) > 0.001f)
                {
                    nv = clamped;
                    _slideFields[label] = Math.Abs(nv - parsed) > 0.0001f ? nv.ToString("F2") : newText;
                }
            }
        }
        else if (Math.Abs(nv - v) > 0.001f)
            _slideFields[label] = nv.ToString("F2");
        GUILayout.EndHorizontal();
        if (Math.Abs(nv - v) > 0.001f) { onChange?.Invoke(); return nv; }
        return v;
    }

    void PosSlide2(string label, ref float vx, ref float vy)
    {
        const float min = -2000, max = 2000;
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(55));
        GUILayout.Label("X", GUILayout.Width(14));
        float nx = GUILayout.HorizontalSlider(vx, min, max, GUILayout.ExpandWidth(true));
        if (!_slideFields.TryGetValue(label + "X", out var tx))
            _slideFields[label + "X"] = tx = $"{(int)vx}";
        string ntx = GUILayout.TextField(tx, GUILayout.Width(42));
        if (ntx != tx) { _slideFields[label + "X"] = ntx; if (float.TryParse(ntx, out float p)) nx = Mathf.Clamp(p, min, max); }
        else if (ntx == tx && Math.Abs(nx - vx) > 0.001f) _slideFields[label + "X"] = $"{(int)nx}";
        GUILayout.Space(4);
        GUILayout.Label("Y", GUILayout.Width(14));
        float ny = GUILayout.HorizontalSlider(vy, min, max, GUILayout.ExpandWidth(true));
        if (!_slideFields.TryGetValue(label + "Y", out var ty))
            _slideFields[label + "Y"] = ty = $"{(int)vy}";
        string nty = GUILayout.TextField(ty, GUILayout.Width(42));
        if (nty != ty) { _slideFields[label + "Y"] = nty; if (float.TryParse(nty, out float p)) ny = Mathf.Clamp(p, min, max); }
        else if (nty == ty && Math.Abs(ny - vy) > 0.001f) _slideFields[label + "Y"] = $"{(int)ny}";
        GUILayout.EndHorizontal();
        if (Math.Abs(nx - vx) > 0.001f || Math.Abs(ny - vy) > 0.001f) { vx = nx; vy = ny; Overlayer.Overlay.Instance?.ApplyPositionOffsets(); }
    }

    static readonly Dictionary<string, string> _slideFields = new();
    static readonly Dictionary<string, bool> _jColorFold = new();
    private static readonly GUIStyle _foldBtn = new()
    {
        fixedWidth = 18f, normal = new GUIStyleState { textColor = Color.white }, fontSize = 14, margin = new RectOffset(4, 2, 4, 4)
    };
    private static bool _generalFold, _displayFold, _fontFold, _alignFold, _textEffectsFold;
    private static string _expandedAlign, _expandedDisplaySub, _expandedPos;
    private static string _expandedOrder, _expandedFontSlot;
    private static bool _labelsFold;

    void DrawFontSlotSelector(FontSlot slot)
    {
        string label = GetSlotLabel(slot);
        string key = $"FontSlot_{slot}";
        bool expanded = _expandedFontSlot == key;

        int rawIdx = GetRawSlotFontIndex(slot);
        bool useGlobal = rawIdx < 0;
        int resolvedIdx = GetFontIndexForSlot(slot);
        string fontName = useGlobal
            ? $"{FontManager.FontNames[resolvedIdx]} ({Tr.Get(Tr.Key.AlignMain)})"
            : FontManager.FontNames[resolvedIdx];

        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(label, GUILayout.Width(80));
        if (GUILayout.Button(fontName, GUI.skin.label, GUILayout.ExpandWidth(true)))
            _expandedFontSlot = expanded ? null : key;
        GUILayout.EndHorizontal();

        if (!expanded) return;

        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.BeginVertical();

        // "Use Global" option
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            bool now = GUILayout.Toggle(useGlobal, GUIContent.none, GUILayout.ExpandWidth(false));
            GUILayout.Label(Tr.Get(Tr.Key.Font) + " (" + Tr.Get(Tr.Key.AlignMain) + ")");
            GUILayout.EndHorizontal();
            if (now && !useGlobal) { SetSlotFontIndex(slot, -1); Overlayer.Overlay.Instance?.ApplyFontToAll(); }
        }

        for (int i = 0; i < FontManager.FontNames.Length; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            bool sel = resolvedIdx == i && !useGlobal;
            bool now = GUILayout.Toggle(sel, GUIContent.none, GUILayout.ExpandWidth(false));
            GUILayout.Label(FontManager.FontNames[i], GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (now && !sel) { SetSlotFontIndex(slot, i); Overlayer.Overlay.Instance?.ApplyFontToAll(); _expandedFontSlot = null; }
        }

        // Font size slider + text input
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label("Font Size", GUILayout.Width(60));
        float curF = GetFontSize(slot);
        float nv = GUILayout.HorizontalSlider(curF, 8, 200, GUILayout.ExpandWidth(true));
        string sizeKey = $"FontSize_{slot}";
        if (!_slideFields.TryGetValue(sizeKey, out var sizeText))
            _slideFields[sizeKey] = sizeText = curF.ToString("F0");
        string newSizeText = GUILayout.TextField(sizeText, GUILayout.Width(42));
        if (newSizeText != sizeText)
        {
            if (float.TryParse(newSizeText, out float parsed))
            {
                nv = Mathf.Clamp(parsed, 8, 200);
                _slideFields[sizeKey] = Math.Abs(nv - parsed) > 0.0001f
                    ? ((int)Math.Round(nv)).ToString() : newSizeText;
            }
            else if (newSizeText.StartsWith(sizeText) && newSizeText.Length > sizeText.Length
                && float.TryParse(newSizeText.Substring(sizeText.Length), out parsed))
            {
                nv = Mathf.Clamp(parsed, 8, 200);
                _slideFields[sizeKey] = Math.Abs(nv - parsed) > 0.0001f
                    ? ((int)Math.Round(nv)).ToString() : newSizeText;
            }
            else _slideFields[sizeKey] = curF.ToString("F0");
        }
        else if (Math.Abs(nv - curF) > 0.5f)
            _slideFields[sizeKey] = ((int)Math.Round(nv)).ToString();
        GUILayout.EndHorizontal();
        int newSize = (int)Math.Round(nv);
        if (Math.Abs(newSize - GetFontSize(slot)) > 0.001f)
        {
            switch (slot)
            {
                case FontSlot.Main: MainFontSize = newSize; break;
                case FontSlot.BPM: BPMFontSize = newSize; break;
                case FontSlot.Judgement: JudgeFontSize = newSize; break;
                case FontSlot.ComboTitle: ComboTitleFontSize = newSize; break;
                case FontSlot.ComboVal: ComboValFontSize = newSize; break;
                case FontSlot.Timing: TimingFontSize = newSize; break;
                case FontSlot.Attempt: AttemptFontSize = newSize; break;
            }
            Overlayer.Overlay.Instance?.ApplyFontSizes();
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    void DrawLabelsSection()
    {
        if (GUILayout.Button($"{( _labelsFold ? "▼" : "▷")} {Tr.Get(Tr.Key.CustomLabels)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _labelsFold = !_labelsFold;
        if (!_labelsFold) return;
        GUI.changed = false;

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        // GUILayout.Button 不会置位 GUI.changed，手动置位以走 RefreshAllTexts 通道刷新覆盖层。
        if (GUILayout.Button("English Preset", GUILayout.ExpandWidth(false))) { Labels = LabelConfig.GetPreset(Language.English); Labels.Save(); GUI.changed = true; }
        if (GUILayout.Button("한국어", GUILayout.ExpandWidth(false))) { Labels = LabelConfig.GetPreset(Language.Korean); Labels.Save(); GUI.changed = true; }
        if (GUILayout.Button("中文", GUILayout.ExpandWidth(false))) { Labels = LabelConfig.GetPreset(Language.Chinese); Labels.Save(); GUI.changed = true; }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("  -- Standard --");
        DrawLabelField("Progress", ref Labels.Progress);
        DrawLabelField("Accuracy", ref Labels.Accuracy);
        DrawLabelField("XAccuracy", ref Labels.XAccuracy);
        DrawLabelField("Music Time", ref Labels.MusicTime);
        DrawLabelField("Map Time", ref Labels.MapTime);
        DrawLabelField("CheckPoint", ref Labels.Checkpoint);
        DrawLabelField("Best", ref Labels.Best);
        DrawLabelField("TBPM", ref Labels.TBPM);
        DrawLabelField("CBPM", ref Labels.CBPM);
        DrawLabelField("KPS", ref Labels.KPS);
        DrawLabelField("Attempt", ref Labels.Attempt);
        DrawLabelField("Full Attempt", ref Labels.FullAttempt);
        DrawLabelField("Timing Scale", ref Labels.TimingScale);
        DrawLabelField("XPerfect", ref Labels.XPerfectLabel);
        DrawLabelField("Perfect", ref Labels.PerfectLabel);
        DrawLabelField("Great", ref Labels.GreatLabel);
        DrawLabelField("Good", ref Labels.GoodLabel);
        DrawLabelField("Combo Title", ref Labels.ComboTitle);
        DrawLabelField("Combo Title Alt", ref Labels.ComboTitleAlt);

        GUILayout.Space(3);
        GUILayout.Label("  -- ExtendedOverlay --");
        DrawLabelField("FPS", ref Labels.FPS);
        DrawLabelField("Author", ref Labels.Author);
        DrawLabelField("State", ref Labels.State);
        DrawLabelField("Death", ref Labels.Death);
        DrawLabelField("Start", ref Labels.Start);
        DrawLabelField("Timing", ref Labels.Timing);

        GUILayout.Space(3);
        GUILayout.Label("  -- State Texts --");
        DrawLabelField("Waiting", ref Labels.StateWaiting);
        DrawLabelField("Auto Tile", ref Labels.StateAutoTile);
        DrawLabelField("Auto", ref Labels.StateAuto);
        DrawLabelField("Perfect Play", ref Labels.StatePerfectPlay);
        DrawLabelField("Completed", ref Labels.StateComplete);
        DrawLabelField("Clear", ref Labels.StateClear);
        DrawLabelField("No Miss", ref Labels.StateNoMiss);
        DrawLabelField("Perfectionist", ref Labels.StatePerfectionist);
        DrawLabelField("Suffix", ref Labels.StateSuffix);
        DrawLabelField("Mid Start", ref Labels.StateMidStart);

        if (GUI.changed)
        {
            var o = Overlay.Instance;
            if (o != null && o.GameObject.activeSelf)
                o.RefreshAllTexts();
        }
    }

    static void DrawLabelField(string label, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(label, GUILayout.Width(100));
        value = GUILayout.TextField(value, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
    }

    static Action ColorChanged(Action updateOverlay) => () => updateOverlay?.Invoke();

    static bool DrawJColorFoldout(string key, string label, ColorCache cache, Action onReset = null)
    {
        bool expanded = _jColorFold.TryGetValue(key, out var v) && v;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        expanded = GUILayout.Toggle(expanded, expanded ? "▼" : "▷", _foldBtn, GUILayout.Width(18));
        if (GUILayout.Button(label, GUI.skin.label)) expanded = !expanded;
        GUILayout.FlexibleSpace();
        if (onReset != null && GUILayout.Button("R", GUILayout.MinWidth(20))) { onReset(); }
        GUILayout.EndHorizontal();
        _jColorFold[key] = expanded;
        if (!expanded) return false;
        GUILayout.BeginHorizontal();
        GUILayout.Space(32);
        GUILayout.BeginVertical();
        cache.SettingGUI("", cache);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        return true;
    }

    static readonly int[] AlignValues = [257, 258, 260, 513, 514, 516, 1025, 1026, 1028];
    static readonly string[] AlignLabels = ["TL", "T", "TR", "L", "C", "R", "BL", "B", "BR"];

    static string AlignLabel(int v)
    {
        int idx = Array.IndexOf(AlignValues, v);
        return idx >= 0 ? AlignLabels[idx] : "C";
    }

    static void DrawAlignment(string label, ref int align, ref int style)
    {
        bool expanded = _expandedAlign == label;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label(label, GUILayout.ExpandWidth(true));
        GUILayout.Label(AlignLabel(align), GUILayout.Width(28));
        if (GUILayout.Button(expanded ? "▲" : "▼", GUILayout.Width(20), GUILayout.Height(18)))
            _expandedAlign = expanded ? null : label;
        (int bit, Tr.Key key)[] styles = [(1, Tr.Key.StyleBold), (2, Tr.Key.StyleItalic), (4, Tr.Key.StyleUnderline), (64, Tr.Key.StyleStrike), (512, Tr.Key.StyleHighlight)];
        foreach (var st in styles)
        {
            bool active = (style & st.bit) != 0;
            bool now = GUILayout.Toggle(active, Tr.Get(st.key), GUILayout.Width(24), GUILayout.Height(18));
            if (now != active) { style = now ? (style | st.bit) : (style & ~st.bit); Overlayer.Overlay.Instance?.ApplyFontStyle(); }
        }
        GUILayout.EndHorizontal();

        if (!expanded) return;
        int idx = Array.IndexOf(AlignValues, align);
        if (idx < 0) idx = 4;
        GUILayout.BeginHorizontal();
        GUILayout.Space(24);
        int newIdx = GUILayout.SelectionGrid(idx, AlignLabels, 3, GUILayout.Height(60));
        GUILayout.EndHorizontal();
        if (newIdx != idx) { align = AlignValues[newIdx]; Overlayer.Overlay.Instance?.ApplyAlignment(); }
    }

    public void OnSaveGUI() { Save(); Colors?.Save(); Labels?.Save(); }
    public void Save() { SaveJson(); }
    public static Settings Load()
    {
        bool freshConfig = !File.Exists(SettingsPath()) && !File.Exists(OldXmlPath());
        var s = LoadJson() ?? LoadXmlFallback() ?? new Settings();
        // 全新安装：首启观感对齐旧的普通模式默认（扩展文本与宽松连击全关），
        // 与旧配置迁移规则保持一致；想默认全开改这里的强制项即可
        if (freshConfig)
        {
            s.ShowFPS = s.ShowAuthor = s.ShowState = s.ShowDeath = s.ShowStart = s.ShowTiming = false;
            s.AllowELCombo = s.AllowOrangeCombo = s.CheckPseudo = false;
            s.HideDebugText = s.RemoveNotRequireInAuto = s.RepositionAutoText = false;
            s.DetailedProgress = s.TimeDecimals = false;
        }
        // 手改 Settings.json 的越界语言值会让 Tr.Get 每次 GUI 绘制越界崩溃，这里钳制。
        if (s.CurrentLanguage < Language.English || s.CurrentLanguage > Language.Chinese)
            s.CurrentLanguage = Language.English;
        if (s.ConfigVersion < 2)
        {
            float Sw = 1920, Sh = 1080;
            s.MainOffsetX = s.MainPX * Sw - 16;
            s.MainOffsetY = (s.MainPY - 1f) * Sh + 16;
            s.BPMOffsetX = (s.BPMPX - 1f) * Sw + 16;
            s.BPMOffsetY = (s.BPMPY - 1f) * Sh + 16;
            s.JudgeOffsetX = (s.JudgePX - 0.5f) * Sw;
            s.JudgeOffsetY = s.JudgePY * Sh - (s.JudgementLocationUp ? 85f : 5f);
            s.ComboOffsetX = (s.ComboPX - 0.5f) * Sw;
            s.ComboOffsetY = (s.ComboPY - 1f) * Sh + 43f + 14f * s.Size;
            s.TimingOffsetX = (s.TimingPX - 0.5f) * Sw;
            s.TimingOffsetY = s.TimingPY * Sh - 90f - 40f * s.Size;
            s.AttemptOffsetX = (s.AttmptPX - 0.5f) * Sw - 310f;
            s.AttemptOffsetY = s.AttmptPY * Sh - 35f;
            s.ProgBarOffsetX = (s.ProgBarPX - 0.5f) * Sw;
            s.ProgBarOffsetY = (s.ProgBarPY - 1f) * Sh + 10f;
            s.ConfigVersion = 2;
            s.Save();
        }
        s.Colors = ColorConfig.Load();
        s.Labels = LabelConfig.Load();
        // 唯一的显示顺序：原 GeneralDisplayOrder 并入（未启用扩展文本时，多余元素自然不显示）
        if (s.ExtendedDisplayOrder == null || s.ExtendedDisplayOrder.Length == 0)
            s.ExtendedDisplayOrder = GetDefaultExtendedOverlayOrder();
        else
            // 先剔除越界值，再补齐旧配置里缺失的元素：新增显示元素（如 XScore / 潜力值）
            // 若不在顺序数组里，SetupLocation 的 foreach 根本不会遍历到它们，
            // 表现为「开关打开了但文本永远不显示」。补齐追加在末尾，不打乱用户已排的顺序。
            s.ExtendedDisplayOrder = AppendMissingExtendedOverlayElements(
                s.ExtendedDisplayOrder.Where(x => IsValidExtendedOverlayElement(x)).ToArray());
        if (s.BpmLineOrder == null || s.BpmLineOrder.Length == 0)
            s.BpmLineOrder = [0, 1, 2];
        if (s.BpmLineVisibility == null || s.BpmLineVisibility.Length != 3)
            s.BpmLineVisibility = [true, true, true];
        if (s.AttemptLineOrder == null || s.AttemptLineOrder.Length == 0)
            s.AttemptLineOrder = [0, 1];
        return s;
    }

    // ===== JSON 持久化 =====

    private static string SettingsPath() =>
        Path.Combine(Loader.ModPath, "Settings.json");

    private static string OldXmlPath() =>
        Path.Combine(Loader.ModPath, "Settings.xml");

    private void SaveJson()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath(), json);
        }
        catch (Exception e)
        {
            Loader.Warning($"Failed to save Settings.json: {e.Message}");
        }
    }

    private static Settings LoadJson()
    {
        try
        {
            var path = SettingsPath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var s = JsonConvert.DeserializeObject<Settings>(json);
            ApplyLegacyExtendedOverlayMigration(s, json);
            return s;
        }
        catch (Exception e)
        {
            Loader.Warning($"Failed to load Settings.json: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 旧配置迁移：ExtendedOverlayMode 总开关移除后，原「普通模式」用户的配置里六个扩展文本与
    /// ExtendedOverlay 行为项虽然保存着默认值 true，但从未实际生效。若不迁移，拆分开关会让这些功能
    /// 一进游戏全部点亮。规则：配置里 ExtendedOverlayMode=false → 全部关掉，保持原观感；
    /// ExtendedOverlayMode=true → 富格式/时间小数随迁，其余原样保留。
    ///
    /// ⚠ 只在配置中仍残留 ExtendedOverlayMode 键时执行一次：新构建保存配置时该键即被丢弃，
    /// 之后必须完全不再触碰这些开关，否则用户每次手动开启的开关都会在下次启动时被抹掉
    /// （「键缺失」≠「旧普通模式」——缺失只说明已经迁移过、或本就是新构建写的配置）。
    /// </summary>
    static void ApplyLegacyExtendedOverlayMigration(Settings s, string json)
    {
        if (s == null) return;
        bool hasKey;
        bool legacyMode = false;
        try
        {
            var jo = JsonConvert.DeserializeObject<JObject>(json);
            hasKey = jo?["ExtendedOverlayMode"] != null;
            if (hasKey) legacyMode = jo!["ExtendedOverlayMode"]!.Value<bool>();
        }
        catch { return; }
        ApplyLegacyMigrationCore(s, hasKey, legacyMode);
    }

    /// <summary>XML 老配置的同一迁移：以 &lt;ExtendedOverlayMode&gt; 元素是否存在为准。</summary>
    static void ApplyLegacyExtendedOverlayMigrationXml(Settings s, string xmlText)
    {
        if (s == null) return;
        int i = xmlText.IndexOf("<ExtendedOverlayMode>", StringComparison.Ordinal);
        if (i < 0) { ApplyLegacyMigrationCore(s, false, false); return; }
        int start = i + "<ExtendedOverlayMode>".Length;
        int end = xmlText.IndexOf("</ExtendedOverlayMode>", start, StringComparison.Ordinal);
        bool legacy = end > start && xmlText.Substring(start, end - start).Trim() == "true";
        ApplyLegacyMigrationCore(s, true, legacy);
    }

    static void ApplyLegacyMigrationCore(Settings s, bool hasKey, bool legacyMode)
    {
        if (!hasKey) return;
        if (legacyMode)
        {
            // 原 ExtendedOverlay 用户：富格式进度、带小数时间随迁，精度保持其已保存值
            s.DetailedProgress = s.TimeDecimals = true;
            return;
        }
        s.ShowFPS = s.ShowAuthor = s.ShowState = s.ShowDeath = s.ShowStart = s.ShowTiming = false;
        s.AllowELCombo = s.AllowOrangeCombo = s.CheckPseudo = false;
        s.HideDebugText = s.RemoveNotRequireInAuto = s.RepositionAutoText = false;
        // 原普通模式用户：主文本固定 2 位小数，富格式/时间小数关闭
        s.DetailedProgress = s.TimeDecimals = false;
        s.ExtendedDecimalPrecision = 2;
    }

    /// <summary>从旧的 UMM XML 加载，迁移后删除 XML 文件。</summary>
    private static Settings LoadXmlFallback()
    {
        var path = OldXmlPath();
        if (!File.Exists(path)) return null;

        try
        {
            string xmlText = File.ReadAllText(path);
            using var reader = new StreamReader(path);
            var xml = new XmlSerializer(typeof(Settings));
            var s = (Settings)xml.Deserialize(reader);
            // 迁移必须在序列化之前完成（旧写法先序列化再迁移，迁移结果落不了盘）
            ApplyLegacyExtendedOverlayMigrationXml(s, xmlText);
            var json = JsonConvert.SerializeObject(s, Formatting.Indented);
            File.WriteAllText(SettingsPath(), json);
            File.Delete(path);
            Loader.Log("Settings: migrated from XML to JSON");
            return s;
        }
        catch (Exception e)
        {
            Loader.Warning($"Failed to migrate Settings.xml: {e.Message}");
            return null;
        }
    }
}
