using JipperOverlayer.Overlayer.Util;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 游玩状态判定（与参考工程 PlaybackState 同义，但用本模组既有的强类型 GameRefs）：
/// IsPlaying = 关卡已加载（菜单/结算/读图时为 false），IsPaused = 游戏暂停。
/// </summary>
internal static class PlaybackState
{
    public static bool IsPlaying
    {
        get
        {
            try
            {
                return GameRefs.ControllerInstance != null
                    && GameRefs.ConductorInstance != null
                    && GameRefs.IsGameWorld
                    && GameRefs.LevelMaker?.listFloors is { Count: > 0 };
            }
            catch { return false; }
        }
    }

    public static bool IsPaused
    {
        get
        {
            try { return GameRefs.IsPaused; }
            catch { return false; }
        }
    }
}
