using System;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Util;

/// <summary>
/// 判定时间窗口（±x ms）计算器
/// 直接复用游戏函数 scrMisc.GetAdjustedAngleBoundaryInDeg，保证与真实判定一致
/// 同时增加对XPerfect的支持，但手动计算
/// </summary>
internal static class TimingWindowCalculator
{
	public readonly struct Result
	{
		public readonly float XPerfectMs;
		public readonly float PerfectMs;
		public readonly float GreatMs;
		public readonly float GoodMs;
		public readonly bool XPerfectValid;

		public Result(float perfectMs, float xPerfectMs, float greatMs, float goodMs, bool xPerfectValid)
		{
			PerfectMs = perfectMs;
			XPerfectMs = xPerfectMs;
			GreatMs = greatMs;
			GoodMs = goodMs;
			XPerfectValid = xPerfectValid;
		}

		public readonly bool Valid => PerfectMs > 0 || GreatMs > 0 || GoodMs > 0;
	}

	// 输入缓存：Calculate 每帧被调用，输入未变时直接复用上次结果，跳过游戏函数调用
	private static double _cacheBpm = -1.0, _cachePitch = -1.0;
	private static float _cacheMargin = -1f, _cacheSpeedTrial = -1f, _cacheCounted = -1f;
	private static int _cacheDifficulty = -1;
	private static bool _cacheXp, _warned;
	private static Result _cache;

	public static Result Calculate(scrFloor floor)
	{
		if (floor == null) return default;
		var conductor = GameRefs.ConductorInstance;
		var ctrl = GameRefs.ControllerInstance;
		if (conductor == null || ctrl == null) return default;

		double bpmTimesSpeed = conductor.bpm * VersionSafe.GetPlanetSpeed(ctrl);
		double conductorPitch = GameRefs.SongPitch;
		float marginScale = (float)floor.marginScale;

		if (bpmTimesSpeed <= 0 || conductorPitch <= 0) return default;

		// GetAdjustedAngleBoundaryInDeg 除参数外还读取 GCS.difficulty / currentSpeedTrial /
		// HITMARGIN_COUNTED（isMobile 运行期不变），这些隐藏输入必须一并纳入缓存键
		int difficulty = (int)GCS.difficulty;
		float speedTrial = GCS.currentSpeedTrial;
		float counted = GCS.HITMARGIN_COUNTED;
		bool xp = HitMarginCompat.XPerfectDisplayAvailable;

		if (bpmTimesSpeed == _cacheBpm && conductorPitch == _cachePitch && marginScale == _cacheMargin &&
			difficulty == _cacheDifficulty && speedTrial == _cacheSpeedTrial && counted == _cacheCounted && xp == _cacheXp)
			return _cache;

		// 角度(度) → 时间(秒)：deg = time * pitch * bpm * speed * 3 → time = deg / (3 * bpm * speed * pitch)
		double denom = 3.0 * bpmTimesSpeed * conductorPitch;

		try
		{
			// r148: static double GetAdjustedAngleBoundaryInDeg(HitMarginGeneral, double, double, double)
			// r150: static 结构体 GetAdjustedAngleBoundaryInDeg(Difficulty, double, double, double)
			// 首参与返回类型同时变化，直接调用在另一版本会 MissingMethodException（被 catch 吞掉后
			// 表现为判定时间窗静默消失），因此统一走 GameCompat 的运行时签名探测。
			if (!GameCompat.TryGetAngleBoundaries(bpmTimesSpeed, conductorPitch, marginScale,
					out double countedDeg, out double perfectDeg, out double pureDeg, out double xPerfectDeg))
			{
				// 探测/调用失败不写缓存，每帧都会重试——沿用原有的只警告一次机制
				if (!_warned) { _warned = true; Loader.Warning("TimingWindow: GetAdjustedAngleBoundaryInDeg 不可用，判定时间窗停用"); }
				return default;
			}

			float perfectMs = AngleToMs(pureDeg, denom);      // Pure 边界 → Perfect 档
			float greatMs = AngleToMs(perfectDeg, denom);     // Perfect 边界 → Great 档
			float goodMs = AngleToMs(countedDeg, denom);      // Counted 边界 → Good 档

			float xPerfectMs = 0f;
			bool xPerfectValid = false;
			if (HitMarginCompat.HasNativeXPerfect)
			{
				// r150 热修（2026-09-11 晚）后的 XPerfect 语义：与普通判定边界一致——
				// XPerfect = Max(12.5° × marginMult, deg(16.67ms × pitch))，
				// 随判定倍率放大、下限换算用真实 pitch；初版 r150 的「绝对时间指标」设计
				// （不乘倍率、pitch 写死 1.0、27.5ms 封顶）已被官方撤销，且两路径不一致
				// 一并修复（useAbsoluteTime 被移除，角度/时间路径严格互逆）。
				// 有效窗口 = max(16.67ms, 4166.67·m/(bpm·pitch) ms)，低 BPM 下比初版更宽。
				// 本 mod 直接读游戏函数返回值，公式变化自动跟随，无需改代码。
				if (xPerfectDeg > 0.0)
				{
					xPerfectMs = AngleToMs(xPerfectDeg, denom);
					xPerfectValid = xPerfectMs > 0;
				}
			}
			else if (xp)
			{
				// r148：外部 XPerfect mod，大 p 边界是确定公式；仅当已安装并启用时展示
				double xBoundaryDeg = XPerfectBoundaryDeg(bpmTimesSpeed, conductorPitch, marginScale);
				xPerfectMs = AngleToMs(xBoundaryDeg, denom);
				xPerfectValid = xPerfectMs > 0;
			}

			_cacheBpm = bpmTimesSpeed; _cachePitch = conductorPitch; _cacheMargin = marginScale;
			_cacheDifficulty = difficulty; _cacheSpeedTrial = speedTrial; _cacheCounted = counted; _cacheXp = xp;
			_cache = new Result(perfectMs, xPerfectMs, greatMs, goodMs, xPerfectValid);
			_warned = false;
			return _cache;
		}
		catch (Exception e)
		{
			// 每帧调用，持续失败只警告一次，避免日志刷屏
			if (!_warned) { _warned = true; Loader.Warning($"TimingWindow: 游戏函数调用失败 ({e.Message})"); }
			return default;
		}
	}

	// 静态方法而非捕获 denom 的局部 lambda：lambda 每次调用都会分配一个闭包对象
	private static float AngleToMs(double deg, double denom) => (float)(deg * 1000.0 / denom);

	/// <summary>XPerfect 大 p 边界角度（度）：max(15° × margin, 16.67ms 换算的角度)</summary>
	private static double XPerfectBoundaryDeg(double bpmTimesSpeed, double conductorPitch, float marginScale)
	{
		const double xPerfectBaseDeg = 15.0;
		const double xPerfectMinTimeSec = 0.01667;

		double xMinTimeDeg = scrMisc.TimeToAngleInRad(xPerfectMinTimeSec, bpmTimesSpeed, conductorPitch, false) * Mathf.Rad2Deg;
		return Math.Max(xPerfectBaseDeg * marginScale, xMinTimeDeg);
	}
}
