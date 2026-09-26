using System.Collections;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public class OverlayMono : MonoBehaviour
{
    public Overlay Overlay;
    private Coroutine _comboAnim;
    private bool _canvasEnabledState;
    private bool _canvasStateInitialized;

    private void Update()
    {
        if (Overlay == null || !Overlay.GameObject.activeSelf) return;
        // 没有音乐/地图时间文本时无需每帧进入时间计算与格式化路径。
        if (Overlay.TimeDisplayNeeded) Overlay.UpdateTime();
        if (Overlay.Canvas)
        {
            bool desired = !GameRefs.IsPaused;
            if (!_canvasStateInitialized || desired != _canvasEnabledState)
            {
                Overlay.Canvas.enabled = desired;
                _canvasEnabledState = desired;
                _canvasStateInitialized = true;
            }
        }
    }

    public void StartComboBump()
    {
        if (_comboAnim != null) StopCoroutine(_comboAnim);
        _comboAnim = StartCoroutine(ComboAnim());
    }

    public void StopComboBump()
    {
        if (_comboAnim != null) StopCoroutine(_comboAnim);
        _comboAnim = null;
    }

    private IEnumerator ComboAnim()
    {
        double elapsed = 0;
        bool reversed = Main.Settings.ComboLineReversed;
        int baseSize = Main.Settings.ComboValFontSize;
        while (elapsed < 500)
        {
            float t = (float)(elapsed / 500);
            if (t > 1) t = 1;
            Overlay.ComboText.fontSize = (int)(30 * OutExpoChange(t) + baseSize);
            if (Overlay._comboTitleTransform)
            {
                try
                {
                    if (reversed)
                    {
                        Overlay._comboTitleTransform.anchoredPosition = new Vector2(0, -40f - OutExpoChange(t) * 15f);
                    }
                    else
                    {
                        Overlay._comboTitleTransform.anchoredPosition = new Vector2(0, 43.505f + OutExpoChange(t) * 15f);
                    }
                }
                catch { }
            }
            yield return null;
            elapsed += Time.deltaTime * 1000;
        }
        Overlay.ComboText.fontSize = Main.Settings.ComboValFontSize;
        _comboAnim = null;
    }

    private static readonly float[] _expTable = BuildExpoTable();

    private static float[] BuildExpoTable()
    {
        var t = new float[31];
        for (int i = 0; i < t.Length; i++)
        {
            float p = i / 30f;
            t[i] = p >= 1f ? 0f : (float)System.Math.Pow(2.0, -10.0 * p);
        }
        return t;
    }

    private static float OutExpoChange(double t)
    {
        int idx = (int)(t * 30.0 + 0.5);
        return idx >= _expTable.Length ? 0f : _expTable[idx];
    }
}