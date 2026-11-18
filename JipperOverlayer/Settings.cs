using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// <summary>判定条旁「打击误差」的独立小数位（issue #6）。不复用 TimingDecimal：
    /// 后者的滑条只在 ShowTiming 开启时可见，只开误差文本就没法调精度。</summary>
    public int HitOffsetDecimal = 5;

    /// <summary>任一 ExtendedOverlay 扩展文本开启。拆掉总开关后，用它决定「ExtendedOverlay 风格」的连带表现
    /// （富格式进度文本、扩展文本的小数精度等），避免普通模式用户被动接受这些变化。</summary>
    public bool ExtendedStyleActive => ShowFPS || ShowAuthor || ShowState || ShowDeath || ShowStart || ShowTiming;

    /// <summary>任一挂在主容器下的栈式文本开启（含 XScore 与三个潜力值文本）。
    /// 主容器一旦 SetActive(false)，其下所有文本都不渲染；布局也只在任一元素开启时才跑。
    /// 新增显示元素必须并入此处，否则会出现「开关打开、文本却永远不显示」。</summary>
    public bool AnyStackedTextVisible => ShowProgress || ShowAccuracy || ShowXAccuracy || ShowMusicTime
        || ShowMapTime || ShowCheckpoint || ShowBest || ShowXScore || ExtendedStyleActive
        || HasCustomStackText;

    /// <summary>是否存在「参与主栈」的自定义行（它们同样需要主容器可见并参与布局）。</summary>
    [JsonIgnore]
    public bool HasCustomStackText
    {
        get
        {
            if (CustomTexts == null) return false;
            for (int i = 0; i < CustomTexts.Count; i++)
            {
                var line = CustomTexts[i];
                if (line != null && line.Enabled && line.InStack && !string.IsNullOrEmpty(line.Template)) return true;
            }
            return false;
        }
    }
    public float FPSRefreshRate = 0.2f;
    public int ExtendedDecimalPrecision = 2;
    public bool HideDebugText = true, ShowDeath = true, ShowStart = true, ShowTiming = true;
    /// <summary>隐藏关卡标题（曲名）。直接写游戏闲置静态字段 GCS.d_dontShowTitles——
    /// scrHUDText.Update 每帧读它禁用 isTitle 的标题 Graphic，零补丁原生隐藏
    /// （实现口径参照 Overlayer.Module.ADOFAI 的 HideTitle）。</summary>
    public bool HideTitle;
    /// <summary>在判定条旁显示本次敲击的毫秒误差（issue #6）。独立于 ShowTiming：
    /// 主栈的 Timing 文本在左上角，这个专门贴着判定条，方便打击瞬间余光读取。</summary>
    public bool ShowHitOffset;
    public bool RemoveNotRequireInAuto = true, CheckPseudo = true, AllowELCombo = true, AllowOrangeCombo = true;
    public bool ComboTitleAltOnNonPerfect = false;
    /// <summary>结算界面「完美」明细：仅在判定文本前把版式预设临时压成 Default，让游戏原生
    /// 输出「稍快 / 完美 / 稍慢」三列同一行，随后还原，并把 [+X/-] 三分量括号追加到中间那一列。
    /// 仅 r149+（原生 XPerfect）可用，默认关闭以免改变既有结算观感。</summary>
    public bool ShowResultsPerfectDetail = false;
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

    /// <summary>各字体槽的独立文本特效（阴影/描边）。索引 = FontSlot 序号。
    /// 未开启 Override 的槽位沿用全局 TextEffects，与旧版观感完全一致。</summary>
    public SlotTextEffectConfig[] SlotTextEffects = CreateDefaultSlotTextEffects();

    static SlotTextEffectConfig[] CreateDefaultSlotTextEffects()
    {
        int n = Enum.GetValues(typeof(FontSlot)).Length;
        var arr = new SlotTextEffectConfig[n];
        for (int i = 0; i < n; i++) arr[i] = new SlotTextEffectConfig();
        return arr;
    }

    /// <summary>取该槽位的特效配置对象（可能未开启 Override）。</summary>
    public SlotTextEffectConfig GetSlotTextEffect(FontSlot slot)
    {
        var arr = SlotTextEffects;
        int idx = (int)slot;
        if (arr == null || idx < 0 || idx >= arr.Length) return null;
        return arr[idx] ??= new SlotTextEffectConfig();
    }

    /// <summary>该槽位实际生效的特效：开了 Override 用自己的，否则用全局。</summary>
    public TextEffectConfig GetEffectForSlot(FontSlot slot)
    {
        var sc = GetSlotTextEffect(slot);
        if (sc != null && sc.Override && sc.Effect != null) return sc.Effect;
        return TextEffects;
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
        if (idx < 0) idx = FontIndex;
        // 字体列表会随版本/自定义字体增删而变化；手改配置也可能留下越界下标。
        return FontManager.FontNames == null || idx >= FontManager.FontNames.Length ? 0 : idx;
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
    /// <summary>判定条旁「打击误差」文本的额外偏移（issue #6）。</summary>
    public float HitOffsetX, HitOffsetY;
    public int ConfigVersion;
    public bool ShowXPerfectInJudgement;
    public bool ShowAutoInXPerfect;
    // 统一后的唯一显示顺序（原 GeneralDisplayOrder 已并入：两个模式本就是同一个栈式布局）
    public int[] ExtendedDisplayOrder = [10, 11, 0, 1, 2, 3, 4, 5, 6, 12, 13, 14, 15, 16, 17, 18, 19, 20];
    public int[] BpmLineOrder = [0, 1, 2];
    public bool[] BpmLineVisibility = [true, true, true];
    public int[] AttemptLineOrder = [0, 1];
    public bool ComboLineReversed;

    /// <summary>玩家自定义文本行（可增删，见 CustomTextLine）。</summary>
    public List<CustomTextLine> CustomTexts = new();
    /// <summary>用户自定义组合 tag（名称 → 模板片段，见 CustomTagDef / TagManager.SetCustomTags）。</summary>
    public List<CustomTagDef> CustomTags = new();

    [JsonIgnore] public ColorConfig Colors;
    [JsonIgnore] public LabelConfig Labels;
    /// <summary>隐藏期间编辑过标签；下一次 Show 会主动重绘，避免沿用旧语言/旧缓存。</summary>
    internal bool LabelsDirty { get; set; }
    private static readonly string[] LanguageNames = ["English", "한국어", "中文"];
    private static readonly FontSlot[] FontSlots = (FontSlot[])Enum.GetValues(typeof(FontSlot));

    public void OnGUI()
    {
        // UMM 在同一次 IMGUI pass 里先画自己的控件再调本回调，其控件可能把
        // GUI.changed 置真——不清零的话「有改动」会跨帧污染，RefreshVisibility
        // 在设置窗口每帧都跑，非游玩模式下的行位置跟着游戏态条件漂移。
        GUI.changed = false;
        DrawGeneralSection();
        bool needsVisibilityRefresh = GUI.changed;
        DrawDisplaySection();
        needsVisibilityRefresh |= GUI.changed;
        DrawTextSettings();
        needsVisibilityRefresh |= GUI.changed;
        DrawTextEffectsSection();
        needsVisibilityRefresh |= GUI.changed;
        DrawLabelsSection();
        needsVisibilityRefresh |= GUI.changed;
        DrawTemplatesSection();
        needsVisibilityRefresh |= GUI.changed;
        DrawCustomTextsSection();
        needsVisibilityRefresh |= GUI.changed;
        DrawCustomTagsSection();
        needsVisibilityRefresh |= GUI.changed;
        DrawDiagnosticsSection();
        needsVisibilityRefresh |= GUI.changed;
        // RefreshVisibility 会重排文本、字体、位置与原生 UI；只在设置真的改变时执行，
        // 不再在每个 IMGUI Layout 事件中重复整轮工作。分节累计 GUI.changed 是必要的：
        // DrawLabelsSection 会把它清零。
        if (needsVisibilityRefresh)
            Overlay.Instance?.RefreshVisibility();
    }

    void DrawGeneralSection()
    {
        if (GUILayout.Button($"{( _generalFold ? "▼" : "▷")} {Tr.Get(Tr.Key.General)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _generalFold = !_generalFold;
        if (!_generalFold) return;

        SlideField(ref Size, Tr.Get(Tr.Key.Size), 0, 2, () => Overlayer.Overlay.Instance?.UpdateSize());

        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr.Get(Tr.Key.LangLabel), GUILayout.Width(100));
        int langIdx = (int)CurrentLanguage;
        int newLang = GUILayout.SelectionGrid(langIdx, LanguageNames, 3);
        if (newLang != langIdx)
        {
            CurrentLanguage = (Overlayer.Localization.Language)newLang;
            var o = Overlayer.Overlay.Instance;
            if (o != null)
            {
                if (o.GameObject.activeSelf) { o.RefreshAllTexts(); o.RefreshVisibility(); }
                else LabelsDirty = true;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (GUILayout.Button($"{( _fontFold ? "▼" : "▷")} {Tr.Get(Tr.Key.Font)}", GUI.skin.label, GUILayout.ExpandWidth(false)))
            _fontFold = !_fontFold;
        if (_fontFold && FontManager.FontNames != null)
        {
            if (GUILayout.Button(Tr.Get(Tr.Key.RescanFonts), GUILayout.Width(150)))
            {
                FontManager.ScanFonts();
                Overlayer.Overlay.Instance?.ApplyFontToAll();
            }
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
            foreach (FontSlot slot in FontSlots)
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
                PosSlide2(Tr.Get(Tr.Key.PosHitOffset), ref HitOffsetX, ref HitOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosAttempt), ref AttemptOffsetX, ref AttemptOffsetY);
                if (VersionSafe.IsV141OrLater)
                    PosSlide2($"{Tr.Get(Tr.Key.PosAttempt)}\n{Tr.Get(Tr.Key.Coop)}", ref AttemptCoopOffsetX, ref AttemptCoopOffsetY);
                PosSlide2(Tr.Get(Tr.Key.PosProgBar), ref ProgBarOffsetX, ref ProgBarOffsetY);
            });
            if (GUILayout.Button(Tr.Get(Tr.Key.ResetPositions), GUILayout.ExpandWidth(false)))
                ResetCustomPos();
        }

        // FreeMake 风格的拖拽编辑器入口：比逐个拉 X/Y 滑杆直观得多，
        // 写回的仍是同一组偏移字段（见 OverlayDragEditor）。
        bool editingLayout = Overlayer.OverlayDragEditor.IsEditing;
        if (GUILayout.Button(Tr.Get(Tr.Key.OpenLayoutEditor) + (editingLayout ? "  ●" : "")))
            Overlayer.OverlayDragEditor.Instance?.SetActive(!editingLayout);

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
                DecSlideField(ref AccuracyDecimal, "accDec", Tr.Key.DecimalPrecision, 4,
                    () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                EnumSelField(ref AccuracyTextType, Tr.Key.PotentialDisplay, TextTypeNames.Name, () =>
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
                DecSlideField(ref XAccuracyDecimal, "xaccDec", Tr.Key.DecimalPrecision, 4,
                    () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                EnumSelField(ref XAccuracyTextType, Tr.Key.PotentialDisplay, TextTypeNames.Name, () =>
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
                EnumSelField(ref XScoreTextType, Tr.Key.ScoreFormat, TextTypeNames.Name, () => Overlayer.Overlay.Instance?.UpdateAccuracy());
                EnumSelField(ref XScorePotentialType, Tr.Key.PotentialDisplay, TextTypeNames.Name, () =>
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
                DecSlideField(ref BestDecimal, "bestDec", Tr.Key.DecimalPrecision, 4,
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
                IntSlideField(ref ComboColorMax, Tr.Get(Tr.Key.ComboColorMax), 1, 5000, null);
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
                SlideField(ref BpmColorMax, Tr.Get(Tr.Key.BpmColorMax), 100, 20000, () =>
                {
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.DirtyBpmCache(); o.UpdateBPM(); }
                });
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
            // 结算界面「完美」明细：三分量只有 r149+ 才有；r148 上不显示该开关
            if (HitMarginCompat.HasNativeXPerfect)
                ShowResultsPerfectDetail = Tog(Tr.Get(Tr.Key.ShowResultsPerfectDetail), ShowResultsPerfectDetail);
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
                SlideField(ref FPSRefreshRate, Tr.Get(Tr.Key.FPSRefreshRate), 0.05f, 1f, null);
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
                DecSlideField(ref TimingDecimal, "timingDec", Tr.Key.DecimalPrecision, 5,
                    () => Overlay.Instance?.ExtendedOverlay?.RefreshTiming());
                EnumSelField(ref TimingTextType, Tr.Key.TimingDisplay, TextTypeNames.Name, () =>
                {
                    var o = Overlay.Instance;
                    if (o != null) { o.SetupLocationMain(); o.ExtendedOverlay?.RefreshTiming(); }
                });
                HelpLabel(Tr.Get(Tr.Key.HelpTimingMode));
            }
            // 判定条旁的本次敲击误差（issue #6）：与上方主栈 Timing 是同一个采样值，
            // 但贴判定条独立显示，方便打击瞬间余光读取。
            ShowHitOffset = TogR(Tr.Key.ShowHitOffset, ShowHitOffset);
            if (ShowHitOffset)
            {
                DecSlideField(ref HitOffsetDecimal, "hitOffsetDec", Tr.Key.DecimalPrecision, 5,
                    () => Overlay.Instance?.ExtendedOverlay?.RefreshHitOffset());
                Colors.JHitOffset.SettingGUI(ColorChanged(() => Overlay.Instance?.ExtendedOverlay?.RefreshHitOffset()), Tr.Get(Tr.Key.HitOffsetColor),
                    () => { Colors.JHitOffset = new([(0f, Color.red), (1f, Color.green)]); Colors.Save(); });
                HelpLabel(Tr.Get(Tr.Key.HelpHitOffset));
            }
            DrawGlobalColorsSection();
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
            HideTitle = Tog(Tr.Get(Tr.Key.HideTitle), HideTitle); // 每帧由 Main.OnUpdate 写入 GCS
            RemoveNotRequireInAuto = Tog(Tr.Get(Tr.Key.RemoveAutoReq), RemoveNotRequireInAuto);
            bool prevPseudo = CheckPseudo;
            CheckPseudo = Tog(Tr.Get(Tr.Key.CheckPseudo), CheckPseudo);
            if (prevPseudo != CheckPseudo)
            {
                var bpmOverlay = Overlay.Instance;
                if (bpmOverlay != null) { bpmOverlay.DirtyBpmCache(); bpmOverlay.UpdateBPM(); }
            }
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

        // 所有显示类开关都在本节内。显示开关与 Harmony 补丁是动态门控关系：
        // 只改字段而不重新注册，开关打开后下一格/下一击的回调仍然缺席。
        // 在字段已写回后统一同步一次；GUI.changed 只在用户操作时为真，成本可忽略。
        if (GUI.changed) PatchManager.RefreshPatches();
    }

    /// <summary>带重排的开关：先返回新值，字段由调用方写回；补丁/布局刷新在
    /// DrawDisplaySection 末尾统一执行，避免回调读到旧开关状态。</summary>
    bool TogR(Tr.Key key, bool value) => Tog(Tr.Get(key), value);

    void DrawGlobalColorsSection()
    {
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        // 本节嵌在 DrawDisplaySub("jDisplay") 内，不能共用 _expandedDisplaySub：
        // 换 key 写入会让父级判定自己收起，下一帧连本节一起消失。
        bool expanded = _globalColorsFold;
        if (GUILayout.Button($"{(expanded ? "▼" : "▷")} {Tr.Get(Tr.Key.GlobalColors)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _globalColorsFold = !expanded;
        GUILayout.EndHorizontal();
        if (expanded)
        {
            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUILayout.Space(36);
            GUILayout.BeginVertical();

            GUILayout.Label(Tr.Get(Tr.Key.BaseColor), GUILayout.ExpandWidth(true));
            _globalBaseColor.SettingGUI("Base", (Color)_globalBaseColor);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _presetColors.Length; i++)
            {
                var c = _presetColors[i];
                var prev = GUI.color; GUI.color = c;
                if (GUILayout.Button(_presetLabels[i], GUILayout.Width(52)))
                    _globalBaseColor.SetPreset(c);
                GUI.color = prev;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            // 预览：对几个代表性渐变取 0 / 0.5 / 1 三点，展示“当前 -> 预览”色块
            GUILayout.Label(Tr.Get(Tr.Key.Preview), GUILayout.ExpandWidth(true));
            DrawGlobalColorPreviewRow("Progress", Colors.Progress);
            DrawGlobalColorPreviewRow("Accuracy", Colors.Accuracy);
            DrawGlobalColorPreviewRow("BPM", Colors.Bpm);
            DrawGlobalColorPreviewRow("Combo", Colors.Combo);

            GUILayout.Space(4);
            HelpLabel(Tr.Get(Tr.Key.AllColorHint));

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tr.Get(Tr.Key.ApplyToAll), GUILayout.Height(26)))
            {
                Color baseColor = _globalBaseColor;
                Colors.ApplyHueToAll(baseColor);
                Overlay.Instance?.RefreshAllTexts();
                Loader.Log("Colors: Apply hue to all done");
            }
            if (GUILayout.Button(Tr.Get(Tr.Key.ResetAllColors), GUILayout.Height(26)))
            {
                Colors.ResetAll();
                Overlay.Instance?.RefreshAllTexts();
                Loader.Log("Colors: Reset all done");
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

    void DrawGlobalColorPreviewRow(string label, ColorPerDictionary dict)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(70));
        float[] samples = [0f, 0.5f, 1f];
        Color baseColor = _globalBaseColor;
        foreach (float t in samples)
        {
            Color cur = dict.GetColor(t);
            Color pre = dict.GetPreviewColor(t, baseColor);
            var prev = GUI.color;
            GUI.color = cur;
            GUILayout.Label("■", GUILayout.Width(16));
            GUI.color = Color.white;
            GUILayout.Label("→", GUILayout.Width(12));
            GUI.color = pre;
            GUILayout.Label("■", GUILayout.Width(16));
            GUI.color = prev;
            GUILayout.Space(6);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
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

    /// <summary>把显示顺序规整为「合法、去重、保留用户相对顺序、缺失项追加到末尾」。
    /// 旧版配置可能没有新元素，重复/负值则会重复布局或越界访问可见性数组。</summary>
    static int[] NormalizeOrder(int[] order, int[] defaults, Func<int, bool> isValid)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        if (order != null)
            foreach (int id in order)
                if (isValid(id) && seen.Add(id)) result.Add(id);
        foreach (int id in defaults)
            if (seen.Add(id)) result.Add(id);
        return result.ToArray();
    }

    static bool OrderDiffers(int[] before, int[] after)
        => before == null || after == null || !before.SequenceEqual(after);

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
            id => id >= 0 && id < BpmLineVisibility.Length && BpmLineVisibility[id],
            (id, v) => { if (id >= 0 && id < BpmLineVisibility.Length) { BpmLineVisibility[id] = v; Save(); var o = Overlay.Instance; o?.DirtyBpmCache(); o?.UpdateBPM(); } });
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

        bool changed = DrawEffectControls(TextEffects, 16);

        if (changed)
        {
            Save();
            ShadowManager.ClearCache();
            Overlay.Instance?.ApplyFontToAll();
        }
    }

    /// <summary>阴影/描边控件（全局与各槽位独立特效共用）。indent 为左侧缩进。</summary>
    bool DrawEffectControls(TextEffectConfig fx, int indent)
    {
        bool changed = false;

        // Shadow
        GUILayout.BeginHorizontal();
        GUILayout.Space(indent);
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
        GUILayout.Space(indent);
        bool outlineOn = GUILayout.Toggle(fx.OutlineEnabled, Tr.Get(Tr.Key.TextEffectOutline), GUILayout.ExpandWidth(true));
        if (outlineOn != fx.OutlineEnabled) { fx.OutlineEnabled = outlineOn; changed = true; }
        GUILayout.EndHorizontal();

        if (fx.OutlineEnabled)
        {
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectOutlineWidth), ref fx.OutlineWidth, 0f, 0.5f);
            changed |= DrawFloatField(Tr.Get(Tr.Key.TextEffectOutlineSoftness), ref fx.OutlineSoftness, 0f, 1f);
            changed |= DrawColorCacheField(Tr.Get(Tr.Key.TextEffectOutlineColor), fx.OutlineColor);
        }

        return changed;
    }

    /// <summary>字体槽的独立文本特效：默认沿用全局，开启后该槽位单独配置阴影/描边。
    /// 开启时从全局复制一份作为起点，避免"打开就变成另一套观感"。</summary>
    void DrawSlotEffectPanel(FontSlot slot)
    {
        var sc = GetSlotTextEffect(slot);
        if (sc == null) return;

        GUILayout.Space(3);
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        bool now = GUILayout.Toggle(sc.Override, Tr.Get(Tr.Key.TextEffectPerSlot), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();

        if (now != sc.Override)
        {
            sc.Override = now;
            // 首次开启：从全局复制一份当前配置作为起点；关闭时保留配置便于再次打开。
            if (now) sc.Effect = TextEffects?.Clone() ?? new TextEffectConfig();
            Save();
            ShadowManager.ClearCache();
            Overlay.Instance?.ApplyFontToAll();
        }

        if (!sc.Override) return;

        // 继承全局的快捷操作：把全局设置搬过来
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        if (GUILayout.Button(Tr.Get(Tr.Key.TextEffectCopyGlobal), GUILayout.Width(180), GUILayout.Height(20)))
        {
            sc.Effect = TextEffects?.Clone() ?? new TextEffectConfig();
            Save();
            ShadowManager.ClearCache();
            Overlay.Instance?.ApplyFontToAll();
        }
        GUILayout.EndHorizontal();

        if (DrawEffectControls(sc.Effect, 36))
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

    internal void ResetCustomPos()
    {
        MainOffsetX = MainOffsetY = BPMOffsetX = BPMOffsetY = JudgeOffsetX = JudgeOffsetY = 0;
        P1JudgeOffsetX = P1JudgeOffsetY = P2JudgeOffsetX = P2JudgeOffsetY = 0;
        P3JudgeOffsetX = P3JudgeOffsetY = P4JudgeOffsetX = P4JudgeOffsetY = 0;
        ComboOffsetX = ComboOffsetY = TimingOffsetX = TimingOffsetY = 0;
        AttemptOffsetX = AttemptOffsetY = AttemptCoopOffsetX = AttemptCoopOffsetY = ProgBarOffsetX = ProgBarOffsetY = 0;
        HitOffsetX = HitOffsetY = 0;
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
    /// （见 TextTypeNames），不再直接显示英文原始枚举名。字段赋值由 EnumSelField 完成，
    /// 避免 onChange 在旧字段值上执行。</summary>
    static T EnumSel<T>(Tr.Key key, T value, Func<T, string> nameOf) where T : struct, Enum
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr.Get(key), GUILayout.Width(140));
        if (GUILayout.Button(nameOf(value), GUILayout.Width(180)))
        {
            var vals = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(vals, value);
            value = vals[(i + 1) % vals.Length];
        }
        GUILayout.EndHorizontal();
        return value;
    }

    static void EnumSelField<T>(ref T field, Tr.Key key, Func<T, string> nameOf, Action onChange) where T : struct, Enum
    {
        T next = EnumSel(key, field, nameOf);
        if (EqualityComparer<T>.Default.Equals(next, field)) return;
        field = next;
        onChange?.Invoke();
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
    int DecSlide(string fieldKey, Tr.Key labelKey, int value, int max)
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
        return nv;
    }

    void DecSlideField(ref int field, string fieldKey, Tr.Key labelKey, int max, Action onChange)
    {
        int next = DecSlide(fieldKey, labelKey, field, max);
        if (next == field) return;
        field = next;
        onChange?.Invoke();
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static float Slide(string label, float v, float min, float max)
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
            if (float.TryParse(newText, out float parsed) && IsFinite(parsed))
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
        return Math.Abs(nv - v) > 0.001f ? nv : v;
    }

    static void SlideField(ref float field, string label, float min, float max, Action onChange)
    {
        float next = Slide(label, field, min, max);
        if (Math.Abs(next - field) <= 0.001f) return;
        field = next;
        onChange?.Invoke();
    }

    static void IntSlideField(ref int field, string label, float min, float max, Action onChange)
    {
        float next = Slide(label, field, min, max);
        int rounded = (int)Math.Round(next);
        if (rounded == field) return;
        field = rounded;
        onChange?.Invoke();
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
        if (ntx != tx) { _slideFields[label + "X"] = ntx; if (float.TryParse(ntx, out float p) && IsFinite(p)) nx = Mathf.Clamp(p, min, max); }
        else if (ntx == tx && Math.Abs(nx - vx) > 0.001f) _slideFields[label + "X"] = $"{(int)nx}";
        GUILayout.Space(4);
        GUILayout.Label("Y", GUILayout.Width(14));
        float ny = GUILayout.HorizontalSlider(vy, min, max, GUILayout.ExpandWidth(true));
        if (!_slideFields.TryGetValue(label + "Y", out var ty))
            _slideFields[label + "Y"] = ty = $"{(int)vy}";
        string nty = GUILayout.TextField(ty, GUILayout.Width(42));
        if (nty != ty) { _slideFields[label + "Y"] = nty; if (float.TryParse(nty, out float p) && IsFinite(p)) ny = Mathf.Clamp(p, min, max); }
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
    private static bool _globalColorsFold; // 独立状态：嵌套在"显示选项"内，见 DrawGlobalColorsSection 注释
    private static string _expandedAlign, _expandedDisplaySub, _expandedPos;
    private static string _expandedOrder, _expandedFontSlot;
    private static bool _labelsFold;
    private static bool _diagnosticsFold;
    private static bool _templateFold;
    private static bool _tagHelpOpen;
    private static string _tagSearch;
    private static bool _customTextFold;
    private static Vector2 _tagHelpScroll;
    private static string _lastCopiedTag;
    private static float _tagsCopiedUntil;
    private static float _diagnosticsCopiedUntil;
    private static readonly ColorCache _globalBaseColor = new(Color.white);
    private static readonly Color[] _presetColors = [Color.red, new Color(1, 0.5f, 0), Color.yellow, Color.green, new Color(0.34f, 0.8f, 1), Color.blue, new Color(0.6f, 0.2f, 1)];
    private static readonly string[] _presetLabels = ["Red", "Orange", "Yellow", "Green", "Sky", "Blue", "Purple"];

    void DrawFontSlotSelector(FontSlot slot)
    {
        string label = GetSlotLabel(slot);
        string key = $"FontSlot_{slot}";
        bool expanded = _expandedFontSlot == key;

        int rawIdx = GetRawSlotFontIndex(slot);
        bool useGlobal = rawIdx < 0;
        int resolvedIdx = GetFontIndexForSlot(slot);
        bool hasFonts = FontManager.FontNames != null && FontManager.FontNames.Length > 0
                        && resolvedIdx >= 0 && resolvedIdx < FontManager.FontNames.Length;
        string fontName = !hasFonts ? "<no font>" : useGlobal
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

        // 独立文本特效放在字体列表之前：展开就能看到，不用滚过一长串字体。
        DrawSlotEffectPanel(slot);

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
            if (float.TryParse(newSizeText, out float parsed) && IsFinite(parsed))
            {
                nv = Mathf.Clamp(parsed, 8, 200);
                _slideFields[sizeKey] = Math.Abs(nv - parsed) > 0.0001f
                    ? ((int)Math.Round(nv)).ToString() : newSizeText;
            }
            else if (newSizeText.StartsWith(sizeText) && newSizeText.Length > sizeText.Length
                && float.TryParse(newSizeText.Substring(sizeText.Length), out parsed) && IsFinite(parsed))
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

    void DrawDiagnosticsSection()
    {
        if (GUILayout.Button($"{( _diagnosticsFold ? "▼" : "▷")} {Tr.Get(Tr.Key.Diagnostics)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _diagnosticsFold = !_diagnosticsFold;
        if (!_diagnosticsFold) return;

        string loaderName = Loader.Instance?.GetType().Name ?? "none";
        string api = VersionSafe.IsInitialized
            ? (VersionSafe.IsV141OrLater ? "v141+" : "v136")
            : "not initialized";
        string xperfect = HitMarginCompat.HasNativeXPerfect ? "native"
            : XPerfectIntegration.IsAvailable ? "external XPerfect" : "unavailable";
        string font = AssetLoader.FontAsset != null ? AssetLoader.FontAsset.name : "fallback/missing";
        int fontCount = FontManager.FontNames?.Length ?? 0;
        var o = Overlay.Instance;
        string overlay = o == null ? "not created"
            : o.GameObject != null && o.GameObject.activeSelf
                ? $"active, seq={GameRefs.CurrentSeqID}, checkpoints={GameRefs.CheckpointsUsed}"
                : "hidden";
        string report =
            $"Loader: {loaderName}\n" +
            $"API: {api} | {HitMarginCompat.VersionReport}\n" +
            $"XPerfect: {xperfect}\n" +
            $"Font: {font} ({fontCount} loaded)\n" +
            $"Overlay: {overlay}\n" +
            $"ModPath: {Loader.ModPath}";
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label(report, GUILayout.Height(110), GUILayout.ExpandWidth(true));
        if (GUILayout.Button(_diagnosticsCopiedUntil > Time.realtimeSinceStartup
                ? Tr.Get(Tr.Key.DiagnosticsCopied) : Tr.Get(Tr.Key.CopyDiagnostics),
                GUILayout.Width(150), GUILayout.Height(24)))
        {
            GUIUtility.systemCopyBuffer = report;
            _diagnosticsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>文本模板独立分区：模板输入 + 可用 tag 一览。
    /// 刻意与「自定义标签」分开——模板不是改标签文字，而是整行重写，混在一起玩家很难发现。</summary>
    /// <summary>自定义文本行：可增删的列表，每行一个模板 + 定位方式（进栈 / 自由坐标）。
    /// 与固定槽位不同，这些行由 Overlay 在配置变化时动态创建，见 Overlay.RebuildCustomTexts。</summary>
    static int CycleValue(int current, int[] seq)
    {
        int idx = Array.IndexOf(seq, current);
        return seq[(idx + 1) % seq.Length];
    }

    static string StyleLabel(int style) => style switch
    {
        0 => Tr.Get(Tr.Key.StyleCompactRegular),
        1 => Tr.Get(Tr.Key.StyleCompactBold),
        2 => Tr.Get(Tr.Key.StyleCompactItalic),
        3 => Tr.Get(Tr.Key.StyleCompactBoldItalic),
        _ => style.ToString(),
    };

    void DrawCustomTextsSection()
    {
        if (GUILayout.Button($"{( _customTextFold ? "▼" : "▷")} {Tr.Get(Tr.Key.CustomText)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _customTextFold = !_customTextFold;
        if (!_customTextFold) return;
        GUI.changed = false;

        HelpLabel(Tr.Get(Tr.Key.CustomTextHint));

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.AddLine), GUILayout.Width(120)))
        {
            CustomTexts.Add(new CustomTextLine());
            GUI.changed = true;
        }
        GUILayout.Label($"({CustomTexts.Count})");
        GUILayout.EndHorizontal();

        int remove = -1;
        bool lookChanged = false;
        for (int i = 0; i < CustomTexts.Count; i++)
        {
            var line = CustomTexts[i];
            if (line == null) { remove = i; continue; }

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            line.Enabled = GUILayout.Toggle(line.Enabled, "", GUILayout.Width(18));
            line.ShowOutsidePlay = GUILayout.Toggle(line.ShowOutsidePlay, Tr.Get(Tr.Key.ShowOutsidePlay), GUILayout.Width(96));
            GUILayout.Label("#" + (i + 1), GUILayout.Width(30));
            // 栈位：折叠展开后点选（自由 / 主栈 / BPM / 判定 / 连击），当前项带 ●。
            string stackKey = "ctstack" + i;
            bool stackOpen = _jColorFold.TryGetValue(stackKey, out var sov) && sov;
            if (GUILayout.Button($"{(stackOpen ? "▼" : "▷")} {StackTargetLabel(line)}", GUILayout.Width(76)))
                _jColorFold[stackKey] = !stackOpen;
            string ctl = "ctpl_" + i;
            GUI.SetNextControlName(ctl);
            line.Template = GUILayout.TextField(line.Template ?? "", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", GUILayout.Width(24))) remove = i;
            GUILayout.EndHorizontal();

            if (stackOpen)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                for (int t = -1; t <= 3; t++)
                {
                    bool cur = t < 0 ? !line.InStack : line.InStack && line.StackTarget == t;
                    if (GUILayout.Button((cur ? "● " : "") + StackTargetLabel(t), GUILayout.Width(72)))
                    {
                        if (t < 0) line.InStack = false;
                        else { line.InStack = true; line.StackTarget = t; }
                        _jColorFold[stackKey] = false;
                        // GUILayout.Button 不置位 GUI.changed：不手动置位，栈位改动不会落盘。
                        GUI.changed = true;
                    }
                }
                GUILayout.EndHorizontal();
            }

            // 聚焦本行：实时求值预览 + tag 自动补全。
            if (GUI.GetNameOfFocusedControl() == ctl && !string.IsNullOrEmpty(line.Template))
            {
                try
                {
                    var pv = Overlayer.Tags.TemplateEngine.Evaluate(line.Template);
                    if (pv != null)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(44);
                        var pst = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }, wordWrap = true };
                        GUILayout.Label("→ " + pv, pst, GUILayout.ExpandWidth(true));
                        GUILayout.EndHorizontal();
                    }
                }
                catch { }
                DrawTagCompletion(ctl, line.Template, v => { line.Template = v; GUI.changed = true; });
            }

            // 第二行：字号 / 对齐 / 样式 / 独立特效；自由坐标时再多 X/Y。
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            // 字体：折叠展开后逐项点选（跟随主 / 字体表）；切换后需要材质级重刷。
            bool fontOpen = _jColorFold.TryGetValue("ctfont" + i, out var fov) && fov;
            string curFont = line.FontIndex < 0 ? Tr.Get(Tr.Key.FollowMain)
                : FontManager.FontNames != null && line.FontIndex < FontManager.FontNames.Length
                    ? FontManager.FontNames[line.FontIndex] : "?";
            if (GUILayout.Button($"{(fontOpen ? "▼" : "▷")} {Tr.Get(Tr.Key.PerLineFont)}: {curFont}", GUILayout.Width(150)))
                _jColorFold["ctfont" + i] = !fontOpen;

            GUILayout.Label("Size", GUILayout.Width(34));
            int fs = (int)FloatBox("ctsize" + i, line.FontSize, 46f);
            if (fs < 0) fs = 0;
            if (fs > 200) fs = 200;
            line.FontSize = fs;
            // 模板自带 <size=...> 时整行字号被模板接管（Size 只影响未包裹部分），
            // 提示用户去改模板里的 size 而不是以为输入框坏了。
            if (!string.IsNullOrEmpty(line.Template) && line.Template.IndexOf("<size=", System.StringComparison.OrdinalIgnoreCase) >= 0)
                GUILayout.Label(Tr.Get(Tr.Key.SizeHint), GUILayout.Width(128));

            // 对齐：主(跟随) → TL/T/TR/L/C/R/BL/B/BR 循环。
            int[] alignSeq = [-1, .. AlignValues];
            GUILayout.Label(Tr.Get(Tr.Key.PerLineAlign), GUILayout.Width(34));
            if (GUILayout.Button(line.Align < 0 ? Tr.Get(Tr.Key.FollowMain) : AlignLabel(line.Align), GUILayout.Width(40)))
            {
                line.Align = CycleValue(line.Align, alignSeq);
                lookChanged = true;
            }

            // 样式：主(跟随) → 常规 / B / I / B+I。
            int[] styleSeq = [-1, 0, 1, 2, 3];
            GUILayout.Label(Tr.Get(Tr.Key.PerLineStyle), GUILayout.Width(34));
            if (GUILayout.Button(line.Style < 0 ? Tr.Get(Tr.Key.FollowMain) : StyleLabel(line.Style), GUILayout.Width(52)))
            {
                line.Style = CycleValue(line.Style, styleSeq);
                lookChanged = true;
            }

            // 独立阴影/描边：关闭时沿用主槽位特效。
            bool fx = GUILayout.Toggle(line.EffectOverride, Tr.Get(Tr.Key.TextEffectPerSlot), GUILayout.Width(76));
            if (fx != line.EffectOverride)
            {
                line.EffectOverride = fx;
                if (fx && line.Effect == null) line.Effect = Main.Settings?.TextEffects?.Clone() ?? new TextEffectConfig();
                lookChanged = true;
            }

            // X/Y 对进栈行是「流式位置 + 偏移」、对自由行是「绝对位置」，每行都可单独调整。
            GUILayout.Label("X", GUILayout.Width(14));
            line.OffsetX = FloatBox("ctx" + i, line.OffsetX, 52f);
            GUILayout.Label("Y", GUILayout.Width(14));
            line.OffsetY = FloatBox("cty" + i, line.OffsetY, 52f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 换行：宽度 0 = 不换行（长关卡名保持单行），> 0 = 按该宽度自动换行；行距可为负。
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            GUILayout.Label("宽", GUILayout.Width(20));
            float nw = Mathf.Clamp(FloatBox("ctw" + i, line.WrapWidth, 56f), 0f, 4000f);
            if (!Mathf.Approximately(nw, line.WrapWidth)) { line.WrapWidth = nw; GUI.changed = true; }
            GUILayout.Label("行距", GUILayout.Width(34));
            float nls = Mathf.Clamp(FloatBox("ctls" + i, line.LineSpacing, 50f), -100f, 200f);
            if (!Mathf.Approximately(nls, line.LineSpacing)) { line.LineSpacing = nls; GUI.changed = true; }
            GUILayout.Label("宽 0 = 不换行", GUILayout.ExpandWidth(false));
            bool advOpen = _jColorFold.TryGetValue("ctadv" + i, out var aov) && aov;
            if (GUILayout.Button($"{(advOpen ? "▼" : "▷")} 高级", GUILayout.Width(68)))
                _jColorFold["ctadv" + i] = !advOpen;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 高级：高度基准 / 字距 / 段距 / 旋转。
            if (advOpen)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                bool ah = GUILayout.Toggle(line.AutoHeight, "自动高", GUILayout.Width(76));
                if (ah != line.AutoHeight) { line.AutoHeight = ah; GUI.changed = true; }
                GUILayout.Label("高", GUILayout.Width(20));
                float nh = Mathf.Clamp(FloatBox("cth" + i, line.Height, 50f), 1f, 2000f);
                if (!Mathf.Approximately(nh, line.Height)) { line.Height = nh; GUI.changed = true; }
                GUILayout.Label("字距", GUILayout.Width(34));
                float ncs = Mathf.Clamp(FloatBox("ctcs" + i, line.CharSpacing, 44f), -100f, 200f);
                if (!Mathf.Approximately(ncs, line.CharSpacing)) { line.CharSpacing = ncs; GUI.changed = true; }
                GUILayout.Label("段距", GUILayout.Width(34));
                float nps = Mathf.Clamp(FloatBox("ctps" + i, line.ParagraphSpacing, 44f), -100f, 200f);
                if (!Mathf.Approximately(nps, line.ParagraphSpacing)) { line.ParagraphSpacing = nps; GUI.changed = true; }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                GUILayout.Label("旋转", GUILayout.Width(34));
                float nrot = Mathf.Clamp(FloatBox("ctrot" + i, line.RotZ, 50f), -180f, 180f);
                if (!Mathf.Approximately(nrot, line.RotZ)) { line.RotZ = nrot; GUI.changed = true; }
                GUILayout.Label("度（自动高关闭时用「高」做对齐基准）", GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // 字体展开列表：跟随主 + 字体表逐行点选（展开头在本行第二行控件里）。
            if (_jColorFold.TryGetValue("ctfont" + i, out var fOpen2) && fOpen2)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                GUILayout.BeginVertical();
                if (GUILayout.Button(line.FontIndex < 0 ? "● " + Tr.Get(Tr.Key.FollowMain) : Tr.Get(Tr.Key.FollowMain), GUILayout.Width(220)))
                {
                    if (line.FontIndex != -1) { line.FontIndex = -1; lookChanged = true; }
                }
                if (FontManager.FontNames != null)
                {
                    for (int fi = 0; fi < FontManager.FontNames.Length; fi++)
                    {
                        bool sel = line.FontIndex == fi;
                        if (GUILayout.Button(sel ? "● " + FontManager.FontNames[fi] : FontManager.FontNames[fi], GUILayout.Width(220)))
                        {
                            if (line.FontIndex != fi) { line.FontIndex = fi; lookChanged = true; }
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // 每行基础色：模板里未被 <color> 包裹的部分用这个颜色（内置颜色调节工具）。
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            bool colorOpen = _jColorFold.TryGetValue("ctc" + i, out var cv) && cv;
            bool colorNow = GUILayout.Button($"{(colorOpen ? "▼" : "▷")} {Tr.Get(Tr.Key.LineColor)}", GUI.skin.label, GUILayout.Width(130));
            if (colorNow) _jColorFold["ctc" + i] = !colorOpen;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            bool colorShown = colorNow ? !colorOpen : colorOpen;
            if (colorShown)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                GUILayout.BeginVertical();
                if (line.TextColor.SettingGUI("", line.TextColor)) lookChanged = true;
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            // 效果工具：可视化拼 ColorRange / EasedValue / MovingMan 参数，一键插入模板。
            bool fxOpen = _jColorFold.TryGetValue("ctfx" + i, out var fxv) && fxv;
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            bool nfx = GUILayout.Button($"{(fxOpen ? "▼" : "▷")} {Tr.Get(Tr.Key.EffectTools)}", GUI.skin.label, GUILayout.Width(130));
            if (nfx) _jColorFold["ctfx" + i] = !fxOpen;
            GUILayout.EndHorizontal();
            bool fxShown = nfx ? !fxOpen : fxOpen;
            if (fxShown)
            {
                string k = "ct" + i + "fx.";
                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("tag", GUILayout.Width(34));
                string gtag = FxText(k + "gtag", "Progress", 120);
                GUILayout.Label("min", GUILayout.Width(30));
                double gmin = FxNum(k + "gmin", 0);
                GUILayout.Label("max", GUILayout.Width(32));
                double gmax = FxNum(k + "gmax", 100);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("from", GUILayout.Width(34));
                string gfrom = FxText(k + "gfrom", "FF0000", 70);
                GUILayout.Label("to", GUILayout.Width(24));
                string gto = FxText(k + "gto", "00FF00", 70);
                GUILayout.Label("ease", GUILayout.Width(38));
                string gease = FxText(k + "gease", "OutExpo", 90);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("atMax", GUILayout.Width(40));
                string gat = FxText(k + "gat", "", 70);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {ColorRange}", GUILayout.ExpandWidth(false)))
                {
                    line.Template = AppendTag(line.Template, "{" + GradExpr(gtag, gmin, gmax, gfrom, gto, gease, gat) + "}");
                    GUI.changed = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("→", GUILayout.Width(14));
                string pv = Overlayer.Tags.GameTags.ColorRange(gtag, gmin, gmax, gfrom, gto, gease, -1, gat.Length == 0 ? null : gat);
                GUILayout.Label(pv.Length == 0 ? "(tag 无效)" : "#" + pv, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("tag", GUILayout.Width(34));
                string etag = FxText(k + "etag", "Combo", 120);
                GUILayout.Label("速度ms", GUILayout.Width(52));
                double espeed = FxNum(k + "espeed", 300);
                GUILayout.Label("ease", GUILayout.Width(38));
                string eease = FxText(k + "eease", "OutExpo", 90);
                if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {EasedValue}", GUILayout.ExpandWidth(false)))
                {
                    line.Template = AppendTag(line.Template, "{EasedValue(" + etag + ",2," + FxN(espeed) + "," + eease + ")}");
                    GUI.changed = true;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                GUILayout.Label("tag", GUILayout.Width(34));
                string mtag = FxText(k + "mtag", "Combo", 120);
                GUILayout.Label("start", GUILayout.Width(38));
                double mstart = FxNum(k + "mstart", 140);
                GUILayout.Label("end", GUILayout.Width(32));
                double mend = FxNum(k + "mend", 108);
                GUILayout.Label("ms", GUILayout.Width(28));
                double mspeed = FxNum(k + "mspeed", 300);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(56);
                if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {MovingMan}", GUILayout.ExpandWidth(false)))
                {
                    line.Template = AppendTag(line.Template, "{MovingMan(" + mtag + "," + FxN(mstart) + "," + FxN(mend) + "," + FxN(mend) + "," + FxN(mspeed) + ")}");
                    GUI.changed = true;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (line.EffectOverride)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(44);
                GUILayout.BeginVertical();
                // 描边/阴影是材质级效果，必须把 changed 接到 lookChanged 才能触发
                // ShadowManager.ClearCache() + ApplyFontToAll() 重刷材质；丢掉返回值
                // 就只重求值文本、不刷材质，表现为「开关亮着但描边不出现」。
                if (DrawEffectControls(line.Effect, 0)) lookChanged = true;
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }
        if (remove >= 0)
        {
            CustomTexts.RemoveAt(remove);
            GUI.changed = true;
        }

        if (GUI.changed)
        {
            LabelsDirty = true;
            var o = Overlay.Instance;
            if (o != null)
            {
                o.MaybeRebuildCustomTexts();
                if (o.GameObject.activeSelf)
                {
                    // 外观字段（对齐/样式/特效/颜色）变化才做材质级重刷；
                    // 模板文字变化只需重求值——打字路径不重建材质，不卡顿。
                    if (lookChanged)
                    {
                        ShadowManager.ClearCache();
                        o.ApplyFontToAll();
                    }
                    o.RefreshVisibility();
                    o.RefreshCustomTexts();
                    LabelsDirty = false;
                }
            }
        }
    }

    /// <summary>效果工具的文本框（带缓冲，键独立于滑条）。</summary>
    string FxText(string key, string fallback, float width)
    {
        if (!_slideFields.TryGetValue(key, out var text)) _slideFields[key] = text = fallback;
        string nv = GUILayout.TextField(text, GUILayout.Width(width));
        if (nv != text) { _slideFields[key] = nv; return nv; }
        return text;
    }

    double FxNum(string key, double fallback)
    {
        if (!_slideFields.TryGetValue(key, out var text)) _slideFields[key] = text = fallback.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        string nv = GUILayout.TextField(text, GUILayout.Width(52));
        if (nv != text)
        {
            _slideFields[key] = nv;
            text = nv;
        }
        // 稳定帧也返回存储文本的解析值：此前未变帧返回 fallback，输入后预览与插入
        // 会退回默认数值（如 min 填 50 但表达式仍拼 0）。
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) && !float.IsNaN((float)v) && !float.IsInfinity((float)v) ? v : fallback;
    }

    static string FxN(double v)
    {
        return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    static string AppendTag(string template, string tag)
    {
        return string.IsNullOrEmpty(template) ? tag : template + " " + tag;
    }

    /// <summary>生成 ColorRange 表达式（atMax 留空则不带顶格参数）。</summary>
    static string GradExpr(string tag, double min, double max, string from, string to, string ease, string atMax)
    {
        string s = "ColorRange(" + tag + "," + FxN(min) + "," + FxN(max) + "," + from + "," + to;
        if (!string.IsNullOrEmpty(ease)) s += "," + ease;
        if (!string.IsNullOrEmpty(atMax)) s += ",,," + atMax;
        return s + ")";
    }

    // ===== 效果工具面板（渐变 / 动画 / 缓动 三个专属分页）=====

    private int _sfxTab;

    // UMM 暗色皮肤的 box/button 背景纹理接近纯黑，GUI.color 是乘法染色——黑底乘任何色仍是黑。
    // 所有色块一律用纯白纹理打底（染色后即准确颜色）。
    private static GUIStyle _sfxSwatchStyle;

    private static GUIStyle SfxSwatchStyle => _sfxSwatchStyle ??= new GUIStyle(GUI.skin.button)
    {
        normal = { background = Texture2D.whiteTexture },
        hover = { background = Texture2D.whiteTexture },
        active = { background = Texture2D.whiteTexture },
        focused = { background = Texture2D.whiteTexture },
    };

    /// <summary>画一块实色矩形（白纹理 × GUI.color）。</summary>
    private static void SfxColorRect(Color c, float w, float h)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h)), Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = prev;
    }

    /// <summary>hex → Color（容错：非法输入显示中灰色，不影响运行）。</summary>
    private static Color SfxHexColor(string hex)
    {
        try { return ColorUtility.TryParseHtmlString("#" + hex.Trim().TrimStart('#'), out var c) ? c : new Color(0.45f, 0.45f, 0.45f); }
        catch { return new Color(0.45f, 0.45f, 0.45f); }
    }

    private string SfxVal(string key, string fallback) => _slideFields.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>hex 颜色输入：左色块实时预览 + 右 hex 输入框（键控持久）。</summary>
    private void SfxColorField(string key, string label, string fallback)
    {
        if (!_slideFields.TryGetValue(key, out var hex)) _slideFields[key] = hex = fallback;
        GUILayout.Label(label, GUILayout.Width(44));
        SfxColorRect(SfxHexColor(hex), 24, 19);
        string nv = GUILayout.TextField(hex, GUILayout.Width(86));
        if (nv != hex) _slideFields[key] = nv;
    }

    /// <summary>常用色快捷色板，点击即写入该 hex 键。</summary>
    private void SfxSwatchRow(string key)
    {
        string[] presets = { "FF0000", "FF8000", "FFFF00", "00FF00", "00FFCC", "0080FF", "8000FF", "FF00FF", "FFFFFF", "111111" };
        GUILayout.BeginHorizontal();
        GUILayout.Space(44);
        foreach (var p in presets)
        {
            var prev = GUI.color;
            GUI.color = SfxHexColor(p);
            if (GUILayout.Button(GUIContent.none, SfxSwatchStyle, GUILayout.Width(28), GUILayout.Height(16)))
            {
                _slideFields[key] = p;
                GUI.changed = true;
            }
            GUI.color = prev;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    /// <summary>缓动选择器：当前名按钮 + 展开网格（Ease 全部 31 个枚举名，6 列）。返回选中名。</summary>
    private string SfxEasePicker(string key, string fallback)
    {
        if (!_slideFields.TryGetValue(key, out var cur)) _slideFields[key] = cur = fallback;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label("ease", GUILayout.Width(38));
        bool open = _jColorFold.TryGetValue(key + ".open", out var ov) && ov;
        if (GUILayout.Button(cur, GUILayout.Width(110)))
        {
            _jColorFold[key + ".open"] = !open;
            GUI.changed = true;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (open)
        {
            var names = Enum.GetNames(typeof(Overlayer.Tags.GameTags.Ease));
            int inRow = 0;
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button(names[i], cur == names[i] ? GUI.skin.box : GUI.skin.button, GUILayout.Width(84), GUILayout.Height(19)))
                {
                    _slideFields[key] = names[i];
                    _jColorFold[key + ".open"] = false;
                    GUI.changed = true;
                }
                if (++inRow >= 6 && i < names.Length - 1)
                {
                    inRow = 0;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(44);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        return _slideFields[key];
    }

    /// <summary>渐变页：tag + 区间 + 双色/顶格色（色块+hex+色板）+ 缓动 + 渐变条预览 + 插入。</summary>
    private void DrawSfxGradientTab()
    {
        string k = "sfx.g.";
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label("tag", GUILayout.Width(38));
        string tag = FxText(k + "tag", "Progress", 150);
        GUILayout.Label("min", GUILayout.Width(34));
        double min = FxNum(k + "min", 0);
        GUILayout.Label("max", GUILayout.Width(36));
        double max = FxNum(k + "max", 100);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        SfxColorField(k + "from", "from", "FF0000");
        SfxColorField(k + "to", "to", "00FF00");
        bool useAt = _jColorFold.TryGetValue(k + "at", out var av) && av;
        bool nAt = GUILayout.Button($"{(useAt ? "▼" : "▷")} atMax", GUI.skin.label, GUILayout.Width(80));
        if (nAt) _jColorFold[k + "at"] = !useAt;
        if (nAt ? !useAt : useAt) SfxColorField(k + "atc", "color", "FFD700");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        SfxSwatchRow(k + "from");
        string from = SfxVal(k + "from", "FF0000");
        string to = SfxVal(k + "to", "00FF00");
        string ease = SfxEasePicker(k + "ease", "OutExpo");
        string atHex = useAt ? SfxVal(k + "atc", "FFD700") : "";

        // 渐变条预览：与运行时 ColorRange 用同一缓动库逐段采样。
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        var c0 = SfxHexColor(from);
        var c1 = SfxHexColor(to);
        for (int i = 0; i < 20; i++)
            SfxColorRect(Color.Lerp(c0, c1, Overlayer.Tags.GameTags.SampleEase(ease, i / 19f)), 42, 16);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        string expr = GradExpr(tag, min, max, from, to, ease, atHex);
        string pv = Overlayer.Tags.GameTags.ColorRange(tag, min, max, from, to, ease, -1, atHex.Length == 0 ? null : atHex);
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {ColorRange}", GUILayout.ExpandWidth(false)))
        {
            GUIUtility.systemCopyBuffer = "{" + expr + "}";
            _lastCopiedTag = tag;
            _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
        }
        GUILayout.Label(pv.Length == 0 ? "-> (tag invalid)" : "-> " + pv + "  (current value)", GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
    }

    /// <summary>动画页：MovingMan 参数 + invert/ease + 实时求值预览 + 插入。</summary>
    private void DrawSfxAnimTab()
    {
        string k = "sfx.a.";
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label("tag", GUILayout.Width(38));
        string tag = FxText(k + "tag", "Combo", 140);
        GUILayout.Label("start", GUILayout.Width(40));
        double start = FxNum(k + "start", 140);
        GUILayout.Label("end", GUILayout.Width(34));
        double end = FxNum(k + "end", 108);
        GUILayout.Label("hold", GUILayout.Width(38));
        double hold = FxNum(k + "hold", 108);
        GUILayout.Label("speed", GUILayout.Width(42));
        double speed = FxNum(k + "speed", 300);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        bool invert = _jColorFold.TryGetValue(k + "inv", out var iv) && iv;
        bool nInv = GUILayout.Toggle(invert, "invert", GUILayout.Width(66));
        if (nInv != invert) _jColorFold[k + "inv"] = nInv;
        GUILayout.EndHorizontal();
        string ease = SfxEasePicker(k + "ease", "Linear");

        string expr = "MovingMan(" + tag + "," + FxN(start) + "," + FxN(end) + "," + FxN(hold) + "," + FxN(speed);
        if (ease != "Linear" || invert) expr += invert ? ",true" : ",false";
        if (ease != "Linear") expr += "," + ease;
        expr += ")";
        string pv;
        try { pv = Overlayer.Tags.TemplateEngine.Evaluate("{" + expr + "}") ?? ""; } catch { pv = ""; }

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {MovingMan}", GUILayout.ExpandWidth(false)))
        {
            GUIUtility.systemCopyBuffer = "{" + expr + "}";
            _lastCopiedTag = tag;
            _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
        }
        GUILayout.Label(pv.Length == 0 ? "-> (no value)" : "-> " + pv, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
    }

    /// <summary>缓动页：EasedValue 参数 + ease + 实时求值预览 + 插入。</summary>
    private void DrawSfxEaseTab()
    {
        string k = "sfx.e.";
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label("tag", GUILayout.Width(38));
        string tag = FxText(k + "tag", "Progress", 150);
        GUILayout.Label("digits", GUILayout.Width(44));
        double digits = FxNum(k + "digits", 2);
        GUILayout.Label("speed", GUILayout.Width(42));
        double speed = FxNum(k + "speed", 300);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        string ease = SfxEasePicker(k + "ease", "OutExpo");

        string expr = "EasedValue(" + tag + "," + FxN(digits) + "," + FxN(speed);
        if (ease != "Linear") expr += "," + ease;
        expr += ")";
        string pv;
        try { pv = Overlayer.Tags.TemplateEngine.Evaluate("{" + expr + "}") ?? ""; } catch { pv = ""; }

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.Insert) + " {EasedValue}", GUILayout.ExpandWidth(false)))
        {
            GUIUtility.systemCopyBuffer = "{" + expr + "}";
            _lastCopiedTag = tag;
            _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
        }
        GUILayout.Label(pv.Length == 0 ? "-> (no value)" : "-> " + pv, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
    }

    private bool _customTagsFold;

    /// <summary>自定义组合 tag：名称 → 模板片段。注册后任何模板都能用 {名称} 引用，
    /// 这是「不写代码自己造 tag」的入口。名称非法/与内置重名会在日志告警并跳过。</summary>
    void DrawCustomTagsSection()
    {
        if (GUILayout.Button($"{( _customTagsFold ? "▼" : "▷")} {Tr.Get(Tr.Key.CustomTags)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _customTagsFold = !_customTagsFold;
        if (!_customTagsFold) return;
        GUI.changed = false;

        HelpLabel(Tr.Get(Tr.Key.CustomTagHint));

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.AddTag), GUILayout.Width(120)))
        {
            CustomTags.Add(new CustomTagDef());
            GUI.changed = true;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        int remove = -1;
        for (int i = 0; i < CustomTags.Count; i++)
        {
            var def = CustomTags[i];
            if (def == null) { remove = i; continue; }

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("#" + (i + 1), GUILayout.Width(30));
            GUILayout.Label("tag", GUILayout.Width(34));
            def.Name = GUILayout.TextField(def.Name ?? "", GUILayout.Width(120));
            GUILayout.Label("=", GUILayout.Width(14));
            def.Template = GUILayout.TextField(def.Template ?? "", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", GUILayout.Width(24))) remove = i;
            GUILayout.EndHorizontal();
        }
        if (remove >= 0)
        {
            CustomTags.RemoveAt(remove);
            GUI.changed = true;
        }

        if (GUI.changed)
        {
            // 名称/模板变更即时注册（非法名与内置重名在日志告警并跳过）。
            Overlayer.Tags.TagManager.SetCustomTags(CustomTags);
            GUI.changed = true;
            var o = Overlay.Instance;
            if (o != null && o.GameObject.activeSelf)
            {
                o.RefreshAllTexts();
            }
        }
    }

    /// <summary>带缓冲的数值输入框：聚焦期间保留中间态（如 "-"、"1."），失焦后才规范化回当前值。
    /// 之前每帧都按当前值重写缓冲，逐字符输入 "-5" 时 "-" 会在下一帧被 0 覆盖，负数打不进去。</summary>
    float FloatBox(string key, float value, float width)
    {
        if (!_slideFields.TryGetValue(key, out var text))
            _slideFields[key] = text = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        string ctl = "fb_" + key;
        GUI.SetNextControlName(ctl);
        string entered = GUILayout.TextField(text, GUILayout.Width(width));
        if (entered != text)
        {
            _slideFields[key] = entered;
            return ParseBuffered(entered, value);
        }
        // 聚焦中：用户可能正打到一半（"-"、"1." 都还不是最终值），保留缓冲不动。
        if (GUI.GetNameOfFocusedControl() == ctl) return ParseBuffered(text, value);
        string formatted = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        if (formatted != text) _slideFields[key] = formatted;
        return value;
    }

    /// <summary>缓冲文本 → 数值；解析失败或非有限值回退当前值（与格式化同为 InvariantCulture）。</summary>
    static float ParseBuffered(string text, float fallback)
        => float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
           && !float.IsNaN(parsed) && !float.IsInfinity(parsed)
            ? parsed
            : fallback;

    void DrawTemplatesSection()
    {
        if (GUILayout.Button($"{( _templateFold ? "▼" : "▷")} {Tr.Get(Tr.Key.Templates)}", GUI.skin.label, GUILayout.ExpandWidth(true)))
            _templateFold = !_templateFold;
        if (!_templateFold) return;
        GUI.changed = false;

        HelpLabel(Tr.Get(Tr.Key.TemplateHint));

        // 示例模板：点击复制整段到剪贴板；悬停 Tooltip 显示完整模板内容。
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        GUILayout.Label(Tr.Get(Tr.Key.TemplateExamples), GUILayout.Width(44));
        foreach (var ex in Overlayer.Tags.TagHelp.Examples)
        {
            if (GUILayout.Button(new GUIContent(ex.Title, ex.Template), GUILayout.ExpandWidth(false)))
            {
                GUIUtility.systemCopyBuffer = ex.Template;
                _lastCopiedTag = ex.Title;
                _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        if (GUILayout.Button(Tr.Get(Tr.Key.CopyTagList), GUILayout.Width(150)))
        {
            GUIUtility.systemCopyBuffer = string.Join(" ", Overlayer.Tags.TagManager.GetTagNames());
            _lastCopiedTag = null;
            _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
        }
        if (GUILayout.Button(Tr.Get(Tr.Key.ResetTemplates), GUILayout.Width(110)))
        {
            Labels.ResetTemplates();
            GUI.changed = true;
        }
        // 把内置渲染逐槽位导出为等价模板并填满全部模板字段：
        // 从「和现在一模一样」的起点开始改，而不是从空白摸索。覆盖现有模板，重置按钮可清空。
        if (GUILayout.Button(Tr.Get(Tr.Key.ImportBuiltin), GUILayout.Width(130)))
        {
            Overlayer.Tags.BuiltinTemplateGenerator.FillAll(this);
            Labels.Save();
            GUI.changed = true;
        }
        if (GUILayout.Button((_tagHelpOpen ? "▼ " : "▷ ") + Tr.Get(Tr.Key.TagList), GUILayout.ExpandWidth(false)))
            _tagHelpOpen = !_tagHelpOpen;
        GUILayout.FlexibleSpace();
        if (_tagsCopiedUntil > Time.realtimeSinceStartup)
            GUILayout.Label(string.IsNullOrEmpty(_lastCopiedTag)
                ? Tr.Get(Tr.Key.TagListCopied)
                : Tr.Get(Tr.Key.TagCopied) + " {" + _lastCopiedTag + "}");
        GUILayout.EndHorizontal();

        if (_tagHelpOpen)
        {
            HelpLabel(Tr.Get(Tr.Key.TagListHint));

            // 搜索框：按 tag 名或说明过滤（忽略大小写）；117 个 tag 逐个翻太低效。
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(Tr.Get(Tr.Key.TagSearch), GUILayout.Width(60));
            _tagSearch = GUILayout.TextField(_tagSearch ?? "", GUILayout.Width(220));
            if (GUILayout.Button("✕", GUILayout.Width(24)) && !string.IsNullOrEmpty(_tagSearch)) { _tagSearch = ""; GUI.changed = true; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            string filter = (_tagSearch ?? "").Trim();
            bool filtering = filter.Length > 0;
            int shown = 0, total = 0;
            _tagHelpScroll = GUILayout.BeginScrollView(_tagHelpScroll, GUILayout.Height(260));
            foreach (var group in Overlayer.Tags.TagHelp.Groups)
            {
                bool headerDrawn = false;
                foreach (var item in group.Items)
                {
                    total++;
                    if (filtering
                        && (item.Name == null || item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        && (item.Desc == null || item.Desc.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;
                    if (!headerDrawn)
                    {
                        GUILayout.Label("  -- " + group.Title + " --");
                        headerDrawn = true;
                    }
                    shown++;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    if (GUILayout.Button("{" + item.Name + "}", GUI.skin.label, GUILayout.Width(170)))
                    {
                        GUIUtility.systemCopyBuffer = "{" + item.Name + "}";
                        _lastCopiedTag = item.Name;
                        _tagsCopiedUntil = Time.realtimeSinceStartup + 1.5f;
                    }
                    GUILayout.Label(item.Desc, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            if (filtering)
                GUILayout.Label("  " + shown + " / " + total);
        }

        // 共享效果工具：渐变 / 动画 / 缓动三个专属分页（对齐参考工程 ColorRange/MovingMan
        // 专属设置的思路）——每页可视化控件 + 实时预览，拼好参数一键插入任意模板字段。
        bool sfxOpen = _jColorFold.TryGetValue("stfx", out var sfxv) && sfxv;
        GUILayout.BeginHorizontal();
        GUILayout.Space(16);
        bool nsfx = GUILayout.Button($"{(sfxOpen ? "▼" : "▷")} {Tr.Get(Tr.Key.EffectTools)}", GUI.skin.label, GUILayout.Width(130));
        if (nsfx) _jColorFold["stfx"] = !sfxOpen;
        GUILayout.FlexibleSpace();
        if (_tagsCopiedUntil > Time.realtimeSinceStartup)
            GUILayout.Label(string.IsNullOrEmpty(_lastCopiedTag)
                ? Tr.Get(Tr.Key.TagListCopied)
                : Tr.Get(Tr.Key.TagCopied) + " {" + _lastCopiedTag + "}");
        GUILayout.EndHorizontal();
        // 按钮返回值只代表「点击帧」；展开态以翻转后的状态为准。
        bool sfxShown = nsfx ? !sfxOpen : sfxOpen;
        if (sfxShown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            int ntab = GUILayout.Toolbar(_sfxTab, new[]
            {
                Tr.Get(Tr.Key.SfxGradient), Tr.Get(Tr.Key.SfxAnim), Tr.Get(Tr.Key.SfxEase)
            }, GUILayout.Height(22));
            if (ntab != _sfxTab) { _sfxTab = ntab; GUI.changed = true; }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            if (_sfxTab == 0) DrawSfxGradientTab();
            else if (_sfxTab == 1) DrawSfxAnimTab();
            else DrawSfxEaseTab();
        }

        GUILayout.Space(4);
        GUILayout.Label("  -- Standard --");
            DrawTemplateField("Progress", ref Labels.ProgressTemplate);
            DrawTemplateField("Accuracy", ref Labels.AccuracyTemplate);
            DrawTemplateField("XAccuracy", ref Labels.XAccuracyTemplate);
            DrawTemplateField("XScore", ref Labels.XScoreTemplate);
            DrawTemplateField("P.Accuracy", ref Labels.PotAccuracyTemplate);
            DrawTemplateField("P.XAccuracy", ref Labels.PotXAccuracyTemplate);
            DrawTemplateField("P.XScore", ref Labels.PotXScoreTemplate);
            DrawTemplateField("Music Time", ref Labels.MusicTimeTemplate);
            DrawTemplateField("Map Time", ref Labels.MapTimeTemplate);
            DrawTemplateField("CheckPoint", ref Labels.CheckpointTemplate);
            DrawTemplateField("Best", ref Labels.BestTemplate);
            DrawTemplateField("TBPM", ref Labels.TbpmTemplate);
            DrawTemplateField("CBPM", ref Labels.CbpmTemplate);
            DrawTemplateField("KPS", ref Labels.KpsTemplate);
            DrawTemplateField("Attempt", ref Labels.AttemptTemplate);
            DrawTemplateField("Full Attempt", ref Labels.FullAttemptTemplate);
            DrawTemplateField("Timing Scale", ref Labels.TimingScaleTemplate);
            DrawTemplateField("Judgement", ref Labels.JudgementTemplate);
            DrawTemplateField("Combo Value", ref Labels.ComboTemplate);
            DrawTemplateField("Timing Window", ref Labels.TimingWindowTemplate);

            GUILayout.Label("  -- ExtendedOverlay --");
            DrawTemplateField("FPS", ref Labels.FpsTemplate);
            DrawTemplateField("Author", ref Labels.AuthorTemplate);
            DrawTemplateField("State", ref Labels.StateTemplate);
            DrawTemplateField("Death", ref Labels.DeathTemplate);
            DrawTemplateField("Start", ref Labels.StartTemplate);
            DrawTemplateField("Timing", ref Labels.TimingTemplate);
            DrawTemplateField("Avg Timing", ref Labels.AvgTimingTemplate);
            DrawTemplateField("Hit Offset", ref Labels.HitOffsetTemplate);

        if (GUI.changed)
        {
            LabelsDirty = true;
            var o = Overlay.Instance;
            if (o != null && o.GameObject.activeSelf)
            {
                o.RefreshAllTexts();
                LabelsDirty = false;
            }
        }
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

        HelpLabel(Tr.Get(Tr.Key.SeparatorHint));

        GUILayout.Label("  -- Standard --");
        DrawLabelField("Progress", ref Labels.Progress, ref Labels.ProgressSeparator);
        DrawLabelField("Accuracy", ref Labels.Accuracy, ref Labels.AccuracySeparator);
        DrawLabelField("XAccuracy", ref Labels.XAccuracy, ref Labels.XAccuracySeparator);
        DrawLabelField("Music Time", ref Labels.MusicTime, ref Labels.MusicTimeSeparator);
        DrawLabelField("Map Time", ref Labels.MapTime, ref Labels.MapTimeSeparator);
        DrawLabelField("CheckPoint", ref Labels.Checkpoint, ref Labels.CheckpointSeparator);
        DrawLabelField("Best", ref Labels.Best, ref Labels.BestSeparator);
        DrawLabelField("TBPM", ref Labels.TBPM, ref Labels.TBPMSeparator);
        DrawLabelField("CBPM", ref Labels.CBPM, ref Labels.CBPMSeparator);
        DrawLabelField("KPS", ref Labels.KPS, ref Labels.KPSSeparator);
        DrawLabelField("Attempt", ref Labels.Attempt);
        DrawLabelField("Full Attempt", ref Labels.FullAttempt);
        DrawLabelField("Timing Scale", ref Labels.TimingScale, ref Labels.TimingScaleSeparator);
        DrawLabelField("XPerfect", ref Labels.XPerfectLabel);
        DrawLabelField("Perfect", ref Labels.PerfectLabel);
        DrawLabelField("Great", ref Labels.GreatLabel);
        DrawLabelField("Good", ref Labels.GoodLabel);
        DrawLabelField("Combo Title", ref Labels.ComboTitle);
        DrawLabelField("Combo Title Alt", ref Labels.ComboTitleAlt);

        GUILayout.Space(3);
        GUILayout.Label("  -- ExtendedOverlay --");
        DrawLabelField("FPS", ref Labels.FPS, ref Labels.FPSSeparator);
        DrawLabelField("Author", ref Labels.Author, ref Labels.AuthorSeparator);
        DrawLabelField("State", ref Labels.State, ref Labels.StateSeparator);
        DrawLabelField("Death", ref Labels.Death, ref Labels.DeathSeparator);
        DrawLabelField("Start", ref Labels.Start, ref Labels.StartSeparator);
        DrawLabelField("Timing", ref Labels.Timing, ref Labels.TimingSeparator);
        DrawLabelField("Hit Offset", ref Labels.HitOffset, ref Labels.HitOffsetSeparator);

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

        GUILayout.Space(3);
        GUILayout.Label("  -- Separator only --");
        DrawSeparatorField("XScore", ref Labels.XScoreSeparator);
        DrawSeparatorField("Timing Window", ref Labels.TimingWindowSeparator);

        if (GUI.changed)
        {
            LabelsDirty = true;
            var o = Overlay.Instance;
            if (o != null && o.GameObject.activeSelf)
            {
                o.RefreshAllTexts();
                LabelsDirty = false;
            }
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

    /// <summary>带分隔符输入框的标签行：右侧窄框 = 该项标签与数值之间的分隔符（见 SeparatorHint）。</summary>
    static void DrawLabelField(string label, ref string value, ref string separator)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(label, GUILayout.Width(100));
        value = GUILayout.TextField(value, GUILayout.ExpandWidth(true));
        separator = GUILayout.TextField(separator ?? "", GUILayout.Width(64));
        GUILayout.EndHorizontal();
    }

    /// <summary>没有可编辑标签行的项（XScore/判定时间窗）只显示分隔符输入框。</summary>
    /// <summary>模板输入行（空 = 内置渲染）。聚焦时在下方实时显示求值预览。</summary>
    static void DrawTemplateField(string label, ref string template)
    {
        string ctl = "tpl_" + label;
        string current = template;
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(label, GUILayout.Width(110));
        GUI.SetNextControlName(ctl);
        current = GUILayout.TextField(current ?? "", GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        DrawTemplatePreview(ctl, current);
        DrawTagCompletion(ctl, current, v => current = v);
        template = current;
    }

    /// <summary>聚焦中的模板字段下方显示实时求值预览（每帧只求值聚焦的那一个，开销可控）。</summary>
    static void DrawTemplatePreview(string ctl, string template)
    {
        if (GUI.GetNameOfFocusedControl() != ctl) return;
        if (string.IsNullOrEmpty(template)) return;
        try
        {
            var v = Overlayer.Tags.TemplateEngine.Evaluate(template);
            if (v == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Space(44);
            var st = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }, wordWrap = true };
            GUILayout.Label("→ " + v, st, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
        catch { }
    }

    /// <summary>tag 自动补全：光标尾部有未闭合的 {片段 时，显示匹配的 tag 按钮，
    /// 点击替换片段并恢复输入焦点（参考引擎 TagCompletionPopup 的轻量 IMGUI 版）。</summary>
    static void DrawTagCompletion(string ctl, string template, Action<string> setValue)
    {
        if (GUI.GetNameOfFocusedControl() != ctl) return;
        int lastOpen = template != null ? template.LastIndexOf('{') : -1;
        if (lastOpen < 0) return;
        if (template.IndexOf('}', lastOpen) >= 0) return; // 已闭合 → 不补全
        string frag = template.Substring(lastOpen + 1);
        if (frag.Length == 0 || frag.IndexOf('(') >= 0) return; // 已进入参数区 → 不补全
        var names = Overlayer.Tags.TagManager.GetTagNames();
        var matches = new List<string>();
        foreach (var n in names)
        {
            if (n.StartsWith(frag, StringComparison.OrdinalIgnoreCase) && n != frag) matches.Add(n);
            if (matches.Count >= 6) break;
        }
        if (matches.Count == 0) return;
        GUILayout.BeginHorizontal();
        GUILayout.Space(44);
        for (int i = 0; i < matches.Count; i++)
        {
            if (GUILayout.Button("{" + matches[i] + "}", GUI.skin.label, GUILayout.Width(36f + matches[i].Length * 6.5f)))
            {
                setValue(template.Substring(0, lastOpen + 1) + matches[i] + "}");
                GUI.changed = true;
            }
            if (i < matches.Count - 1) GUILayout.Space(6);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    static void DrawSeparatorField(string label, ref string separator)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(label, GUILayout.Width(100));
        separator = GUILayout.TextField(separator ?? "", GUILayout.Width(64));
        GUILayout.FlexibleSpace();
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
    /// <summary>对齐方式的三语短标签；顺序与 AlignValues 一一对应。</summary>
    static readonly Tr.Key[] AlignKeys =
    [
        Tr.Key.AlignTL, Tr.Key.AlignT, Tr.Key.AlignTR,
        Tr.Key.AlignL, Tr.Key.AlignC, Tr.Key.AlignR,
        Tr.Key.AlignBL, Tr.Key.AlignB, Tr.Key.AlignBR,
    ];

    static string[] AlignLabels()
    {
        var labels = new string[AlignKeys.Length];
        for (int i = 0; i < labels.Length; i++) labels[i] = Tr.Get(AlignKeys[i]);
        return labels;
    }

    /// <summary>栈位名：-1 = 自由，0 = 主栈，1 = BPM，2 = 判定，3 = 连击。</summary>
    static string StackTargetLabel(int t) => t switch
    {
        0 => Tr.Get(Tr.Key.StackMain),
        1 => Tr.Get(Tr.Key.StackBpm),
        2 => Tr.Get(Tr.Key.StackJudge),
        3 => Tr.Get(Tr.Key.StackCombo),
        _ => Tr.Get(Tr.Key.FreeLayout),
    };

    static string StackTargetLabel(CustomTextLine line)
        => line == null || !line.InStack ? Tr.Get(Tr.Key.FreeLayout) : StackTargetLabel(line.StackTarget);

    static string AlignLabel(int v)
    {
        int idx = Array.IndexOf(AlignValues, v);
        return idx >= 0 ? Tr.Get(AlignKeys[idx]) : Tr.Get(Tr.Key.AlignC);
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
        int newIdx = GUILayout.SelectionGrid(idx, AlignLabels(), 3, GUILayout.Height(60));
        GUILayout.EndHorizontal();
        if (newIdx != idx) { align = AlignValues[newIdx]; Overlayer.Overlay.Instance?.ApplyAlignment(); }
    }

    static float ClampFinite(float value, float fallback, float min, float max)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    static bool NormalizeLoadedValues(Settings s)
    {
        bool changed = false;
        void Check(ref float field, float fallback, float min, float max)
        {
            float next = ClampFinite(field, fallback, min, max);
            if (Math.Abs(next - field) > 0.0001f) { field = next; changed = true; }
        }
        void CheckInt(ref int field, int fallback, int min, int max)
        {
            int next = Math.Min(max, Math.Max(min, field == int.MinValue ? fallback : field));
            if (next != field) { field = next; changed = true; }
        }
        void CheckAlign(ref int field)
        {
            if (Array.IndexOf(AlignValues, field) < 0) { field = 514; changed = true; }
        }

        Check(ref s.Size, 1f, 0f, 3f);
        Check(ref s.BpmColorMax, 8000f, 1f, 100000f);
        CheckInt(ref s.ComboColorMax, 1000, 1, 5000);
        Check(ref s.FPSRefreshRate, 0.2f, 0.05f, 1f);
        CheckInt(ref s.ExtendedDecimalPrecision, 2, 0, 5);
        CheckInt(ref s.ProgressDecimal, 2, 0, 4);
        CheckInt(ref s.AccuracyDecimal, 2, 0, 4);
        CheckInt(ref s.XAccuracyDecimal, 2, 0, 4);
        CheckInt(ref s.BestDecimal, 2, 0, 4);
        CheckInt(ref s.TimingDecimal, 5, 0, 5);
        CheckInt(ref s.HitOffsetDecimal, 5, 0, 5);
        CheckInt(ref s.MainFontSize, 25, 8, 200);
        CheckInt(ref s.BPMFontSize, 25, 8, 200);
        CheckInt(ref s.JudgeFontSize, 25, 8, 200);
        CheckInt(ref s.ComboTitleFontSize, 40, 8, 200);
        CheckInt(ref s.ComboValFontSize, 108, 8, 200);
        CheckInt(ref s.TimingFontSize, 20, 8, 200);
        CheckInt(ref s.AttemptFontSize, 25, 8, 200);
        void CheckFinite(ref float field)
        {
            if (float.IsNaN(field) || float.IsInfinity(field)) { field = 0; changed = true; }
        }
        CheckFinite(ref s.MainOffsetX); CheckFinite(ref s.MainOffsetY);
        CheckFinite(ref s.BPMOffsetX); CheckFinite(ref s.BPMOffsetY);
        CheckFinite(ref s.JudgeOffsetX); CheckFinite(ref s.JudgeOffsetY);
        CheckFinite(ref s.P1JudgeOffsetX); CheckFinite(ref s.P1JudgeOffsetY);
        CheckFinite(ref s.P2JudgeOffsetX); CheckFinite(ref s.P2JudgeOffsetY);
        CheckFinite(ref s.P3JudgeOffsetX); CheckFinite(ref s.P3JudgeOffsetY);
        CheckFinite(ref s.P4JudgeOffsetX); CheckFinite(ref s.P4JudgeOffsetY);
        CheckFinite(ref s.ComboOffsetX); CheckFinite(ref s.ComboOffsetY);
        CheckFinite(ref s.TimingOffsetX); CheckFinite(ref s.TimingOffsetY);
        CheckFinite(ref s.AttemptOffsetX); CheckFinite(ref s.AttemptOffsetY);
        CheckFinite(ref s.AttemptCoopOffsetX); CheckFinite(ref s.AttemptCoopOffsetY);
        CheckFinite(ref s.ProgBarOffsetX); CheckFinite(ref s.ProgBarOffsetY);
        CheckFinite(ref s.HitOffsetX); CheckFinite(ref s.HitOffsetY);
        CheckAlign(ref s.MainAlign); CheckAlign(ref s.BPMAlign); CheckAlign(ref s.JudgeAlign);
        CheckAlign(ref s.ComboAlign); CheckAlign(ref s.ComboValAlign); CheckAlign(ref s.TimingAlign); CheckAlign(ref s.AttemptAlign);
        if (s.TextEffects == null) { s.TextEffects = new TextEffectConfig(); changed = true; }
        if (s.TextEffects.ShadowColor == null) { s.TextEffects.ShadowColor = new ColorCache(new Color(0, 0, 0, 0.5f)); changed = true; }
        if (s.TextEffects.OutlineColor == null) { s.TextEffects.OutlineColor = new ColorCache(Color.black); changed = true; }
        // 各槽位的独立文本特效：旧配置没有这个数组，按「全部沿用全局」补齐，
        // 保证升级后观感不变（旧版本没有的字段补成 null 也要填默认）。
        int slotCount = Enum.GetValues(typeof(FontSlot)).Length;
        if (s.SlotTextEffects == null || s.SlotTextEffects.Length != slotCount)
        {
            s.SlotTextEffects = CreateDefaultSlotTextEffects();
            changed = true;
        }
        for (int i = 0; i < s.SlotTextEffects.Length; i++)
        {
            if (s.SlotTextEffects[i] == null) { s.SlotTextEffects[i] = new SlotTextEffectConfig(); changed = true; }
            var e = s.SlotTextEffects[i].Effect;
            if (e == null) { s.SlotTextEffects[i].Effect = new TextEffectConfig(); changed = true; continue; }
            if (e.ShadowColor == null) { e.ShadowColor = new ColorCache(new Color(0, 0, 0, 0.5f)); changed = true; }
            if (e.OutlineColor == null) { e.OutlineColor = new ColorCache(Color.black); changed = true; }
        }
        return changed;
    }

    public void OnSaveGUI() { Save(); Colors?.Save(); Labels?.Save(); }
    public void Save() { SaveJson(); }
    public static Settings Load()
    {
        bool freshConfig = !File.Exists(SettingsPath())
            && !File.Exists(SettingsPath() + ".bak")
            && !File.Exists(OldXmlPath());
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
        // 唯一的显示顺序：原 GeneralDisplayOrder 并入（未启用扩展文本时，多余元素自然不显示）。
        // 统一剔除越界/重复项并补齐新增元素；否则重复 id 会重复占栈，负 id 会让 UI 越界。
        var oldExtendedOrder = s.ExtendedDisplayOrder;
        var oldBpmOrder = s.BpmLineOrder;
        var oldAttemptOrder = s.AttemptLineOrder;
        s.ExtendedDisplayOrder = NormalizeOrder(s.ExtendedDisplayOrder,
            GetDefaultExtendedOverlayOrder(), IsValidExtendedOverlayElement);
        s.BpmLineOrder = NormalizeOrder(s.BpmLineOrder, [0, 1, 2], id => id >= 0 && id <= 2);
        if (s.BpmLineVisibility == null || s.BpmLineVisibility.Length != 3)
            s.BpmLineVisibility = [true, true, true];
        s.AttemptLineOrder = NormalizeOrder(s.AttemptLineOrder, [0, 1], id => id >= 0 && id <= 1);
        bool orderChanged = OrderDiffers(oldExtendedOrder, s.ExtendedDisplayOrder)
                         || OrderDiffers(oldBpmOrder, s.BpmLineOrder)
                         || OrderDiffers(oldAttemptOrder, s.AttemptLineOrder);
        if (NormalizeLoadedValues(s) || orderChanged) s.Save();
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
            Loader.WriteAllTextAtomic(SettingsPath(), json);
        }
        catch (Exception e)
        {
            Loader.Warning($"Failed to save Settings.json: {e.Message}");
        }
    }

    private static Settings LoadJson()
    {
        string path = SettingsPath();
        if (File.Exists(path))
        {
            try { return ReadJsonFile(path); }
            catch (Exception e) { Loader.Warning($"Failed to load Settings.json: {e.Message}"); }
        }
        string backup = path + ".bak";
        if (File.Exists(backup))
        {
            try { return ReadJsonFile(backup); }
            catch (Exception e) { Loader.Warning($"Failed to load Settings.json backup: {e.Message}"); }
        }
        return null;
    }

    private static Settings ReadJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        var s = JsonConvert.DeserializeObject<Settings>(json);
        ApplyLegacyExtendedOverlayMigration(s, json);
        MigrateLegacyDisplayOrder(s, json);
        // 手改 JSON 可能留下 null 元素/负字号，统一回填后再交给渲染层。
        if (s != null) CustomTextLine.NormalizeAll(s.CustomTexts);
        return s;
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

    /// <summary>迁移旧版 JALib 的显示顺序键：仅当新字段尚未存在时读取一次，
    /// 避免用户后来在 GUI 排好的顺序被残留旧键每次启动覆盖。</summary>
    static void MigrateLegacyDisplayOrder(Settings s, string json)
    {
        try
        {
            if (s == null) return;
            var jo = JsonConvert.DeserializeObject<JObject>(json);
            var current = jo?["ExtendedDisplayOrder"];
            if (current != null && current.Type != JTokenType.Null) return;
            var legacy = jo?["JongyeolDisplayOrder"]?.ToObject<int[]>();
            if (legacy == null || legacy.Length == 0) return;
            s.ExtendedDisplayOrder = legacy;
        }
        catch
        {
            // 旧键格式异常不应阻断其它设置的加载。
        }
    }

    /// <summary>XML 老配置的同一迁移：以 &lt;ExtendedOverlayMode&gt; 元素是否存在为准。</summary>
    static void ApplyLegacyExtendedOverlayMigrationXml(Settings s, string xmlText)
    {
        if (s == null) return;
        int i = xmlText.IndexOf("<ExtendedOverlayMode>", StringComparison.Ordinal);
        if (i < 0) { ApplyLegacyMigrationCore(s, false, false); MigrateLegacyDisplayOrderXml(s, xmlText); return; }
        int start = i + "<ExtendedOverlayMode>".Length;
        int end = xmlText.IndexOf("</ExtendedOverlayMode>", start, StringComparison.Ordinal);
        bool legacy = end > start && xmlText.Substring(start, end - start).Trim() == "true";
        ApplyLegacyMigrationCore(s, true, legacy);
        MigrateLegacyDisplayOrderXml(s, xmlText);
    }

    static void MigrateLegacyDisplayOrderXml(Settings s, string xmlText)
    {
        try
        {
            int i = xmlText.IndexOf("<JongyeolDisplayOrder>", StringComparison.Ordinal);
            if (i < 0) return;
            if (xmlText.IndexOf("<ExtendedDisplayOrder>", StringComparison.Ordinal) >= 0) return;
            int start = i + "<JongyeolDisplayOrder>".Length;
            int end = xmlText.IndexOf("</JongyeolDisplayOrder>", start, StringComparison.Ordinal);
            if (end <= start) return;
            // XmlSerializer 通常写成 <int>10</int>，但历史手改文件也可能是逗号分隔文本；
            // 提取所有整数 token 兼容两种形态，负值随后由 Normalize 过滤。
            var legacy = Regex.Matches(xmlText.Substring(start, end - start), @"-?\d+")
                .Cast<Match>()
                .Select(m => int.TryParse(m.Value, out int value) ? value : int.MinValue)
                .Where(value => value != int.MinValue)
                .ToArray();
            if (legacy.Length > 0) s.ExtendedDisplayOrder = legacy;
        }
        catch
        {
            // 旧 XML 顺序键损坏时回退默认顺序。
        }
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
            Loader.WriteAllTextAtomic(SettingsPath(), json);
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
