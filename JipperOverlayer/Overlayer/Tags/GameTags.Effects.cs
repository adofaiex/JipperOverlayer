using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using JipperOverlayer.Overlayer.Util;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 从参考工程 Overlayer 移植的特效 tag：
/// - {ColorRange(tag, min, max, fromHex, toHex, ease?, maxLength?)}：按数值在两色间插值，输出 hex（不含 #）
/// - {EasedValue(tag, digits?, speed?, ease?)}：数值缓动跟随（平滑跳变）
/// - {MovingMan(tag, start, end, default, speed, invert?, ease?)}：数值变化瞬间在两值间做缓动
/// 三者的第一个参数都是「tag 表达式」（{Progress} 里去掉花括号的部分，如 Progress、Accuracy(1)、XScore:0），
/// 求值结果按表达式缓存，避免每帧重复解析。
/// 另含若干低成本数值 tag：Difficulty / LastXScore / MaxXAccuracy / SessionAttempts / FrameTime。
/// </summary>
internal static partial class GameTags
{
    // ===== 缓动库（自参考工程 O5Ease 完整移植）=====

    internal enum Ease
    {
        Linear, InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce,
    }

    private static float EaseValue(float progress, Ease ease) => ease switch
    {
        Ease.InSine => 1f - Mathf.Cos(progress * Mathf.PI * 0.5f),
        Ease.OutSine => Mathf.Sin(progress * Mathf.PI * 0.5f),
        Ease.InOutSine => -(Mathf.Cos(Mathf.PI * progress) - 1f) * 0.5f,
        Ease.InQuad => progress * progress,
        Ease.OutQuad => 1f - ((1f - progress) * (1f - progress)),
        Ease.InOutQuad => progress < 0.5f ? 2f * progress * progress : 1f - (Mathf.Pow((-2f * progress) + 2f, 2f) / 2f),
        Ease.InCubic => progress * progress * progress,
        Ease.OutCubic => 1f - Mathf.Pow(1f - progress, 3f),
        Ease.InOutCubic => progress < 0.5f ? 4f * progress * progress * progress : 1f - (Mathf.Pow((-2f * progress) + 2f, 3f) / 2f),
        Ease.InQuart => progress * progress * progress * progress,
        Ease.OutQuart => 1f - Mathf.Pow(1f - progress, 4f),
        Ease.InOutQuart => progress < 0.5f ? 8f * progress * progress * progress * progress : 1f - Mathf.Pow(-2f * progress + 2f, 4f) / 2f,
        Ease.InQuint => progress * progress * progress * progress * progress,
        Ease.OutQuint => 1f - Mathf.Pow(1f - progress, 5f),
        Ease.InOutQuint => progress < 0.5f ? 16f * progress * progress * progress * progress * progress : 1f - Mathf.Pow(-2f * progress + 2f, 5f) / 2f,
        Ease.InExpo => progress <= 0f ? 0f : Mathf.Pow(2f, 10f * progress - 10f),
        Ease.OutExpo => progress >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * progress),
        Ease.InOutExpo => progress <= 0f ? 0f : progress >= 1f ? 1f : progress < 0.5f ? Mathf.Pow(2f, 20f * progress - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * progress + 10f)) / 2f,
        Ease.InCirc => 1f - Mathf.Sqrt(1f - progress * progress),
        Ease.OutCirc => Mathf.Sqrt(1f - Mathf.Pow(progress - 1f, 2f)),
        Ease.InOutCirc => progress < 0.5f ? (1f - Mathf.Sqrt(1f - 4f * progress * progress)) / 2f : (Mathf.Sqrt(1f - Mathf.Pow(-2f * progress + 2f, 2f)) + 1f) / 2f,
        Ease.InBack => 2.70158f * progress * progress * progress - 1.70158f * progress * progress,
        Ease.OutBack => 1f + (2.70158f * Mathf.Pow(progress - 1f, 3f)) + (1.70158f * Mathf.Pow(progress - 1f, 2f)),
        Ease.InOutBack => progress < 0.5f ? 4f * progress * progress * ((3.59491f * 2f * progress) - 2.59491f) / 2f : (Mathf.Pow(2f * progress - 2f, 2f) * ((3.59491f * (2f * progress - 2f)) + 2f) + 2f) / 2f,
        Ease.InElastic => progress <= 0f ? 0f : progress >= 1f ? 1f : -Mathf.Pow(2f, 10f * progress - 10f) * Mathf.Sin((10f * progress - 10.75f) * 2.0944f),
        Ease.OutElastic => progress <= 0f ? 0f : progress >= 1f ? 1f : Mathf.Pow(2f, -10f * progress) * Mathf.Sin((10f * progress - 0.75f) * 2.0944f) + 1f,
        Ease.InOutElastic => progress <= 0f ? 0f : progress >= 1f ? 1f : progress < 0.5f ? -(Mathf.Pow(2f, 20f * progress - 10f) * Mathf.Sin((20f * progress - 11.125f) * 1.39626f)) / 2f : Mathf.Pow(2f, -20f * progress + 10f) * Mathf.Sin((20f * progress - 11.125f) * 1.39626f) / 2f,
        Ease.InBounce => 1f - BounceOut(1f - progress),
        Ease.OutBounce => BounceOut(progress),
        Ease.InOutBounce => progress < 0.5f ? (1f - BounceOut(1f - 2f * progress)) / 2f : (1f + BounceOut(2f * progress - 1f)) / 2f,
        _ => progress,
    };

    private static float BounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    private static Ease ParseEase(string raw, Ease fallback)
    {
        if (string.IsNullOrEmpty(raw)) return fallback;
        try { return Enum.TryParse<Ease>(raw.Trim(), true, out var e) ? e : fallback; }
        catch { return fallback; }
    }

    /// <summary>效果面板预览用：按名字采样缓动曲线（0..1）。</summary>
    internal static float SampleEase(string raw, float t) => EaseValue(t, ParseEase(raw, Ease.Linear));

    // ===== tag 表达式读取（ColorRange / EasedValue / MovingMan 的第一个参数）=====

    private sealed class TagSource
    {
        public readonly bool Valid;
        public readonly string Name;
        public readonly string[] Args;
        public TagSource(bool valid, string name, string[] args) { Valid = valid; Name = name; Args = args; }
    }

    private static readonly Dictionary<string, TagSource> TagSourceCache = new(StringComparer.Ordinal);

    /// <summary>解析并求值一个 tag 表达式（"Progress"、"Accuracy(1)"、"XScore:0"）为 double。</summary>
    private static bool TryReadTagNumber(string expression, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(expression)) return false;
        expression = expression.Trim();
        if (!TagSourceCache.TryGetValue(expression, out var src))
        {
            if (TagSourceCache.Count >= 256) TagSourceCache.Clear();
            TagSourceCache[expression] = src = ParseTagSource(expression);
        }
        if (!src.Valid) return false;
        try
        {
            if (!TagManager.TryInvoke(src.Name, src.Args, out var result) || result == null) return false;
            if (result is double d) { value = d; return true; }
            if (result is float f) { value = f; return true; }
            if (result is int i) { value = i; return true; }
            if (result is long l) { value = l; return true; }
            if (result is bool b) { value = b ? 1 : 0; return true; }
            return double.TryParse(result.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        catch { return false; }
    }

    private static TagSource ParseTagSource(string expression)
    {
        // 形如 Name / Name:format / Name(args) / Name(args):format —— format 不影响取数，先切掉。
        // 参数拆分走 TagArgParser（引号/嵌套括号感知），与模板引擎同一套规则。
        string body = expression;
        int colon = TopLevelColon(body);
        if (colon > 0) body = body.Substring(0, colon);
        if (!TagArgParser.TrySplit(body, out var name, out var args)) return new TagSource(false, null, null);
        if (string.IsNullOrEmpty(name)) return new TagSource(false, null, null);
        return new TagSource(true, name, args.Length > 0 ? args : null);
    }

    /// <summary>第一个不在参数括号内的 ':'（format 分隔符）。</summary>
    private static int TopLevelColon(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ':' && depth == 0) return i;
        }
        return -1;
    }

    // ===== 动画状态 =====

    private sealed class AnimState
    {
        public double Previous;
        public double Target;
        public double StartedAt;
    }

    private static readonly Dictionary<string, AnimState> AnimStates = new(StringComparer.Ordinal);

    /// <summary>只负责取/建状态；目标的推进由各特效 tag 自己做——
    /// EasedValue 需要先算出当前插值位置再切换目标（Previous = 当前位置），
    /// 提前写 Target 会破坏「从动画中途续走」的语义。</summary>
    private static AnimState GetAnimState(string key, double value)
    {
        if (!AnimStates.TryGetValue(key, out var state))
        {
            double now = Time.realtimeSinceStartup * 1000d;
            AnimStates[key] = state = new AnimState { Previous = value, Target = value, StartedAt = now };
        }
        return state;
    }

    private static double Interpolate(AnimState state, double speed, Ease ease)
    {
        if (speed <= 0) return state.Target;
        double now = Time.realtimeSinceStartup * 1000d;
        float progress = Mathf.Clamp01((float)((now - state.StartedAt) / speed));
        return state.Previous + ((state.Target - state.Previous) * EaseValue(progress, ease));
    }

    // ===== 特效 tag =====

    /// <summary>按数值在两色间插值，输出 hex（不含 #，可直接放进 &lt;color=#...&gt;）。</summary>
    [Tag("ColorRange", TagState.None)]
    internal static string ColorRange(string tag, double min, double max, string fromHex, string toHex,
        string ease = null, int maxLength = -1, string atMaxHex = null)
    {
        if (!TryReadTagNumber(tag, out double value)) return "";
        // atMax：value ≥ max 时直接返回顶格色——「X精准度/X分数 满命中转金」用这个表达。
        if (!string.IsNullOrEmpty(atMaxHex) && value >= max) return SanitizeHex(atMaxHex, maxLength) ?? "";
        if (max <= min) return SanitizeHex(fromHex, maxLength) ?? "";
        if (!TryParseHex(fromHex, out var from) || !TryParseHex(toHex, out var to)) return "";
        float progress = Mathf.Clamp01((float)((value - min) / (max - min)));
        progress = EaseValue(progress, ParseEase(ease, Ease.Linear));
        var color = Color.LerpUnclamped(from, to, progress);
        var result = ColorUtility.ToHtmlStringRGB(color);
        return maxLength >= 0 && result.Length > maxLength ? result.Substring(0, maxLength) : result;
    }

    /// <summary>数值缓动跟随：tag 跳变时按 ease 在 speed 毫秒内平滑过渡。</summary>
    [Tag("EasedValue", TagState.None)]
    internal static double EasedValue(string tag, int digits = -1, double speed = 500, string ease = null)
    {
        if (!TryReadTagNumber(tag, out double value)) return 0;
        var state = GetAnimState("e:" + tag, value);
        double current = Interpolate(state, speed, ParseEase(ease, Ease.OutExpo));
        // 目标变化：把「此刻的插值位置」记为起点再切换目标——连续跳变时从动画中途续走。
        if (value != state.Target)
        {
            state.Previous = current;
            state.Target = value;
            state.StartedAt = Time.realtimeSinceStartup * 1000d;
            current = state.Previous;
        }
        return digits < 0 ? current : Math.Round(current, digits);
    }

    /// <summary>数值变化瞬间，在 start→end 之间按缓动取值（用于字号/位移脉冲式动效）。</summary>
    [Tag("MovingMan", TagState.None)]
    internal static double MovingMan(string tag, double startSize, double endSize, double defaultSize,
        double speed = 250, bool invert = false, string ease = null)
    {
        if (!TryReadTagNumber(tag, out double value)) return defaultSize;
        var state = GetAnimState("m:" + tag, value);
        double now = Time.realtimeSinceStartup * 1000d;
        if (value != state.Target) { state.Target = value; state.StartedAt = now; }
        if (speed <= 0 || now - state.StartedAt >= speed) return defaultSize;
        float progress = Mathf.Clamp01((float)((now - state.StartedAt) / speed));
        float eased = EaseValue(invert ? 1f - progress : progress, ParseEase(ease, Ease.OutExpo));
        return startSize + ((endSize - startSize) * eased);
    }

    private static string SanitizeHex(string hex, int maxLength)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var h = hex.TrimStart('#');
        return maxLength >= 0 && h.Length > maxLength ? h.Substring(0, maxLength) : h;
    }

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrEmpty(hex)) return false;
        var h = hex.Trim().TrimStart('#');
        if (h.Length != 6 && h.Length != 8) return false;
        return ColorUtility.TryParseHtmlString("#" + h, out color);
    }

    // ===== 低成本数值 tag =====

    /// <summary>平均帧耗时（毫秒）。</summary>
    [Tag("FrameTime", TagState.None)]
    internal static double FrameTime()
    {
        float dt = Time.unscaledDeltaTime;
        return dt > 0f ? R(dt * 1000f) : 0;
    }

    /// <summary>本次游戏会话内已开始的尝试次数（进关/重开各计一次）。</summary>
    [Tag("SessionAttempts")]
    internal static double SessionAttempts() => SessionAttemptCount;

    internal static int SessionAttemptCount;

    /// <summary>难度（本地化名；RDString 不可用时回退英文枚举名）。</summary>
    [Tag("Difficulty", TagState.None)]
    internal static string Difficulty() => LocalizedDifficulty(DifficultyRaw());

    /// <summary>难度（英文枚举名：Lenient / Normal / Strict）。</summary>
    [Tag("DifficultyRaw", TagState.None)]
    internal static string DifficultyRaw()
    {
        try { return GCS.difficulty.ToString(); }
        catch { return ""; }
    }

    private static string LocalizedDifficulty(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        try
        {
            var get = typeof(RDString).GetMethod("Get", new[] { typeof(string), typeof(Dictionary<string, object>) })
                      ?? typeof(RDString).GetMethod("Get", new[] { typeof(string) });
            if (get == null) return raw;
            var key = "enum.Difficulty." + raw;
            var value = get.GetParameters().Length == 2
                ? get.Invoke(null, new object[] { key, null }) as string
                : get.Invoke(null, new object[] { key }) as string;
            return string.IsNullOrEmpty(value) ? raw : value;
        }
        catch { return raw; }
    }

    private static FieldInfo _lastXScoreField;
    private static PropertyInfo _maxPossibleXAccProp;

    /// <summary>上一次结算口径的 XScore（原生 marginTracker.lastXScore，r148 无此成员返回 0）。</summary>
    [Tag("LastXScore")]
    internal static double LastXScore(int player = 0)
    {
        var tracker = TrackerFor(player);
        if (tracker == null) return 0;
        try
        {
            _lastXScoreField ??= AccessTools.Field(tracker.GetType(), "lastXScore");
            return _lastXScoreField?.GetValue(tracker) is int v ? v : 0;
        }
        catch { return 0; }
    }

    /// <summary>剩余全 XPerfect 时可达的 X 准确率上限（游戏原生 maxPossibleXAcc × 检查点惩罚 0.9875^n），百分比口径。</summary>
    [Tag("MaxXAccuracy")]
    internal static double MaxXAccuracy(int player = 0)
    {
        var tracker = TrackerFor(player);
        if (tracker == null) return 100.0;
        try
        {
            _maxPossibleXAccProp ??= AccessTools.Property(tracker.GetType(), "maxPossibleXAcc");
            double max = _maxPossibleXAccProp?.GetValue(tracker) is float v && !float.IsNaN(v) ? v : double.NaN;
            if (double.IsNaN(max)) return 100.0;
            int checkpoints = GameRefs.CheckpointsUsed;
            return R(max * Math.Pow(0.9875, checkpoints) * 100.0);
        }
        catch { return 100.0; }
    }

    [Tag("MaxXAccuracy01")]
    internal static double MaxXAccuracy01(int player = 0) => MaxXAccuracy(player) / 100.0;

    // ===== 按判定连击（MarginCombo，自参考工程移植）=====

    private static FieldInfo _hitMarginsField;
    // 按玩家分槽缓存：coop 里 P0/P1 逐个求值时单槽缓存会互相踢掉，每次都得重拷整张判定表。
    private static readonly System.Collections.IEnumerable[] _marginSeqSource = new System.Collections.IEnumerable[4];
    private static readonly int[] _marginSeqLen = [-1, -1, -1, -1];
    private static readonly int[][] _marginSeq = new int[4][];

    /// <summary>关卡重进（Show）时清全部判定序列快照。检查点读档与 RevertToLastCheckpoint
    /// 都是对同一 List 实例的原地修改（Clear+重填 / 尾部 Pop），引用失效判据看不见；
    /// 重开局强制重拷一次，防未知原地修改路径（其它 mod 补丁、未来版本）残留旧快照。</summary>
    internal static void ResetMarginSequenceCache()
    {
        Array.Clear(_marginSeqSource, 0, _marginSeqSource.Length);
        Array.Clear(_marginSeq, 0, _marginSeq.Length);
        for (int i = 0; i < _marginSeqLen.Length; i++) _marginSeqLen[i] = -1;
    }

    /// <summary>把 marginTracker.hitMargins（有序判定列表）取成 int 快照；列表身份/长度不变时复用。</summary>
    private static int[] MarginSequence(int player)
    {
        var tracker = TrackerFor(player);
        if (tracker == null) return Array.Empty<int>();
        int slot = VersionSafe.IsCoopMode() ? Math.Min(Math.Max(player, 0), 3) : 0;
        try
        {
            _hitMarginsField ??= AccessTools.Field(tracker.GetType(), "hitMargins");
            var list = _hitMarginsField?.GetValue(tracker) as System.Collections.IEnumerable;
            if (list == null) return Array.Empty<int>();
            int count = list is System.Collections.ICollection col ? col.Count : 0;
            if (ReferenceEquals(_marginSeqSource[slot], list) && _marginSeqLen[slot] == count && _marginSeq[slot] != null)
                return _marginSeq[slot];
            var seq = new int[count];
            // 优先走泛型索引（不装箱）；非泛型序列才退化为 foreach + Convert。
            if (list is System.Collections.Generic.IList<int> typed)
            {
                for (int i = 0; i < count && i < typed.Count; i++) seq[i] = typed[i];
            }
            else
            {
                int i = 0;
                foreach (var item in list)
                {
                    if (i >= seq.Length) break;
                    seq[i++] = Convert.ToInt32(item);
                }
            }
            _marginSeqSource[slot] = list;
            _marginSeqLen[slot] = count;
            return _marginSeq[slot] = seq;
        }
        catch { return Array.Empty<int>(); }
    }

    private static bool TryParseMarginName(string raw, out int index)
    {
        index = -1;
        switch ((raw ?? "").Trim().ToLowerInvariant())
        {
            case "xp": case "xperfect": index = HitMarginCompat.XPerfect; break;
            case "pm": case "perfectminus": index = HitMarginCompat.PerfectMinus; break;
            case "pp": case "perfectplus": index = HitMarginCompat.PerfectPlus; break;
            case "ep": case "earlyperfect": index = HitMarginCompat.EarlyPerfect; break;
            case "lp": case "lateperfect": index = HitMarginCompat.LatePerfect; break;
            case "te": case "tooearly": index = HitMarginCompat.TooEarly; break;
            case "tl": case "toolate": index = HitMarginCompat.TooLate; break;
            case "ve": case "veryearly": index = HitMarginCompat.VeryEarly; break;
            case "vl": case "verylate": index = HitMarginCompat.VeryLate; break;
            case "a": case "auto": index = HitMarginCompat.Auto; break;
            case "fm": case "failmiss": index = HitMarginCompat.FailMiss; break;
            case "fo": case "failoverload": index = HitMarginCompat.FailOverload; break;
            case "mp": case "multipress": index = HitMarginCompat.Multipress; break;
            case "op": case "overpress": index = HitMarginCompat.OverPress; break;
            case "ms": case "midspin": index = HitMarginCompat.Midspin; break;
        }
        return index >= 0;
    }

    /// <summary>当前连击尾（tail）与最大连击（max），按给定判定集合匹配，Midspin 打断不算。</summary>
    private static (int tail, int max) GetMarginRuns(string margins, int player)
    {
        var set = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(margins))
        {
            foreach (var part in margins.Split('|'))
                if (TryParseMarginName(part, out int idx)) set.Add(idx);
        }
        var seq = MarginSequence(player);
        int midspin = HitMarginCompat.Midspin;
        int best = 0, current = 0;
        for (int i = 0; i < seq.Length; i++)
        {
            if (seq[i] == midspin) continue;
            current = set.Contains(seq[i]) ? current + 1 : 0;
            if (current > best) best = current;
        }
        return (current, best);
    }

    /// <summary>当前连击：从最新判定往回数，命中 margins（如 "XP|A"，支持全名与缩写，| 分隔多判定）即延续。</summary>
    [Tag("MarginCombo")]
    internal static double MarginCombo(string margins, int player = 0) => GetMarginRuns(margins, player).tail;

    /// <summary>本次尝试中该判定集合的最大连击段。</summary>
    [Tag("MarginMaxCombo")]
    internal static double MarginMaxCombo(string margins, int player = 0) => GetMarginRuns(margins, player).max;
}
