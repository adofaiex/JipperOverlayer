using HarmonyLib;

// =========================================================================
// v136 feature patches ONLY — mutually exclusive with V141Patches
// Targets: scrMistakesManager (static/singleton), scrController, etc.
// =========================================================================

namespace JipperOverlayer.Overlayer.Features;

internal static class V136Patches
{
    public static void RegisterAll()
    {
        PatchManager.RegisterPatches(() => Main.Settings.ShowBPM, typeof(ScrControllerHitBpmPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowCombo && !Main.Settings.AllowELCombo, typeof(ScrMistakesAddHitComboPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowJudgement,
            typeof(ScrMistakesAddHitJudgementPatch),
            typeof(ScrMistakesResetPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowProgress || Main.Settings.ShowAccuracy ||
              Main.Settings.ShowXAccuracy || Main.Settings.ShowMusicTime || Main.Settings.ShowMapTime ||
              Main.Settings.ShowCheckpoint || Main.Settings.ShowBest || Main.Settings.ShowProgressBar,
            typeof(ScrMistakesCalcAccPatch));
        PatchManager.RegisterPatches(() => Main.Settings.ShowCombo && Main.Settings.AllowELCombo,
            typeof(ScrMistakesAddHitJComboPatch));
    }
}

[HarmonyPatch(typeof(scrController), "Hit")]
internal static class ScrControllerHitBpmPatch
{
    static void Postfix() => PatchLogic.BpmPostfix();
}

[HarmonyPatch(typeof(scrMistakesManager), "AddHit")]
internal static class ScrMistakesAddHitComboPatch
{
    static void Postfix(HitMargin hit) => PatchLogic.ComboPostfix(hit);
}

[HarmonyPatch(typeof(scrMistakesManager), "AddHit")]
[HarmonyAfter("XPerfect")]
internal static class ScrMistakesAddHitJudgementPatch
{
    static void Postfix() => PatchLogic.JudgementPostfix();
}

[HarmonyPatch(typeof(scrMistakesManager), "Reset")]
internal static class ScrMistakesResetPatch
{
    static void Postfix() => PatchLogic.ResetPostfix();
}

[HarmonyPatch(typeof(scrMistakesManager), "CalculatePercentAcc")]
internal static class ScrMistakesCalcAccPatch
{
    static void Postfix() => PatchLogic.AccuracyPostfixV136();
}

[HarmonyPatch(typeof(scrMistakesManager), "AddHit")]
internal static class ScrMistakesAddHitJComboPatch
{
    static void Postfix(HitMargin hit) => PatchLogic.JComboPostfix(hit);
}
