using HarmonyLib;
using JipperOverlayer.Overlayer.Util;
using System;
using System.Reflection;

namespace JipperOverlayer.Overlayer.Features;

/// <summary>
/// 结算界面「完美」明细：在 DetailedResults.GenerateResults 生成的中间完美计数后面，追加紧凑的
/// [+Perfect / XPerfect(+Auto) / -Perfect] 括号。
///
/// 版式来源（本功能核心）：r149+ 的 GenerateResults 会按 Persistence.hitMarginPerfectText 挑一套
/// 版式。选到 signed 预设（如 ShowXPerfectAndSignedPerfects）时，首行变成
/// 「-完美 / X完美(auto) / +完美」，把「稍快 / 完美 / 稍慢」挤到了第二行——这正是用户看到的
/// 「- +完美被换成 ep lp，但是没有补位」。
/// 所以本补丁不去删那些 ± 列，而是在调用前把预设临时压成 Default（= 0），让游戏按原生三列同行
/// 输出「稍快 / 完美 / 稍慢」，事后再把原值放回去，并把 [+X/-] 括号贴到中间那一列上。
/// 首行天然三列齐全，无需任何补位。
///
/// 压制走 Persistence.generalPrefs 的键值（hitMarginPerfectText），全程反射，不引用
/// HitMarginPerfectTextPreset —— 该枚举在 r148 不存在，直接引用会 TypeLoadException。
/// 该键不在 PlayerPrefsJson.nonSyncedKeys 中，SetInt 只改内存 dict、不落盘；恢复放在 Finalizer，
/// 原方法即使抛异常也一定还原，不会把玩家设置改成 Default。
///
/// 版本兼容：同一个 DLL 同时兼容 r148 / r150。r148 的 GenerateResults 根本没有这套 switch，
/// 首行本来就是三列同行，也没有原生三分量，因此补丁在 r148 整段空转（Available 为 false）。
/// </summary>
[HarmonyPatch(typeof(DetailedResults), "GenerateResults")]
internal static class ResultsPerfectDetailPatch
{
    /// <summary>DetailedResults 同一行内各列的固定分隔符：5 个空格。</summary>
    private const string ColumnSeparator = "     ";

    /// <summary>完美判定绿，与本 mod 判定行、游戏 colourPerfect 一致。</summary>
    private const string PerfectGreen = "#60FF4E";

    /// <summary>版式预设所在的 PlayerPrefsJson 键，与 Persistence.keyHitMarginPerfectText 一致。</summary>
    private const string PerfectTextKey = "hitMarginPerfectText";

    private static readonly object Sync = new object();

    // 嵌套深度：合作模式会按玩家逐个调用 GenerateResults，只在最外层动设置。
    private static int _depth;
    private static bool _hasSaved;
    private static int _savedPerfectText;

    private static bool _suppressLogged;
    private static bool _unavailableLogged;

    private static PropertyInfo _generalPrefsProperty;
    private static bool _generalPrefsProbed;
    private static MethodInfo _getInt;
    private static MethodInfo _setInt;

    private static bool Available =>
        HitMarginCompat.HasNativeXPerfect
        && HitMarginCompat.PerfectMinus >= 0
        && HitMarginCompat.PerfectPlus >= 0;

    /// <summary>仅当开关打开且版本支持三分量时才介入。</summary>
    private static bool Enabled =>
        Main.Settings != null && Main.Settings.ShowResultsPerfectDetail && Available;

    /// <summary>原方法执行前把版式预设压成 Default，让游戏走原生三列同行的首行。</summary>
    static void Prefix()
    {
        if (!Enabled) return;
        try { Suppress(); }
        catch (Exception e) { Loader.Warning($"ResultsPerfectDetail: 压制完美版式失败 ({e.Message})"); }
    }

    /// <summary>把 [+X/-] 括号贴到首行中间那一列（完美列）后面。</summary>
    static void Postfix(scrMarginTracker marginTracker, ref string __result)
    {
        if (!Enabled || marginTracker == null || string.IsNullOrEmpty(__result)) return;
        try { __result = WithDetail(__result, marginTracker); }
        catch (Exception e) { Loader.Warning($"ResultsPerfectDetail: 处理完美明细失败 ({e.Message})"); }
    }

    /// <summary>
    /// Finalizer 一定执行（原方法抛异常也执行），负责还原版式预设。
    /// 还原不能放 Postfix：原方法一旦抛异常，Postfix 会被跳过，玩家设置就永久停在 Default。
    /// </summary>
    static Exception Finalizer(Exception __exception)
    {
        try { Restore(); }
        catch (Exception e) { Loader.Warning($"ResultsPerfectDetail: 还原完美版式失败 ({e.Message})"); }
        return __exception;
    }

    private static void Suppress()
    {
        lock (Sync)
        {
            _depth++;
            if (_depth > 1) return; // 嵌套调用：只在最外层动设置

            object prefs = GetGeneralPrefs();
            if (prefs == null)
            {
                if (!_unavailableLogged)
                {
                    _unavailableLogged = true;
                    Loader.Warning("ResultsPerfectDetail: 读不到 Persistence.generalPrefs，跳过版式压制");
                }
                return;
            }

            _savedPerfectText = InvokeGetInt(prefs, PerfectTextKey, 0);
            InvokeSetInt(prefs, PerfectTextKey, 0); // = HitMarginPerfectTextPreset.Default
            _hasSaved = true;

            if (!_suppressLogged)
            {
                _suppressLogged = true;
                Loader.Log(
                    $"ResultsPerfectDetail: 结算版式临时压成 Default（原值 {_savedPerfectText}），" +
                    "改用原生三列 + [+X/-] 括号");
            }
        }
    }

    private static void Restore()
    {
        lock (Sync)
        {
            if (_depth <= 0) return;
            _depth--;
            if (_depth > 0 || !_hasSaved) return;

            _hasSaved = false;
            object prefs = GetGeneralPrefs();
            if (prefs != null) InvokeSetInt(prefs, PerfectTextKey, _savedPerfectText);
        }
    }

    private static string WithDetail(string result, scrMarginTracker marginTracker)
    {
        int[] counts = marginTracker.hitMarginsCount;
        if (counts == null) return result;

        int minus = HitMarginCompat.Get(counts, HitMarginCompat.PerfectMinus);
        int x = HitMarginCompat.Get(counts, HitMarginCompat.XPerfect);
        int plus = HitMarginCompat.Get(counts, HitMarginCompat.PerfectPlus);
        // 游戏把 Auto 并进完美组显示（ResultWithAuto），明细保持同一口径
        int auto = HitMarginCompat.Get(counts, HitMarginCompat.Auto);

        // 与外部 XPerfect mod 同款：+ 与 - 同用判定绿，中间 X 用游戏当前 XPerfect 配色。
        string detail = " <color=" + PerfectGreen + ">[+" + plus + "/</color>"
                      + "<color=#" + GameCompat.XPerfectHex + ">" + (x + auto) + "</color>"
                      + "<color=" + PerfectGreen + ">/-" + minus + "]</color>";
        if (result.IndexOf(detail, StringComparison.Ordinal) >= 0) return result;

        int newline = result.IndexOf((char)10);
        string firstLine = newline >= 0 ? result.Substring(0, newline) : result;
        string rest = newline >= 0 ? result.Substring(newline) : string.Empty;

        // Default 版式首行 = 「稍快 / 完美 / 稍慢」三列，括号贴中间那列（下标 1）。
        string[] columns = firstLine.Split(new[] { ColumnSeparator }, StringSplitOptions.None);
        if (columns.Length < 2) return result + detail;
        columns[1] += detail;

        return string.Join(ColumnSeparator, columns) + rest;
    }

    /// <summary>Persistence.generalPrefs（PlayerPrefsJson 实例）；取不到返回 null。</summary>
    private static object GetGeneralPrefs()
    {
        if (!_generalPrefsProbed)
        {
            _generalPrefsProbed = true;
            try
            {
                _generalPrefsProperty = typeof(Persistence).GetProperty(
                    "generalPrefs", BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception e)
            {
                Loader.Warning($"ResultsPerfectDetail: 找不到 Persistence.generalPrefs ({e.Message})");
            }
        }
        return _generalPrefsProperty?.GetValue(null);
    }

    private static int InvokeGetInt(object prefs, string key, int defaultValue)
    {
        MethodInfo method = _getInt ??= prefs.GetType().GetMethod(
            "GetInt", BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(int) }, null);
        return method != null
            ? (int)method.Invoke(prefs, new object[] { key, defaultValue })
            : defaultValue;
    }

    private static void InvokeSetInt(object prefs, string key, int value)
    {
        MethodInfo method = _setInt ??= prefs.GetType().GetMethod(
            "SetInt", BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(int) }, null);
        method?.Invoke(prefs, new object[] { key, value });
    }
}