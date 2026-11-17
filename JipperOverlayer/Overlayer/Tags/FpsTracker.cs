using System;
using System.Collections.Generic;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 帧时间戳环形缓冲 + 按窗口平均/节流的 FPS 与帧耗时采样。
/// 移植自参考工程 Overlayer 的 TagImpl/FpsTracker（Overlayer.TagImpl.Performance）：
/// - Tick 由 Main.OnUpdate 每帧喂一次 realtimeSinceStartup；
/// - 窗口查询对环形缓冲二分找窗口内帧数/跨度，平均帧率 = (n-1)/span；
/// - 每个窗口宽度的求值结果按 now + window 节流持有，窗口期内重复求值返回同一个值，
///   这就是 {Fps(500)} 的「每 500ms 才重算一次」的刷新间隔；
/// - 同一帧里不同窗口互不截断（各自二分自己的起点，不共享裁剪游标）。
/// </summary>
internal sealed class FpsTracker
{
    public static FpsTracker Instance { get; private set; }

    private const int MaxWindowMs = 10000;
    private const int MaxSamples = 4096; // 2 的幂，环形索引用位掩码

    // 帧时间戳，最旧在前（起点 = head - count）。每个窗口查询各自二分自己的起点，
    // 互不共享裁剪游标：{Fps(500)} 不会截断同一帧里的 {Fps(5000)}。
    private readonly double[] samples = new double[MaxSamples];
    private int head;
    private int count;
    private readonly Dictionary<int, (double value, double next)> fpsHeld = new();
    private readonly Dictionary<int, (double value, double next)> msHeld = new();

    private FpsTracker() { }

    public static void EnsureInitialized()
    {
        if (Instance == null) Instance = new FpsTracker();
    }

    /// <summary>由 Main.OnUpdate 每帧调用，记录当前帧时间戳。</summary>
    public static void Tick()
    {
        EnsureInitialized();
        var t = Instance;
        t.samples[t.head] = Time.realtimeSinceStartup;
        t.head = (t.head + 1) & (MaxSamples - 1);
        if (t.count < MaxSamples) t.count++;
    }

    private double Sample(int i) => samples[(head - count + i) & (MaxSamples - 1)];

    // 窗口内的采样数与跨度：now - t <= window（与参考工程环形裁剪同一规则）。
    private bool Window(int windowMs, out long n, out double span)
    {
        double now = Time.realtimeSinceStartup;
        double window = windowMs / 1000d;
        int lo = 0, hi = count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (now - Sample(mid) > window) lo = mid + 1;
            else hi = mid;
        }
        n = count - lo;
        span = n < 2 ? 0d : Sample(count - 1) - Sample(lo);
        return n >= 2 && span >= 0.000001;
    }

    /// <summary>窗口平均 FPS；windowMs &lt;= 0 时逐帧 1/unscaledDeltaTime（与 {Fps} 旧语义一致）。</summary>
    public static double Fps(double windowMs)
    {
        EnsureInitialized();
        return Instance.FpsCore(windowMs);
    }

    /// <summary>窗口平均帧耗时（毫秒）；windowMs &lt;= 0 时逐帧 unscaledDeltaTime * 1000。</summary>
    public static double FrameTime(double windowMs)
    {
        EnsureInitialized();
        return Instance.FrameTimeCore(windowMs);
    }

    private double FpsCore(double windowMs)
    {
        if (windowMs <= 0)
        {
            float dt = Time.unscaledDeltaTime;
            return dt > 0f ? 1d / dt : 0d;
        }
        double now = Time.realtimeSinceStartup;
        int w = WindowMs(windowMs);
        if (!fpsHeld.TryGetValue(w, out var e) || now >= e.next)
        {
            if (fpsHeld.Count > 32) fpsHeld.Clear();
            double value = WindowedFps(w);
            fpsHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    private double FrameTimeCore(double windowMs)
    {
        if (windowMs <= 0)
        {
            return Time.unscaledDeltaTime * 1000d;
        }
        double now = Time.realtimeSinceStartup;
        int w = WindowMs(windowMs);
        if (!msHeld.TryGetValue(w, out var e) || now >= e.next)
        {
            if (msHeld.Count > 32) msHeld.Clear();
            double value = WindowedMs(w);
            msHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    private static int WindowMs(double windowMs)
        => (int)Math.Min(Math.Max(windowMs, 1d), MaxWindowMs);

    private double WindowedFps(int windowMs)
        => Window(windowMs, out long n, out double span) ? (n - 1) / span : 0d;

    private double WindowedMs(int windowMs)
        => Window(windowMs, out long n, out double span) ? span / (n - 1) * 1000d : 0d;
}
