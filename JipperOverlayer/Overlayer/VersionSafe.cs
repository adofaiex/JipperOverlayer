using HarmonyLib;
using JipperOverlayer.Overlayer.Util;
using System;
using System.Reflection;
using UnityEngine;

namespace JipperOverlayer.Overlayer;

public static class VersionSafe
{
    public static bool IsInitialized { get; private set; }
    public static bool IsV141OrLater { get; private set; } = true;

    // Cached function pointers — zero reflection at runtime
    private static Func<int[]> _getHitMarginsCount;
    private static Func<scrController, double> _getPlanetSpeed;
    private static Action _calculatePercentAcc;
    private static Func<float> _getPercentAcc;
    private static Func<float> _getPercentXAcc;
    private static Func<bool> _isCoopMode;
    private static Func<scrShowIfDebug, bool> _getHideWithNoAuto;
    private static Func<int> _getPlayerCount;
    private static Func<object, int> _getPlayerIndex;
    private static Func<int, int[]> _getHitMarginsCountForPlayer;
    private static Func<int, string> _getPlayerColorHex;

    // CalculatePercentAcc 跨版本重载：r148 无参 / r150 某段构建带 bool 默认参 / r150 新版又改回无参
    // （2026-09-13 的游戏更新把 bool increaseRemainingPlayerHits 参数删掉了，见 ProbeCalculatePercentAcc）
    private static Action<scrMarginTracker> _calcAccNoArg;
    private static MethodInfo _calcAccBool;

    // Single-slot memo for the per-hit player nameplate hex string
    private static int _cachedPlayerHexIdx = -1;
    private static Color _cachedPlayerHexColor;
    private static string _cachedPlayerHex;

    public static void Setup()
    {
        if (IsInitialized) return;
        IsInitialized = true;

        IsV141OrLater = DetectApiVersion();
        // 版本细分：旧探测只能分 v136/v141+；兼容层补充精确 release 号、HitMargin 取值数与 XPerfect 来源
        Loader.Log($"API version: {(IsV141OrLater ? "v141+" : "v136")} | {HitMarginCompat.VersionReport}");

        if (IsV141OrLater)
            BindV141Delegates();
        else
            BindV136Delegates();
    }

    private static bool DetectApiVersion()
    {
        try { return AccessTools.TypeByName("scrMarginTracker") != null
                    && PatchManager.CreateStaticMemberGetter(typeof(ADOBase), "playerManager") != null; }
        catch { return false; }
    }

    /// <summary>
    /// 空判定数组。长度必须等于 HitMargin 的取值个数：
    /// r148 为 12、r150 为 16。写死长度会让 r150 上后段判定（含 XPerfect）被静默丢弃。
    /// </summary>
    private static int[] NewEmptyCounts() => new int[HitMarginCompat.Count];

    /// <summary>
    /// 按签名探测 scrMarginTracker.CalculatePercentAcc。
    /// 优先绑定无参版本（r148，以及 2026-09-13 起的 r150 新版；可直接 CreateDelegate，零反射开销）；
    /// 只有带 bool 的中间版本（r150 早期构建）才保留 MethodInfo 走反射调用。
    /// 该 bool 参数（increaseRemainingPlayerHits）已被游戏移除，无参分支是当前主线。
    /// </summary>
    private static void ProbeCalculatePercentAcc()
    {
        try
        {
            var noArg = AccessTools.Method(typeof(scrMarginTracker), "CalculatePercentAcc", Type.EmptyTypes);
            if (noArg != null)
            {
                _calcAccNoArg = (Action<scrMarginTracker>)Delegate.CreateDelegate(typeof(Action<scrMarginTracker>), noArg);
                return;
            }
            _calcAccBool = AccessTools.Method(typeof(scrMarginTracker), "CalculatePercentAcc", new[] { typeof(bool) });
            if (_calcAccBool == null)
                Loader.Warning("VersionSafe: 未找到 scrMarginTracker.CalculatePercentAcc（精度显示可能不刷新）");
        }
        catch (Exception e)
        {
            Loader.Warning($"VersionSafe: CalculatePercentAcc 探测失败 ({e.Message})");
        }
    }

    // ===== v141+ — direct access, zero overhead =====

    private static void BindV141Delegates()
    {
        ProbeCalculatePercentAcc();

        _getHitMarginsCount = () =>
        {
            if (scrMistakesManager.marginTrackers == null || scrMistakesManager.marginTrackers.Length == 0)
                return NewEmptyCounts();
            return scrMistakesManager.marginTrackers[0].hitMarginsCount ?? NewEmptyCounts();
        };

        _getPlanetSpeed = ctrl =>
        {
            if (ctrl.playerOne?.planetarySystem != null)
                return ctrl.playerOne.planetarySystem.speed;
            return 1.0;
        };

        _calculatePercentAcc = () =>
        {
            if (scrMistakesManager.marginTrackers == null) return;
            // r148 是 CalculatePercentAcc()；r150 早期构建加过 CalculatePercentAcc(bool increaseRemainingPlayerHits = false)，
            // 2026-09-13 的更新又把它删回无参。默认参数只是编译期语法糖：本 mod 以 r148 为基线编译，
            // 会发出零参 callvirt，在「带 bool 参数」那一版上直接 MissingMethodException，因此必须运行时挑重载。
            if (_calcAccNoArg != null)
            {
                foreach (var t in scrMistakesManager.marginTrackers)
                    if (t != null) _calcAccNoArg(t);
            }
            else if (_calcAccBool != null)
            {
                var args = new object[] { false };
                foreach (var t in scrMistakesManager.marginTrackers)
                    if (t != null) _calcAccBool.Invoke(t, args);
            }
        };

        _getPercentAcc = () => ADOBase.playerManager?.mistakesManager?.percentAcc ?? 1f;
        _getPercentXAcc = () => ADOBase.playerManager?.mistakesManager?.percentXAcc ?? 1f;
        _isCoopMode = () => GetPlayerCount() > 1;
        _getHideWithNoAuto = instance => instance.hideWithNoAuto;

        _getPlayerCount = () => scrMistakesManager.marginTrackers?.Length ?? 1;

        _getPlayerIndex = tracker =>
        {
            if (tracker == null || scrMistakesManager.marginTrackers == null)
                return 0;
            var trackers = scrMistakesManager.marginTrackers;
            for (int i = 0; i < trackers.Length; i++)
            {
                if (trackers[i] == tracker)
                    return i;
            }
            return 0;
        };
        _getHitMarginsCountForPlayer = (playerIdx) =>
        {
            if (scrMistakesManager.marginTrackers == null || playerIdx >= scrMistakesManager.marginTrackers.Length)
                return NewEmptyCounts();
            return scrMistakesManager.marginTrackers[playerIdx]?.hitMarginsCount ?? NewEmptyCounts();
        };

        _getPlayerColorHex = (playerIdx) =>
        {
            if (scrPlayerManager.playerColors == null || playerIdx >= scrPlayerManager.playerColors.Length)
                return "FFFFFF";
            Color c = scrPlayerManager.playerColors[playerIdx].ToRealColor();
            if (_cachedPlayerHexIdx == playerIdx && _cachedPlayerHexColor == c)
                return _cachedPlayerHex;
            _cachedPlayerHexIdx = playerIdx;
            _cachedPlayerHexColor = c;
            return _cachedPlayerHex = ColorUtility.ToHtmlStringRGB(c);
        };
    }

    // ===== v136 — full reflection, no direct member access =====
    private static void BindV136Delegates()
    {
        var mmType = typeof(scrMistakesManager);

        // hitMarginsCount — static field or static property (type varies by version)
        var hitMarginsGetter = TryStaticMemberGetter(mmType, "hitMarginsCount");
        _getHitMarginsCount = () =>
        {
            var v = hitMarginsGetter?.Invoke();
            return v is int[] arr ? arr : NewEmptyCounts();
        };

        // speed — instance field or property (type varies by version)
        var speedGetter = TryMemberGetter<scrController>("speed");
        _getPlanetSpeed = ctrl =>
        {
            if (speedGetter == null || ctrl == null) return 1.0;
            var v = speedGetter(ctrl);
            return v is double d ? d : v is float f ? f : 1.0;
        };

        // mistakesManager — instance field or property + cached static getter
        var mmGetter = TryMemberGetter<scrController>("mistakesManager");
        var instanceGetter = TryStaticMemberGetter(typeof(scrController), "_instance");
        scrMistakesManager GetMM()
        {
            if (mmGetter == null || instanceGetter == null) return null;
            var ctrl = instanceGetter();
            return ctrl is scrController c && mmGetter(c) is scrMistakesManager mm ? mm : null;
        }

        var calcAcc = TryMethodInfo(mmType, "CalculatePercentAcc");
        _calculatePercentAcc = () => calcAcc?.Invoke(GetMM(), null);

        // percentAcc / percentXAcc — instance fields or properties on mistakesManager
        var accGetter = TryMemberGetter<scrMistakesManager>("percentAcc");
        _getPercentAcc = () =>
        {
            var mm = GetMM();
            return mm != null && accGetter != null && accGetter(mm) is float f ? f : 1f;
        };

        var xAccGetter = TryMemberGetter<scrMistakesManager>("percentXAcc");
        _getPercentXAcc = () =>
        {
            var mm = GetMM();
            return mm != null && xAccGetter != null && xAccGetter(mm) is float f ? f : 1f;
        };

        _isCoopMode = () => false;
        _getHideWithNoAuto = _ => true;
        _getPlayerCount = () => 1;
        _getPlayerIndex = _ => 0;
        _getHitMarginsCountForPlayer = (_) => GetHitMarginsCount();
        _getPlayerColorHex = (_) => "";
    }

    // ===== Safe wrappers — return null on miss instead of throwing =====
    private static Func<object> TryStaticMemberGetter(Type type, string name)
    {
        try { return PatchManager.CreateStaticMemberGetter(type, name); }
        catch (Exception e) { Loader.Warning($"VersionSafe: 字段或属性 {type.Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static Func<T, object> TryMemberGetter<T>(string name) where T : class
    {
        try { return PatchManager.CreateMemberGetter<T>(name); }
        catch (Exception e) { Loader.Warning($"VersionSafe: 字段或属性 {typeof(T).Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    private static MethodInfo TryMethodInfo(Type type, string name)
    {
        try { return PatchManager.GetMethodInfo(type, name); }
        catch (Exception e) { Loader.Warning($"VersionSafe: 方法 {type.Name}.{name} 不存在 ({e.Message})"); return null; }
    }

    // ========== Public API ==========
    public static int[] GetHitMarginsCount() => _getHitMarginsCount?.Invoke() ?? NewEmptyCounts();
    public static double GetPlanetSpeed(scrController ctrl) => _getPlanetSpeed?.Invoke(ctrl) ?? 1.0;
    public static void CalculatePercentAcc() => _calculatePercentAcc?.Invoke();
    public static float GetPercentAcc() => _getPercentAcc?.Invoke() ?? 1f;
    public static float GetPercentXAcc() => _getPercentXAcc?.Invoke() ?? 1f;
    public static bool IsCoopMode() => _isCoopMode?.Invoke() ?? false;
    public static bool GetHideWithNoAuto(scrShowIfDebug instance) => _getHideWithNoAuto?.Invoke(instance) ?? true;

    public static int GetPlayerCount() => _getPlayerCount?.Invoke() ?? 1;

    public static int GetPlayerIndex(object tracker) => _getPlayerIndex?.Invoke(tracker) ?? 0;
    public static int[] GetHitMarginsCountForPlayer(int playerIdx) => _getHitMarginsCountForPlayer?.Invoke(playerIdx) ?? GetHitMarginsCount();
    public static string GetPlayerColorHex(int playerIdx) => _getPlayerColorHex?.Invoke(playerIdx) ?? "";
}