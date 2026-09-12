using HarmonyLib;
using JipperOverlayer.Overlayer.Util;
using MonsterLove.StateMachine;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace JipperOverlayer.Overlayer.Features;

internal static class GameLifecyclePatches
{
    public static void Register()
    {
        PatchManager.RegisterPatches(() => true,
            typeof(ScnGamePlayPatch),
            typeof(PressToStartShowTextPatch),
            typeof(UIControllerWipeToBlackPatch),
            typeof(ScnEditorResetScenePatch),
            typeof(ControllerStartLoadingScenePatch),
            typeof(ControllerChangeStatePatch)
        );

        PatchManager.RegisterPatches(() => Main.Settings.ShowProgress || Main.Settings.ShowAccuracy ||
              Main.Settings.ShowXAccuracy || Main.Settings.ShowMusicTime || Main.Settings.ShowMapTime ||
              Main.Settings.ShowCheckpoint || Main.Settings.ShowBest || Main.Settings.ShowProgressBar ||
              Main.Settings.ShowTimingScale || Main.Settings.ShowAttempt || Main.Settings.ShowFullAttempt ||
              Main.Settings.ShowState || Main.Settings.ShowDeath || Main.Settings.ShowStart || Main.Settings.ShowTiming,
            typeof(PlanetMoveToNextFloorPatch));

        // 原先捆绑在 Jongyeol 总开关下的三个补丁，改为各自按所属设置独立门控
        PatchManager.RegisterPatches(() => Main.Settings.HideDebugText, typeof(ScrShowIfDebugUpdatePatch));
        PatchManager.RegisterPatches(() => Main.Settings.RepositionAutoText, typeof(ScrShowIfDebugAwakePatch));
        // auto 切换会影响 State 文本与 checkAuto 类元素（精度/X精度/检查点/最佳）的可见性
        PatchManager.RegisterPatches(() => Main.Settings.ShowState || Main.Settings.ShowAccuracy ||
              Main.Settings.ShowXAccuracy || Main.Settings.ShowCheckpoint || Main.Settings.ShowBest,
            typeof(RdcSetAutoPatch));

        // Jongyeol 计时取自 scrMisc 的判定函数，目标按游戏版本挑选：
        //   r149+（r150 系）：GetHitMargin 被移除，改为 GetHitMarginInDeg / GetHitMarginInSec
        //   r141-r148：      GetHitMargin(hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale)
        // 注册不存在的目标只会被 PatchManager 吞成一条警告，因此必须按版本注册，
        // 并在日志中标注版本区间，便于确认分支是否挑对。
        //
        // 注意：这里无条件挂载（() => true），开关判定在 JongyeolModule.UpdateTiming 内部做。
        // 若按 ShowTiming 门控注册，补丁是否挂载就取决于注册/刷新时刻的开关状态——
        // 配置加载顺序、在主菜单还是局内开开关、哪个开关触发过 RefreshPatches 都会影响，
        // 任何一环没对上补丁就静默缺席，表现为「Timing 冻结，拨别的开关才更新」。
        // 关闭时每次命中只多一个 bool 早退，代价可忽略。
        if (HitMarginCompat.HasNativeXPerfect)
            PatchManager.RegisterPatches(() => true, "r149+",
                typeof(ScrMiscGetHitMarginInDegPatch),
                typeof(ScrMiscGetHitMarginInSecPatch));
        else
            PatchManager.RegisterPatches(() => true, "r141-r148",
                typeof(ScrMiscGetHitMarginPatch));

        // Always capture beta watermark reference when it awakens
        PatchManager.RegisterPatches(() => true, typeof(BetaWatermarkCapturePatch));
    }

}

// ========== Lifecycle ==========

[HarmonyPatch(typeof(StateBehaviour), nameof(StateBehaviour.ChangeState), [typeof(Enum)])]
internal static class ControllerChangeStatePatch
{
    static void Postfix(Enum newState)
    {
        switch ((States)newState)
        {
            case States.Fail2: GameLifecycleHelper.GetOverlay()?.Death(); break;
            case States.Won: GameLifecycleHelper.GetOverlay()?.Clear(); break;
        }
    }
}

[HarmonyPatch(typeof(scnGame), nameof(scnGame.Play))]
internal static class ScnGamePlayPatch
{
    static void Postfix(int seqID)
    {
        if (GCS.practiceMode) return;
        GameLifecycleHelper.GetOverlay()?.Show(seqID);
    }
}

[HarmonyPatch(typeof(scrPressToStart), nameof(scrPressToStart.ShowText))]
internal static class PressToStartShowTextPatch
{
    static void Postfix()
    {
        if (!GCS.practiceMode && GameRefs.GameInstance != null) return;
        GameLifecycleHelper.GetOverlay()?.Show(GameRefs.CurrentSeqID);
    }
}

[HarmonyPatch(typeof(scrUIController), nameof(scrUIController.WipeToBlack))]
internal static class UIControllerWipeToBlackPatch
{
    static void Postfix() => GameLifecycleHelper.GetOverlay()?.Hide();
}

[HarmonyPatch(typeof(scnEditor), "ResetScene")]
internal static class ScnEditorResetScenePatch
{
    static void Postfix() => GameLifecycleHelper.GetOverlay()?.Hide();
}

[HarmonyPatch(typeof(scrController), nameof(scrController.StartLoadingScene))]
internal static class ControllerStartLoadingScenePatch
{
    static void Postfix() => GameLifecycleHelper.GetOverlay()?.Hide();
}

// ========== Progress / Timing / Attempt (version-agnostic, merged) ==========

[HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
internal static class PlanetMoveToNextFloorPatch
{
    static void Postfix(scrPlanet __instance)
    {
        var overlay = GameLifecycleHelper.GetOverlay();
        if (overlay == null) return;
        var s = Main.Settings;
        if (s.ShowProgress || s.ShowAccuracy || s.ShowXAccuracy || s.ShowMusicTime || s.ShowMapTime || s.ShowCheckpoint || s.ShowBest || s.ShowProgressBar)
            overlay.UpdateProgress(__instance);
        if (s.ShowTimingScale) overlay.UpdateTimingScale();
        if (s.ShowAttempt || s.ShowFullAttempt) overlay.UpdateAttempts();
    }
}

// ========== Jongyeol UI (version-agnostic) ==========

[HarmonyPatch(typeof(scrShowIfDebug), "Update")]
internal static class ScrShowIfDebugUpdatePatch
{
    static bool Prefix(Text ___txt)
    {
        if (Main.Settings.HideDebugText) { ___txt.enabled = false; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(scrShowIfDebug), "Awake")]
internal static class ScrShowIfDebugAwakePatch
{
    static void Postfix(scrShowIfDebug __instance)
    {
        if (!Main.Settings.RepositionAutoText) return;
        var t = __instance.GetComponent<RectTransform>();
        if (t) t.anchoredPosition = new Vector2(300, t.anchoredPosition.y);
    }
}

[HarmonyPatch(typeof(RDC), nameof(RDC.auto), MethodType.Setter)]
internal static class RdcSetAutoPatch
{
    static void Postfix()
    {
        if (!GameRefs.IsScnGame) return;
        Overlay.Instance?.Jongyeol?.SetupLocation();
    }
}

// 用字符串而非 nameof 指定目标：nameof(scrMisc.GetHitMargin) 以 r150 为基线编译时直接 CS0117，
// 且该成员在 r150 已被移除。参数名必须与 r148 真实参数名一致（Harmony 按名注入）。
[HarmonyPatch(typeof(scrMisc), "GetHitMargin")]
internal static class ScrMiscGetHitMarginPatch
{
    static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch)
    {
        float angle = (hitangle - refangle) * (isCW ? 1 : -1) * 57.29578f;
        float timing = angle / 180 / bpmTimesSpeed / conductorPitch * 60000;
        Overlay.Instance?.Jongyeol?.UpdateTiming(timing);
    }
}

// r150：GetHitMarginInDeg(Difficulty, float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, double)
[HarmonyPatch(typeof(scrMisc), "GetHitMarginInDeg")]
internal static class ScrMiscGetHitMarginInDegPatch
{
    static void Postfix(float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch)
    {
        // 与 r150 版 GetHitMarginInDeg 内部一致：target = (hitAngle - refAngle) * (clockwise ? 1 : -1) * 57.29578f
        float angle = (hitAngle - refAngle) * (clockwise ? 1 : -1) * 57.29578f;
        float timing = angle / 180 / floorBpm / conductorPitch * 60000;
        Overlay.Instance?.Jongyeol?.UpdateTiming(timing);
    }
}

// r150：GetHitMarginInSec(Difficulty, double timeDiff, float floorBpm, float conductorPitch, double)
// 异步输入路径走这里；timeDiff 单位是秒，timeDiff * 1000 与角度换算公式等价（deg = t·pitch·bpm·3）。
[HarmonyPatch(typeof(scrMisc), "GetHitMarginInSec")]
internal static class ScrMiscGetHitMarginInSecPatch
{
    static void Postfix(double timeDiff)
    {
        Overlay.Instance?.Jongyeol?.UpdateTiming((float)(timeDiff * 1000.0));
    }
}

// ========== Beta watermark capture ==========

[HarmonyPatch(typeof(scrEnableIfBeta), "Awake")]
internal static class BetaWatermarkCapturePatch
{
    static void Postfix(scrEnableIfBeta __instance)
    {
        if (__instance.setBuildText)
        {
            Overlay.BetaWatermark = __instance;
            var rt = __instance.GetComponent<RectTransform>();
            if (rt != null)
                Overlay.BetaWatermarkOriginalPos = rt.anchoredPosition;
        }
    }
}

// ========== Helper ==========

internal static class GameLifecycleHelper
{
    public static int ComboCount;

    public static Overlay GetOverlay() => Overlay.Instance;
}