using System;
using System.Reflection;
using UnityModManagerNet;

namespace JipperOverlayer
{
    public static class XPerfectIntegration
    {
        private static Func<int> _getXPerfect, _getPlusPerfect, _getMinusPerfect;
        private static Func<int, int> _getPlayerXPerfect, _getPlayerPlusPerfect, _getPlayerMinusPerfect;
        public static bool IsAvailable { get; private set; }

        public static int PlusPerfect => _getPlusPerfect?.Invoke() ?? 0;
        public static int XPerfect => _getXPerfect?.Invoke() ?? 0;
        public static int MinusPerfect => _getMinusPerfect?.Invoke() ?? 0;

        public static int GetPlayerXPerfect(int player) => _getPlayerXPerfect?.Invoke(player) ?? 0;
        public static int GetPlayerPlusPerfect(int player) => _getPlayerPlusPerfect?.Invoke(player) ?? 0;
        public static int GetPlayerMinusPerfect(int player) => _getPlayerMinusPerfect?.Invoke(player) ?? 0;

        public static (int plus, int x, int minus) GetPlayer(int player) =>
            (GetPlayerPlusPerfect(player), GetPlayerXPerfect(player), GetPlayerMinusPerfect(player));

        private static bool _subscribedToToggle;
        private static bool _notInstalled; // XPerfect 未安装:运行时无法装上,永久跳过探测
        private static DateTime _nextProbeAt = DateTime.MinValue;
        private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>懒加载：在需要时调用（如 UpdateJudgement 或 OnUpdate）。
        /// 未安装 → 永久短路;安装未启用 → 每 250ms 探测一次而非每帧;已启用使用 → IsAvailable 短路,仅关闭时经 OnToggle 恢复。</summary>
        public static void EnsureInitialized()
        {
            if (IsAvailable || _notInstalled) return;
            if (DateTime.UtcNow < _nextProbeAt) return;
            _nextProbeAt = DateTime.UtcNow + ProbeInterval;
            try
            {
                TryCache();
                SubscribeToToggle();
            }
            catch (Exception e)
            {
                // UMM 程序集不存在（纯 MelonLoader 安装）或探测失败：
                // 本会话永久停用探测，避免每帧抛异常。
                _notInstalled = true;
                Loader.Log($"[XPerfectIntegration] Probe unavailable, integration disabled for this session ({e.GetType().Name}: {e.Message})");
            }
        }

        private static void TryCache()
        {
            if (IsAvailable) return;
            var mod = UnityModManager.FindMod("XPerfect");
            if (mod == null)
            {
                _notInstalled = true;
                Loader.Log("[XPerfectIntegration] XPerfect not found — probe disabled for this session.");
                return;
            }
            if (!mod.Enabled || !mod.Active || mod.Assembly == null) return; // 装了但没开:随时可能启用,继续探测
            CacheDelegates(mod.Assembly);
            Overlayer.Overlay.Instance?.UpdateJudgement();
        }

        private static void CacheDelegates(Assembly asm)
        {
            try
            {
                var type = asm.GetType("XPerfect.AccuracyState");
                if (type == null) return;

                var xProp = type.GetProperty("XPerfectCount", BindingFlags.Public | BindingFlags.Static);
                var plusProp = type.GetProperty("PlusPerfectCount", BindingFlags.Public | BindingFlags.Static);
                var minusProp = type.GetProperty("MinusPerfectCount", BindingFlags.Public | BindingFlags.Static);
                if (xProp == null || plusProp == null || minusProp == null) return;

                _getXPerfect = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), xProp.GetGetMethod());
                _getPlusPerfect = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), plusProp.GetGetMethod());
                _getMinusPerfect = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), minusProp.GetGetMethod());

                // Per-player methods (nullable — optional for single-player only overlays)
                var mPlayerX = type.GetMethod("GetPlayerXPerfectCount", BindingFlags.Public | BindingFlags.Static);
                var mPlayerPlus = type.GetMethod("GetPlayerPlusPerfectCount", BindingFlags.Public | BindingFlags.Static);
                var mPlayerMinus = type.GetMethod("GetPlayerMinusPerfectCount", BindingFlags.Public | BindingFlags.Static);
                if (mPlayerX != null && mPlayerPlus != null && mPlayerMinus != null)
                {
                    _getPlayerXPerfect = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), mPlayerX);
                    _getPlayerPlusPerfect = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), mPlayerPlus);
                    _getPlayerMinusPerfect = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), mPlayerMinus);
                }
                else
                {
                    // v136 XPerfect 没有 per-player 方法，回退到主值
                    _getPlayerXPerfect = _ => XPerfect;
                    _getPlayerPlusPerfect = _ => PlusPerfect;
                    _getPlayerMinusPerfect = _ => MinusPerfect;
                }

                IsAvailable = true;
                Loader.Log("[XPerfectIntegration] Integration ready.");
            }
            catch (Exception ex)
            {
                Loader.Log($"[XPerfectIntegration] Failed: {ex}");
            }
        }

        private static void SubscribeToToggle()
        {
            if (_subscribedToToggle) return;
            var mod = UnityModManager.FindMod("XPerfect");
            if (mod == null) return;
            mod.OnToggle += OnXPerfectToggle;
            _subscribedToToggle = true;
        }

        private static bool OnXPerfectToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                bool wasAvailable = IsAvailable;
                TryCache();
                // 首次探测成功：刷新判定窗口（X 行）——TryCache 内部已刷新 Judgement
                if (IsAvailable && !wasAvailable)
                    RefreshBpmOverlay();
                // 不在这里复位 _subscribedToToggle：UMM 的 OnToggle 事件不会因为
                // 逻辑标志复位而自动移除，反复置 false 只会让下次探测重复 += 同一个
                // 回调，长期启停后 handler 数量不断增长。
            }
            else
            {
                if (IsAvailable)
                {
                    IsAvailable = false;
                    _getXPerfect = _getPlusPerfect = _getMinusPerfect = null;
                    _getPlayerXPerfect = _getPlayerPlusPerfect = _getPlayerMinusPerfect = null;
                    Loader.Log("[XPerfectIntegration] XPerfect disabled.");
                    // 同时刷新判定计数与判定窗口（隐藏 X 行）
                    var o = Overlayer.Overlay.Instance;
                    if (o != null) { o.UpdateJudgement(); RefreshBpm(o); }
                }
            }
            return true;
        }

        /// <summary>主模组卸载时清理探测状态；UMM 的 OnToggle 订阅保留，重新启用后可再次探测。</summary>
        public static void ResetForModDisable()
        {
            IsAvailable = false;
            _getXPerfect = _getPlusPerfect = _getMinusPerfect = null;
            _getPlayerXPerfect = _getPlayerPlusPerfect = _getPlayerMinusPerfect = null;
            _nextProbeAt = DateTime.MinValue;
        }

        /// <summary>使 BPM 缓存失效并立即重绘（判定窗口 X 行随 XPerfect 可用性变化）。</summary>
        private static void RefreshBpmOverlay() => RefreshBpm(Overlayer.Overlay.Instance);

        private static void RefreshBpm(Overlayer.Overlay o)
        {
            if (o == null) return;
            o.DirtyBpmCache();
            o.UpdateBPM();
        }
    }
}
