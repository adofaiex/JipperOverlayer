using HarmonyLib;

// =========================================================================
// v141+ feature patches ONLY — mutually exclusive with V136Patches
// Targets: scrMarginTracker (per-player), scrPlayer, etc.
// =========================================================================

namespace JipperOverlayer.Overlayer.Features;

internal static class V141Patches
{
    public static void RegisterAll()
    {
        PatchManager.RegisterPatches(() => Main.Settings.ShowBPM, typeof(ScrPlayerHitBpmPatch));
        // 宽松连击（EL/Perfect± 计入连击）不再是 Jongyeol 模式专属，作为独立开关存在
        PatchManager.RegisterPatches(() => Main.Settings.ShowCombo && !Main.Settings.AllowELCombo, typeof(ScrMarginAddHitComboPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowJudgement,
            typeof(ScrMarginAddHitJudgementPatch),
            typeof(ScrMarginResetPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowProgress || Main.Settings.ShowAccuracy ||
              Main.Settings.ShowXAccuracy || Main.Settings.ShowMusicTime || Main.Settings.ShowMapTime ||
              Main.Settings.ShowCheckpoint || Main.Settings.ShowBest || Main.Settings.ShowProgressBar,
            typeof(ScrMarginCalcAccPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowCombo && Main.Settings.AllowELCombo,
            typeof(ScrMarginAddHitJComboPatch));
        PatchManager.RegisterPatches(() => true, typeof(MistakesManagerSetPlayerCountPatch));
    }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Hit))]
internal static class ScrPlayerHitBpmPatch
{
    static void Postfix() => PatchLogic.BpmPostfix();
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
internal static class ScrMarginAddHitComboPatch
{
    static void Postfix(HitMargin hit) => PatchLogic.ComboPostfix(hit);
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
[HarmonyAfter("XPerfect")]
internal static class ScrMarginAddHitJudgementPatch
{
    static void Postfix() => PatchLogic.JudgementPostfix();
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.Reset))]
internal static class ScrMarginResetPatch
{
    static void Postfix() => PatchLogic.ResetPostfix();
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.CalculatePercentAcc))]
internal static class ScrMarginCalcAccPatch
{
    static void Postfix(scrMarginTracker __instance) => PatchLogic.AccuracyPostfixV141(__instance);
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
internal static class ScrMarginAddHitJComboPatch
{
    static void Postfix(HitMargin hit) => PatchLogic.JComboPostfix(hit);
}

[HarmonyPatch(typeof(scrMistakesManager), nameof(scrMistakesManager.SetPlayerCount))]
internal static class MistakesManagerSetPlayerCountPatch
{
    static void Postfix() => GameLifecycleHelper.GetOverlay()?.OnChangePlayers();
}
