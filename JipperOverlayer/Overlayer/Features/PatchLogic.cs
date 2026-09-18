using HarmonyLib;
using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Features;

internal static class PatchLogic
{
    public static void BpmPostfix() => GameLifecycleHelper.GetOverlay()?.UpdateBPM();

    public static void JudgementPostfix() { if (Main.Settings.ShowJudgement) GameLifecycleHelper.GetOverlay()?.UpdateJudgement(); }

    public static void ResetPostfix() { if (Main.Settings.ShowJudgement) GameLifecycleHelper.GetOverlay()?.UpdateJudgement(); }

    public static void AccuracyPostfixV136() => GameLifecycleHelper.GetOverlay()?.UpdateAccuracy(-1);

    public static void AccuracyPostfixV141(scrMarginTracker __instance)
    {
        int index = VersionSafe.GetPlayerIndex(__instance);
        GameLifecycleHelper.GetOverlay()?.UpdateAccuracy(index);
    }

    public static void ComboPostfix(HitMargin hit)
    {
        var overlay = GameLifecycleHelper.GetOverlay();
        if (overlay == null || !Main.Settings.ShowCombo) return;
        // 不能直接写 HitMargin.Perfect：r150 已无该枚举值，且 PerfectMinus(3) 会与旧值静默撞号
        int h = (int)hit;
        bool isAuto = h == HitMarginCompat.Auto;
        bool isMidspin = h == HitMarginCompat.Midspin;   // r149+；r148 恒为 -1 不会命中
        if (HitMarginCompat.IsPerfectCore(h) || (Main.Settings.EnableAutoCombo && isAuto))
            overlay.UpdateCombo(++GameLifecycleHelper.ComboCount, true);
        else if (isMidspin)
        {
            // r149+ 转中旋不构成玩家判定：连击既不增加也不清零（与游戏 PerfectHitMargins 语义一致）
        }
        else if ((h == HitMarginCompat.VeryEarly || h == HitMarginCompat.VeryLate) && Main.Settings.AllowOrangeCombo)
        {
            // 橙色连击原是 扩展叠加层专属，现拆为独立开关：普通模式下 VeryEarly/VeryLate 也可保连击
            overlay.UpdateCombo(++GameLifecycleHelper.ComboCount, true);
        }
        else if (Main.Settings.EnableAutoCombo || !isAuto)
        {
            overlay.UpdateCombo(GameLifecycleHelper.ComboCount = 0, false);
            overlay.OnNonPerfectHit();
        }
    }

    public static void JComboPostfix(HitMargin hit)
    {
        var overlay = Overlay.Instance;
        if (overlay?.ExtendedOverlay == null) return;
        // 原版 switch 依赖枚举常量，但 r150 的枚举取值整体位移，
        // 因此改为按运行时解析出的语义下标判断。
        int h = (int)hit;
        bool isAuto = h == HitMarginCompat.Auto;
        bool isMidspin = h == HitMarginCompat.Midspin;   // r149+；r148 恒为 -1 不会命中

        if (HitMarginCompat.IsPerfectExtended(h) || (isAuto && Main.Settings.EnableAutoCombo))
        {
            overlay.UpdateCombo(++GameLifecycleHelper.ComboCount, true);
        }
        else if (h == HitMarginCompat.VeryEarly || h == HitMarginCompat.VeryLate)
        {
            if (Main.Settings.AllowOrangeCombo)
                overlay.UpdateCombo(++GameLifecycleHelper.ComboCount, true);
            else
                overlay.UpdateCombo(GameLifecycleHelper.ComboCount = 0, false);
        }
        else if (isMidspin)
        {
            // r149+ 转中旋：保持当前连击不变
        }
        else if (isAuto && !Main.Settings.EnableAutoCombo)
        {
            // 自动播放且未开启 Auto 连击：保持当前连击不变
        }
        else
        {
            overlay.UpdateCombo(GameLifecycleHelper.ComboCount = 0, false);
        }

        if (!HitMarginCompat.IsPerfectCore(h) && !isAuto && !isMidspin)
            overlay.OnNonPerfectHit();
    }
}
