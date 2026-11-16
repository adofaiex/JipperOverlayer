using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;

namespace JipperOverlayer.Overlayer.Tags;

/// <summary>
/// 轻量模板引擎：把「{Tag} / {Tag(args)} / {Tag:format} / {Tag(args):format}」占位符
/// 替换为 tag 求值结果。
/// - {{ 与 }} 是字面花括号；
/// - format 从 tag 名/参数之后第一个不在括号内的冒号切出，应用到 IFormattable 结果（InvariantCulture）；
/// - 未知 tag、参数错误或 tag 抛异常时保留占位符原文，便于用户发现问题；
/// - 解析结果按模板字符串缓存（内容不变不重解析）。
/// </summary>
internal static class TemplateEngine
{
    private sealed class Segment
    {
        public bool IsLiteral;
        public string Literal;
        public string TagName;
        public string[] Args;
        public string Format;
        public string Raw;

        // 游玩状态门控（BlockOn*）：标记本身在注册表里，段内只缓存查询结果与上次好值。
        public bool StateKnown;
        public TagState State;
        public bool Blockable;
        public string Last;
        public bool HasLast;
    }

    private sealed class ParsedTemplate
    {
        public Segment[] Segments;
    }

    private static readonly ConditionalWeakTable<string, ParsedTemplate> Cache = new();
    private static string _lastSource;
    private static ParsedTemplate _lastParsed;

    /// <summary>求值模板。空模板返回 null（调用方回退到旧渲染路径）。</summary>
    public static string Evaluate(string template) => Evaluate(template, 0);

    /// <summary>按玩家上下文求值：模板里 player 参数未显式写的 tag（{XPerfect} 等）
    /// 会取这个玩家序号——coop 判定行逐玩家渲染同一个模板的关键。求值完恢复。</summary>
    public static string Evaluate(string template, int player)
    {
        if (string.IsNullOrEmpty(template)) return null;
        var saved = TagManager.CurrentPlayer;
        TagManager.CurrentPlayer = player;
        try
        {
            return EvaluateCore(template);
        }
        finally
        {
            TagManager.CurrentPlayer = saved;
        }
    }

    /// <summary>保持当前玩家上下文求值（组合 tag 展开内部用：外层模板已设定上下文，
    /// 组合片段里的 {XAccuracy} 等应沿用同一玩家）。</summary>
    internal static string EvaluateKeepContext(string template)
    {
        if (string.IsNullOrEmpty(template)) return null;
        var saved = TagManager.CurrentPlayer;
        try
        {
            return EvaluateCore(template);
        }
        finally
        {
            TagManager.CurrentPlayer = saved;
        }
    }

    private static string EvaluateCore(string template)
    {
        var parsed = GetParsed(template);
        var sb = new StringBuilder(template.Length + 32);
        foreach (var seg in parsed.Segments)
        {
            if (seg.IsLiteral)
            {
                sb.Append(seg.Literal);
                continue;
            }
            // BlockOn* 门控：被冻结时沿用上一次求到的值（从未求值过才显示原文），
            // 避免读图/暂停/结算瞬间把数值闪成 0 或占位符。
            if (IsBlockedSegment(seg))
            {
                sb.Append(seg.HasLast ? seg.Last : seg.Raw);
                continue;
            }
            if (!TagManager.TryInvoke(seg.TagName, seg.Args, out var value) || value == null)
            {
                sb.Append(seg.Raw);
                continue;
            }
            var text = FormatValue(value, seg.Format);
            if (seg.Blockable) { seg.Last = text; seg.HasLast = true; }
            sb.Append(text);
        }
        return sb.ToString();
    }

    /// <summary>查询并缓存该段的门控标记；tag 未注册（拼错）时不冻结，保留原文提示。</summary>
    private static bool IsBlockedSegment(Segment seg)
    {
        if (!seg.StateKnown)
        {
            if (!TagManager.TryGetTagState(seg.TagName, out var st)) return false;
            seg.State = st;
            seg.StateKnown = true;
            seg.Blockable = st != TagState.None;
        }
        return seg.Blockable && TagManager.IsBlockedState(seg.State);
    }

    /// <summary>tag 返回值字符串化：数值一律 InvariantCulture；format 优先。</summary>
    private static string FormatValue(object value, string format)
    {
        if (value == null) return "";
        if (format != null && value is IFormattable withFormat)
            return withFormat.ToString(format, CultureInfo.InvariantCulture);
        if (value is string s) return s;
        if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString();
    }

    /// <summary>
    /// 把模板求值结果写入文本；模板为空（null/空串）返回 false，调用方继续走内置渲染。
    /// 模板接管后底色统一置白——颜色由模板里的 &lt;color&gt; 标签决定，
    /// 否则组件残留色会把整行（含分隔符）一起染色。
    /// </summary>
    public static bool TryApply(string template, TextMeshProUGUI text) => TryApply(template, text, 0);

    public static bool TryApply(string template, TextMeshProUGUI text, int player)
    {
        if (string.IsNullOrEmpty(template)) return false;
        var value = Evaluate(template, player);
        if (value == null) return false;
        if (text)
        {
            text.text = value;
            text.color = Color.white;
        }
        return true;
    }

    private static ParsedTemplate GetParsed(string template)
    {
        if (ReferenceEquals(template, _lastSource)) return _lastParsed;
        if (!Cache.TryGetValue(template, out var parsed))
        {
            parsed = Parse(template);
            Cache.Add(template, parsed);
        }
        _lastSource = template;
        _lastParsed = parsed;
        return parsed;
    }

    private static ParsedTemplate Parse(string template)
    {
        var segments = new List<Segment>();
        var literal = new StringBuilder();
        int i = 0, n = template.Length;
        while (i < n)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < n && template[i + 1] == '{') { literal.Append('{'); i += 2; continue; }
                int close = FindClosingBrace(template, i + 1);
                if (close < 0) { literal.Append(template, i, n - i); break; }
                FlushLiteral(segments, literal);
                segments.Add(BuildTagSegment(template.Substring(i + 1, close - i - 1)));
                i = close + 1;
            }
            else if (c == '}' && i + 1 < n && template[i + 1] == '}')
            {
                literal.Append('}');
                i += 2;
            }
            else
            {
                literal.Append(c);
                i++;
            }
        }
        FlushLiteral(segments, literal);
        return new ParsedTemplate { Segments = segments.ToArray() };
    }

    /// <summary>找与 start 前那个 { 匹配的 }；参数括号内的 } 不算结束。</summary>
    private static int FindClosingBrace(string s, int start)
    {
        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == '}' && depth == 0) return i;
        }
        return -1;
    }

    private static void FlushLiteral(List<Segment> segments, StringBuilder literal)
    {
        if (literal.Length == 0) return;
        segments.Add(new Segment { IsLiteral = true, Literal = literal.ToString() });
        literal.Clear();
    }

    private static Segment BuildTagSegment(string content)
    {
        var seg = new Segment { TagName = content, Raw = "{" + content + "}" };

        // 切 format：第一个不在括号内的 ':'
        int depth = 0, colon = -1;
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ':' && depth == 0) { colon = i; break; }
        }
        string tagPart = content;
        if (colon >= 0)
        {
            tagPart = content.Substring(0, colon);
            seg.Format = content.Substring(colon + 1);
        }

        // 切参数：交给 TagArgParser（引号内、嵌套括号内的逗号不拆分）。
        if (!TagArgParser.TrySplit(tagPart, out var tagName, out var tagArgs))
        {
            // 括号不配对等异常形态：整段当 tag 名，求值失败后原样保留，用户能看出写错了。
            seg.TagName = tagPart.Trim();
            return seg;
        }
        seg.TagName = tagName;
        if (tagArgs.Length > 0) seg.Args = tagArgs;
        return seg;
    }
}
