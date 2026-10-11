using System;
using System.Collections.Generic;
using System.Globalization;
using JipperOverlayer.Overlayer.Localization;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

/// <summary>可自由拖拽的覆盖层分组。每一项都对应 Settings 里的一对 X/Y 偏移字段——
/// 拖动只写偏移，不改动元素自身的布局逻辑（由 ApplyPositionOffsets 统一叠加），
/// 因此引入本编辑器不需要任何新的布局数据模型。</summary>
public enum OverlayHandle
{
    Main,
    Bpm,
    Judge,
    JudgeP2,
    JudgeP3,
    JudgeP4,
    Combo,
    TimingScale,
    Attempt,
    AttemptCoop,
    ProgressBar,
    HitOffset,
}

/// <summary>
/// FreeMake 风格的覆盖层布局编辑器。交互范式参照 JipperKeyViewer 的 FreeMake：
/// 直接在画布上点选与拖拽、拖拽阈值、对齐吸附与参考线（Alt 临时关闭）、方向键微调、
/// Ctrl+Z 撤销、即时写入配置。全部为本仓库原创 IMGUI 实现。
///
/// 与 JipperKeyViewer 的关键差异：那边的节点是独立数据模型，这里的「节点」是既有的
/// 自定义位置偏移分组，所以本编辑器**只写偏移字段**，退出后一切仍由
/// ApplyPositionOffsets 决定，不影响任何已有渲染路径。
///
/// 刻意挂在常驻的加载器 GameObject 上而不是覆盖层自身：覆盖层隐藏（未在关卡中）
/// 或暂停时 GameObject 会被 SetActive(false)，挂上去 OnGUI 就不会再执行，
/// 编辑器会跟着一起消失。
/// </summary>
public class OverlayDragEditor : MonoBehaviour
{
    public static OverlayDragEditor Instance { get; private set; }

    /// <summary>编辑模式是否开启。OverlayMono 用它决定暂停时是否强行打开 Canvas。</summary>
    public bool Active { get; private set; }
    public static bool IsEditing => Instance != null && Instance.Active;

    private const int WindowId = 0x4A4F0E01;
    /// <summary>吸附阈值（屏幕像素，恒定值——缩放画布不影响手感）。</summary>
    private const float SnapThreshold = 8f;
    /// <summary>起拖阈值：小于它一律当成点选，避免手抖把元素挪歪。</summary>
    private const float DragThreshold = 3f;
    private const int UndoCap = 60;

    private static readonly OverlayHandle[] AllHandles = (OverlayHandle[])Enum.GetValues(typeof(OverlayHandle));

    // 宽度要放得下整行：勾选 18 + 名称 130 + X 标签/框 14+58 + Y 标签/框 14+58 + R 22，
    // 再加元素间距、滚动条与窗口内边距——360 会把最右边的 R 裁掉。
    private Rect _window = new(20f, 60f, 400f, 460f);
    private Vector2 _panelScroll;

    private OverlayHandle _selected = OverlayHandle.Main;
    private bool _snap = true;

    // ===== 指针/手势状态 =====
    private bool _pointerDown;
    private Vector2 _pressScreen;
    private int _pressId = -1;
    private bool _dragging;
    private OverlayHandle _dragHandle;
    private Vector2 _dragStartScreen;
    private Vector2 _dragStartOffset;
    private Rect _dragStartRect;
    private Vector2 _dragScale = Vector2.one;

    private float _snapLineX = float.NaN;
    private float _snapLineY = float.NaN;

    private readonly List<Vector2[]> _undo = new();

    // 复用缓冲：MouseDrag 每秒 60~120 次，逐次分配 List/数组会持续制造垃圾。
    private readonly List<float> _snapXs = new();
    private readonly List<float> _snapYs = new();
    private static readonly float[] _movingEdges = new float[3];

    private GUIStyle _label;

    // ===== 数值输入框状态 =====
    private const string NumFieldPrefix = "joe_num_";
    /// <summary>数值框的编辑缓冲，key = (int)handle * 2 + axis（0=X，1=Y）。
    /// **必须缓冲**：若每帧都拿当前偏移回写文本框，用户敲到 "-" 或 "1." 这种
    /// 中间态时文本会被立刻改回原值，根本输不进负数与小数。</summary>
    private readonly Dictionary<int, string> _numBuf = new();
    /// <summary>本帧持焦的数值框 key（-1 = 无）。**每帧在面板开头统一求值一次**，
    /// 不能各框内各自读 GUI.GetNameOfFocusedControl()：那样结果依赖绘制顺序，
    /// 从 X 框直接点到同一行 Y 框时有一个框的失焦落盘会被漏掉。</summary>
    private int _focusNow = -1;
    /// <summary>上一帧持焦的 key，用于识别「刚获得 / 刚失去焦点」。</summary>
    private int _focusLast = -1;
    /// <summary>本次编辑会话已压过撤销的框 key。不能拿 _focusLast 判断：它在帧初
    /// 就被同步成当前框了，那样敲数值永远不会压撤销，整段修改无法回退。</summary>
    private int _undoPushedKey = -1;
    /// <summary>本次编辑会话是否真的敲改过内容。只点进点出不该触发 Settings.json 原子写。</summary>
    private bool _numDirty;
    /// <summary>缓冲对应的数值。未持焦的框每帧都要判断「外部值变没变」才能决定是否
    /// 重排文本；不缓存就得逐帧对每个框格式化一次字符串（24 框 × ~90fps），持续制造垃圾。</summary>
    private readonly Dictionary<int, float> _numVals = new();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetActive(bool value)
    {
        if (Active == value) return;
        Active = value;
        _undo.Clear();
        _snapLineX = _snapLineY = float.NaN;
        _pointerDown = _dragging = false;
        _pressId = -1;

        var o = Overlay.Instance;
        if (value)
        {
            // 拖动要写偏移，先确保偏移真的被应用（否则拖了没反应）。
            if (Main.Settings != null && !Main.Settings.CustomPositionsEnabled)
            {
                Main.Settings.CustomPositionsEnabled = true;
                Main.Settings.Save();
            }
            o?.ForceShowHandleObjects();
            o?.ApplyPositionOffsets();
        }
        else
        {
            // 退出时交还给常规可见性逻辑，恢复用户自己的显示开关。
            o?.ApplyPositionOffsets();
            o?.RefreshVisibility();
            Main.Settings?.Save();
        }
    }

    // ===== 屏幕矩形 =====

    /// <summary>元素当前的屏幕矩形（GUI 坐标系，左上原点）。
    /// 覆盖层是 ScreenSpaceOverlay 画布，世界坐标即屏幕像素，因此
    /// GetWorldCorners 直接给出屏幕位置，不用自己推 anchoredPosition。</summary>
    private bool TryGetScreenRect(OverlayHandle h, out Rect rect, bool enforceMin = true)
    {
        rect = default;
        var o = Overlay.Instance;
        if (o == null) return false;
        // 矩形换算统一收在 Overlay 里（主栈要取启用子文本的并集，不能直接用容器）。
        if (!o.TryGetHandleScreenRect(h, out rect)) return false;
        if (enforceMin)
        {
            // 文本为空或未启用时矩形可能退化成一条线，给一个最小可抓取面积。
            const float minW = 48f, minH = 22f;
            if (rect.width < minW)
            {
                float cx = rect.center.x;
                rect.xMin = cx - minW * 0.5f;
                rect.xMax = cx + minW * 0.5f;
            }
            if (rect.height < minH)
            {
                float cy = rect.center.y;
                rect.yMin = cy - minH * 0.5f;
                rect.yMax = cy + minH * 0.5f;
            }
        }
        return rect.width > 0f || rect.height > 0f;
    }

    private bool Available(OverlayHandle h) => Overlay.Instance?.IsHandleAvailable(h) ?? false;

    private int PickHandle(Vector2 p)
    {
        int best = -1;
        float bestArea = float.MaxValue;
        for (int i = 0; i < AllHandles.Length; i++)
        {
            var h = AllHandles[i];
            if (!Available(h)) continue;
            if (!TryGetScreenRect(h, out var r)) continue;
            if (!r.Contains(p)) continue;
            float area = r.width * r.height;
            // 重叠时选面积更小的那个（更具体的元素优先）。
            if (area < bestArea) { bestArea = area; best = i; }
        }
        return best;
    }

    // ===== OnGUI =====

    private void OnGUI()
    {
        if (!Active) return;
        var o = Overlay.Instance;
        if (o == null || o.GameObject == null) return;

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.Layout)
        {
            // 编辑期间强制显示所有可拖元素，保证矩形是真实的、所见即所得；
            // 退出编辑时 RefreshVisibility 会按用户的开关还原。
            o.ForceShowHandleObjects();
            // Canvas 的可见性由 OverlayMono 统一裁决（它会读 OverlayDragEditor.IsEditing
            // 在暂停时保留画布），这里刻意不再直接写 Canvas.enabled——两处各写一次
            // 会互相覆盖，表现为暂停下拖拽时整层黑掉。
        }

        bool playable = o.GameObject.activeSelf;
        HandleCanvasEvents(e, playable);
        if (e.type == EventType.Repaint) DrawHandles(playable);
        HandleShortcuts(e, playable);

        _window = GUILayout.Window(WindowId, _window, DrawPanel, Tr.Get(Tr.Key.LayoutEditorTitle));
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));
    }

    private void HandleCanvasEvents(Event e, bool playable)
    {
        if (!playable) return;
        bool overPanel = _window.Contains(e.mousePosition);

        if (e.type == EventType.MouseDown && e.button == 0 && !overPanel)
        {
            // 只有真正命中把手才起手势。空白处完全不碰事件：本编辑器与 UMM 的
            // 设置窗口同帧绘制且顺序不定，若在这里无条件吃掉 MouseDown/MouseUp，
            // 设置窗里正好叠在画布上方的按钮就会失灵。
            int id = PickHandle(e.mousePosition);
            if (id >= 0)
            {
                _pointerDown = true;
                _pressScreen = e.mousePosition;
                _pressId = id;
                _selected = AllHandles[id];
                e.Use();
            }
            return;
        }

        if (_pointerDown && e.type == EventType.MouseDrag && e.button == 0)
        {
            if (!_dragging && _pressId >= 0
                && (e.mousePosition - _pressScreen).sqrMagnitude >= DragThreshold * DragThreshold)
            {
                BeginDrag(_pressId, _pressScreen);
            }
            if (_dragging)
            {
                UpdateDrag(e);
                e.Use();
            }
            return;
        }

        if (_pointerDown && e.type == EventType.MouseUp && e.button == 0)
        {
            EndDrag();
            _pointerDown = false;
            _pressId = -1;
            e.Use();
            return;
        }

        // MouseUp 可能彻底丢失（Alt-Tab、拖到窗口外、焦点变化）。没有这层兜底，
        // _pointerDown 会永久为真，此后所有手势都错位。
        if (_pointerDown && !Input.GetMouseButton(0))
        {
            EndDrag();
            _pointerDown = false;
            _pressId = -1;
        }
    }

    private void BeginDrag(int id, Vector2 screen)
    {
        var o = Overlay.Instance;
        if (o == null) return;
        _dragHandle = AllHandles[id];
        if (!TryGetScreenRect(_dragHandle, out _dragStartRect)) return;
        _dragging = true;
        _dragStartScreen = screen;
        _dragStartOffset = o.GetHandleOffset(_dragHandle);
        var rt = o.GetHandleTransform(_dragHandle);
        // 直接用该元素的 lossyScale 做「锚点单位 <-> 屏幕像素」换算，
        // 比读 Canvas.scaleFactor 更贴近实际（且不依赖 CanvasScaler 的具体模式）。
        _dragScale = rt != null ? rt.lossyScale : Vector2.one;
        if (Mathf.Abs(_dragScale.x) < 0.0001f) _dragScale.x = 1f;
        if (Mathf.Abs(_dragScale.y) < 0.0001f) _dragScale.y = 1f;
        PushUndo();
    }

    private void UpdateDrag(Event e)
    {
        var o = Overlay.Instance;
        if (o == null) return;
        Vector2 screenDelta = e.mousePosition - _dragStartScreen;
        Vector2 newOffset = _dragStartOffset + new Vector2(
            screenDelta.x / _dragScale.x,
            -screenDelta.y / _dragScale.y);

        // 先用「预测矩形」算吸附量，再把吸附修正折回偏移空间。
        Vector2 shift = new(
            (newOffset.x - _dragStartOffset.x) * _dragScale.x,
            -(newOffset.y - _dragStartOffset.y) * _dragScale.y);
        Rect predicted = _dragStartRect;
        predicted.position += shift;

        Vector2 snapPx = ComputeSnap(predicted, _dragHandle, e.alt);
        if (snapPx.x != 0f || snapPx.y != 0f)
            newOffset += new Vector2(snapPx.x / _dragScale.x, -snapPx.y / _dragScale.y);

        o.SetHandleOffset(_dragHandle, newOffset);
        o.ApplyPositionOffsets();
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _snapLineX = _snapLineY = float.NaN;
        Overlay.Instance?.ApplyPositionOffsets();
        Main.Settings?.Save();
    }

    /// <summary>对齐吸附：把被拖元素的左/中/右（上/中/下）与屏幕边、屏幕中心及其它元素
    /// 的对应边比对，取最近的一条。返回屏幕像素修正量，并记录参考线位置。</summary>
    private Vector2 ComputeSnap(Rect moving, OverlayHandle self, bool bypass)
    {
        _snapLineX = float.NaN;
        _snapLineY = float.NaN;
        if (!_snap || bypass) return Vector2.zero;

        _snapXs.Clear();
        _snapYs.Clear();
        _snapXs.Add(0f); _snapXs.Add(Screen.width * 0.5f); _snapXs.Add(Screen.width);
        _snapYs.Add(0f); _snapYs.Add(Screen.height * 0.5f); _snapYs.Add(Screen.height);
        for (int i = 0; i < AllHandles.Length; i++)
        {
            var h = AllHandles[i];
            if (h == self || !Available(h)) continue;
            if (!TryGetScreenRect(h, out var r)) continue;
            _snapXs.Add(r.xMin); _snapXs.Add(r.center.x); _snapXs.Add(r.xMax);
            _snapYs.Add(r.yMin); _snapYs.Add(r.center.y); _snapYs.Add(r.yMax);
        }

        _movingEdges[0] = moving.xMin;
        _movingEdges[1] = moving.center.x;
        _movingEdges[2] = moving.xMax;
        float dx = 0f, bestX = float.MaxValue, lineX = float.NaN;
        for (int i = 0; i < _snapXs.Count; i++)
        {
            float cand = _snapXs[i];
            for (int j = 0; j < 3; j++)
            {
                float d = cand - _movingEdges[j];
                float a = Mathf.Abs(d);
                if (a < bestX) { bestX = a; dx = d; lineX = cand; }
            }
        }
        if (bestX > SnapThreshold) { dx = 0f; lineX = float.NaN; }

        _movingEdges[0] = moving.yMin;
        _movingEdges[1] = moving.center.y;
        _movingEdges[2] = moving.yMax;
        float dy = 0f, bestY = float.MaxValue, lineY = float.NaN;
        for (int i = 0; i < _snapYs.Count; i++)
        {
            float cand = _snapYs[i];
            for (int j = 0; j < 3; j++)
            {
                float d = cand - _movingEdges[j];
                float a = Mathf.Abs(d);
                if (a < bestY) { bestY = a; dy = d; lineY = cand; }
            }
        }
        if (bestY > SnapThreshold) { dy = 0f; lineY = float.NaN; }

        _snapLineX = lineX;
        _snapLineY = lineY;
        return new Vector2(dx, dy);
    }

    // ===== 绘制 =====

    private void EnsureStyles()
    {
        _label ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
        };
    }

    private void DrawHandles(bool playable)
    {
        EnsureStyles();
        for (int i = 0; i < AllHandles.Length; i++)
        {
            var h = AllHandles[i];
            if (!playable || !Available(h)) continue;
            if (!TryGetScreenRect(h, out var r)) continue;
            bool sel = _selected == h || (_dragging && _dragHandle == h);
            Fill(r, sel ? new Color(0.30f, 0.67f, 1f, 0.30f) : new Color(1f, 1f, 1f, 0.08f));
            Outline(r, sel ? new Color(0.30f, 0.67f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f));
            GUI.Label(new Rect(r.x + 4f, r.y + 2f, Mathf.Max(80f, r.width - 8f), 20f), HandleLabel(h), _label);
        }
        if (!float.IsNaN(_snapLineX)) Fill(new Rect(_snapLineX - 0.5f, 0f, 1f, Screen.height), new Color(1f, 0.35f, 0.85f, 0.9f));
        if (!float.IsNaN(_snapLineY)) Fill(new Rect(0f, _snapLineY - 0.5f, Screen.width, 1f), new Color(1f, 0.35f, 0.85f, 0.9f));
    }

    private static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void Outline(Rect r, Color c, float t = 1f)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private static string HandleLabel(OverlayHandle h) => h switch
    {
        OverlayHandle.Main => Tr.Get(Tr.Key.PosMain),
        OverlayHandle.Bpm => Tr.Get(Tr.Key.PosBPM),
        OverlayHandle.Judge => Tr.Get(Tr.Key.PosJudge),
        OverlayHandle.JudgeP2 => Tr.Get(Tr.Key.PosP2),
        OverlayHandle.JudgeP3 => Tr.Get(Tr.Key.PosP3),
        OverlayHandle.JudgeP4 => Tr.Get(Tr.Key.PosP4),
        OverlayHandle.Combo => Tr.Get(Tr.Key.PosCombo),
        OverlayHandle.TimingScale => Tr.Get(Tr.Key.PosTiming),
        OverlayHandle.Attempt => Tr.Get(Tr.Key.PosAttempt),
        OverlayHandle.AttemptCoop => Tr.Get(Tr.Key.PosAttempt) + " (Coop)",
        OverlayHandle.ProgressBar => Tr.Get(Tr.Key.PosProgBar),
        OverlayHandle.HitOffset => Tr.Get(Tr.Key.PosHitOffset),
        _ => h.ToString(),
    };

    // ===== 面板 =====

    private void DrawPanel(int id)
    {
        var o = Overlay.Instance;
        bool playable = o != null && o.GameObject != null && o.GameObject.activeSelf;

        GUILayout.BeginVertical();
        if (!playable)
        {
            GUILayout.Label(Tr.Get(Tr.Key.LayoutEditorNotPlaying));
            if (GUILayout.Button(Tr.Get(Tr.Key.LayoutEditorExit))) SetActive(false);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            return;
        }

        // 数值框的焦点在每帧面板开头统一结算，不依赖各框的绘制顺序。
        SyncNumFieldFocus();

        GUILayout.Label(Tr.Get(Tr.Key.LayoutEditorHint));
        _snap = GUILayout.Toggle(_snap, Tr.Get(Tr.Key.LayoutEditorSnap));

        GUILayout.Space(4f);
        _panelScroll = GUILayout.BeginScrollView(_panelScroll, GUILayout.Height(260f));
        for (int i = 0; i < AllHandles.Length; i++)
        {
            var h = AllHandles[i];
            bool avail = Available(h);
            GUILayout.BeginHorizontal();
            bool sel = _selected == h;
            bool now = GUILayout.Toggle(sel, GUIContent.none, GUILayout.Width(18f));
            if (now && !sel) _selected = h;
            string label = HandleLabel(h);
            var rt = o.GetHandleTransform(h);
            if (rt != null && !rt.gameObject.activeInHierarchy) label += " " + Tr.Get(Tr.Key.LayoutEditorHidden);
            GUILayout.Label(label, GUILayout.Width(130f));
            GUI.enabled = avail;
            Vector2 off = avail ? o.GetHandleOffset(h) : Vector2.zero;
            DrawNumField(h, 0, "X", off.x, avail);
            DrawNumField(h, 1, "Y", off.y, avail);
            if (GUILayout.Button("R", GUILayout.Width(22f)))
            {
                PushUndo();
                o.SetHandleOffset(h, Vector2.zero);
                o.ApplyPositionOffsets();
                InvalidateNumBuffers();
                Main.Settings?.Save();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUI.enabled = _undo.Count > 0;
        if (GUILayout.Button(Tr.Get(Tr.Key.LayoutEditorUndo))) Undo();
        GUI.enabled = true;
        if (GUILayout.Button(Tr.Get(Tr.Key.LayoutEditorResetAll)))
        {
            PushUndo();
            Main.Settings?.ResetCustomPos();
            o.ApplyPositionOffsets();
            InvalidateNumBuffers();
            Main.Settings?.Save();
        }
        if (GUILayout.Button(Tr.Get(Tr.Key.LayoutEditorSave))) Main.Settings?.Save();
        GUILayout.EndHorizontal();

        if (GUILayout.Button(Tr.Get(Tr.Key.LayoutEditorExit))) SetActive(false);
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    // ===== 数值输入框 =====

    /// <summary>结算数值框的焦点迁移：刚失去焦点的框规范化文本并落盘。
    /// 每帧只调一次，且在任何框绘制之前——否则结果会依赖绘制顺序。</summary>
    private void SyncNumFieldFocus()
    {
        int focused = CurrentFocusKey();
        if (focused == _focusLast) { _focusNow = focused; return; }
        _undoPushedKey = -1;   // 焦点迁移 = 新的一次编辑会话
        bool wasDirty = _numDirty;
        _numDirty = false;

        if (_focusLast >= 0)
        {
            // 刚失去焦点：丢弃缓冲与数值缓存，让下一帧从真实偏移重算显示。
            // **不能只在解析成功时重写**：若用户停在 "-" 或 "1." 这类非法中间态，
            // 解析失败就什么都不做的话，框里会永远留着那个字符。清掉即可兼顾
            // 「合法值规范化」与「非法值回退」两种情况。
            _numBuf.Remove(_focusLast);
            _numVals.Remove(_focusLast);
            // 落盘放在失焦时而不是每次按键：否则连打会触发几十次 Settings.json 原子写。
            // 只在真敲改过时写，避免点进点出也产生一次写盘。
            if (wasDirty) Main.Settings?.Save();
        }
        _focusNow = focused;
        _focusLast = focused;
    }

    private static int CurrentFocusKey()
    {
        string focus = GUI.GetNameOfFocusedControl();
        if (string.IsNullOrEmpty(focus) || !focus.StartsWith(NumFieldPrefix, StringComparison.Ordinal)) return -1;
        return int.TryParse(focus.Substring(NumFieldPrefix.Length), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int key) ? key : -1;
    }

    private float CurrentValue(int key)
    {
        var o = Overlay.Instance;
        if (o == null || !TryDecodeKey(key, out var h, out int axis)) return 0f;
        return o.GetHandleOffset(h)[axis];
    }

    private static bool TryDecodeKey(int key, out OverlayHandle h, out int axis)
    {
        axis = key & 1;
        int id = key >> 1;
        if (id < 0 || id >= AllHandles.Length) { h = default; return false; }
        h = AllHandles[id];
        return true;
    }

    private bool TryParseBuf(int key, out float value)
        => TryParseNum(_numBuf.TryGetValue(key, out string s) ? s : null, out value);

    /// <summary>单个坐标数值框（X 或 Y）。可直接敲精确值；输入过程中实时生效但
    /// 不落盘，失焦时（见 SyncNumFieldFocus）才规范化并写 Settings.json。</summary>
    private void DrawNumField(OverlayHandle h, int axis, string label, float current, bool enabled)
    {
        int key = (int)h * 2 + axis;
        string ctrl = NumFieldPrefix + key;
        GUI.SetNextControlName(ctrl);
        if (!_numBuf.TryGetValue(key, out string buf)) buf = FormatNum(current);

        var o = Overlay.Instance;
        GUILayout.Label(label, GUILayout.Width(14f));
        if (!enabled || o == null)
        {
            // 不可用时退化成只读文本，但保持行宽一致，避免整列左右跳动。
            // 这里直接用实时的 current，不用缓冲——缓冲可能是该把手还可用时的旧值。
            GUILayout.Label(FormatNum(current), GUILayout.Width(58f));
            return;
        }

        string typed = GUILayout.TextField(buf, GUILayout.Width(58f));
        if (_focusNow == key)
        {
            if (typed != buf)
            {
                // 一次编辑会话只压一条撤销。不能拿 _focusLast 判断：它在帧初的
                // SyncNumFieldFocus 里已被同步成当前框，条件恒假，敲数值压不进撤销。
                if (_undoPushedKey != key) { PushUndo(); _undoPushedKey = key; }
                _numDirty = true;
                _numBuf[key] = typed;
                // 同步数值缓存，否则失焦后第一次「未持焦」判断会误以为外部值变过。
                if (TryParseNum(typed, out float shown)) _numVals[key] = shown;
                // 实时生效但不落盘：中间态（"-"、"1."）解析失败时只是不应用，
                // 文本仍留在框里，用户可以继续敲。
                if (TryParseNum(typed, out float parsed))
                {
                    var off = o.GetHandleOffset(h);
                    off[axis] = parsed;
                    o.SetHandleOffset(h, off);
                    o.ApplyPositionOffsets();
                }
            }
        }
        else if (!_numVals.TryGetValue(key, out float cached) || cached != current)
        {
            // 未持焦：外部值真的变了才重排文本（拖拽 / 方向键 / 撤销 / R 归零），
            // 否则每帧对每个框格式化一次字符串纯属浪费。
            _numBuf[key] = FormatNum(current);
            _numVals[key] = current;
        }
    }

    private static bool TryParseNum(string s, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(s)) return false;
        // 固定用 InvariantCulture：某些区域设置下小数点会变逗号，
        // 跟着系统走会让 "1.5" 解析失败。
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        if (float.IsNaN(value) || float.IsInfinity(value)) return false;
        // 与滑杆同范围，避免手输把元素丢到屏幕外找不回来。
        value = Mathf.Clamp(value, -2000f, 2000f);
        return true;
    }

    /// <summary>整数不留小数点、需要时保留两位——避免框里显示 "16.00" 这种噪音。</summary>
    private static string FormatNum(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ===== 快捷键 =====

    private void HandleShortcuts(Event e, bool playable)
    {
        if (!playable || e == null || e.type != EventType.KeyDown) return;
        // 数值框持焦时让出全部快捷键：方向键在框内是移动光标、Ctrl+Z 是文本撤销，
        // 都不该动元素。Enter/Esc 用来提交并交出焦点。
        if (CurrentFocusKey() >= 0)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
            {
                GUI.FocusControl(null);
                e.Use();
            }
            return;
        }
        if (e.control && e.keyCode == KeyCode.Z)
        {
            Undo();
            e.Use();
            return;
        }
        float step = e.shift ? 10f : 1f;
        float dx = 0f, dy = 0f;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow: dx = -step; break;
            case KeyCode.RightArrow: dx = step; break;
            case KeyCode.UpArrow: dy = -step; break;
            case KeyCode.DownArrow: dy = step; break;
            default: return;
        }
        Nudge(dx, dy);
        e.Use();
    }

    private void Nudge(float screenDx, float screenDy)
    {
        var o = Overlay.Instance;
        if (o == null || !Available(_selected)) return;
        var rt = o.GetHandleTransform(_selected);
        Vector2 scale = rt != null ? rt.lossyScale : Vector2.one;
        if (Mathf.Abs(scale.x) < 0.0001f) scale.x = 1f;
        if (Mathf.Abs(scale.y) < 0.0001f) scale.y = 1f;
        PushUndo();
        Vector2 off = o.GetHandleOffset(_selected);
        o.SetHandleOffset(_selected, off + new Vector2(screenDx / scale.x, -screenDy / scale.y));
        o.ApplyPositionOffsets();
        Main.Settings?.Save();
    }

    // ===== 撤销 =====

    private Vector2[] Snapshot()
    {
        var arr = new Vector2[AllHandles.Length];
        var o = Overlay.Instance;
        for (int i = 0; i < AllHandles.Length; i++)
            arr[i] = o?.GetHandleOffset(AllHandles[i]) ?? Vector2.zero;
        return arr;
    }

    private void PushUndo()
    {
        if (Overlay.Instance == null) return;
        _undo.Add(Snapshot());
        if (_undo.Count > UndoCap) _undo.RemoveAt(0);
    }

    private void Undo()
    {
        var o = Overlay.Instance;
        if (o == null || _undo.Count == 0) return;
        var arr = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        for (int i = 0; i < AllHandles.Length; i++) o.SetHandleOffset(AllHandles[i], arr[i]);
        o.ApplyPositionOffsets();
        // 未持焦的框每帧会跟随 CurrentValue 自动刷新，持焦的那个不会；不清缓冲的话
        // 正在编辑的框会继续显示撤销前的旧数字。
        _numBuf.Clear();
        Main.Settings?.Save();
    }

    /// <summary>外部批量改动偏移后（撤销、R 归零、全部重置）作废数值框缓冲，
    /// 让每帧的重绘把新值填回去。</summary>
    private void InvalidateNumBuffers()
    {
        _numBuf.Clear();
        // 数值缓存必须一并清掉：留着它的话，「未持焦」分支会认为外部值没变，
        // 于是撤消/R 归零后框里仍显示旧数字。
        _numVals.Clear();
    }
}
