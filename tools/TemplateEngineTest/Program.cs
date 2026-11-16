using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal static class TagArgParser
{
    public static bool TrySplit(string expr, out string name, out string[] args)
    {
        name = (expr ?? string.Empty).Trim();
        args = Array.Empty<string>();
        if (name.Length == 0) return false;
        int open = name.IndexOf('(');
        if (open < 0) return name.IndexOf(')') < 0;
        if (!name.EndsWith(")")) return false;
        string inner = name.Substring(open + 1, name.Length - open - 2);
        name = name.Substring(0, open).Trim();
        if (name.Length == 0) return false;
        if (string.IsNullOrWhiteSpace(inner)) return true;
        var parts = new List<string>();
        int depth = 0; char quote = (char)0; int start = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (quote != (char)0) { if (c == quote) quote = (char)0; continue; }
            if (c == (char)39 || c == (char)34) quote = c;
            else if (c == '(') depth++;
            else if (c == ')') { if (depth == 0) return false; depth--; }
            else if (c == ',' && depth == 0) { parts.Add(inner.Substring(start, i - start).Trim()); start = i + 1; }
        }
        if (quote != (char)0 || depth != 0) return false;
        parts.Add(inner.Substring(start).Trim());
        args = parts.ToArray();
        return true;
    }
}

internal static class TagManager
{
    public class Entry
    {
        public string Name;
        public string[] ParamNames;
        public Type[] ParamTypes;
        public object[] Defaults;
        public int RequiredCount;
        public Func<object[], object> InvokeMirror;
    }

    public static int CurrentPlayer;
    private static readonly Dictionary<string, Entry> Tags = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> CustomTags = new(StringComparer.Ordinal);
    private static int CustomEvalDepth;

    public static void Register(string name, string[] paramNames, Type[] paramTypes, object[] defaults, Func<object[], object> invoke)
    {
        int req = 0;
        foreach (var d in defaults) if (d == DBNull.Value) req++;
        Tags[name] = new Entry { Name = name, ParamNames = paramNames, ParamTypes = paramTypes, Defaults = defaults, RequiredCount = req, InvokeMirror = invoke };
    }

    public static void SetCustomTags(Dictionary<string, string> defs)
    {
        foreach (var kv in defs) CustomTags[kv.Key] = kv.Value;
    }

    public static bool TryInvoke(string name, string[] args, out object result)
    {
        result = null;
        if (string.IsNullOrEmpty(name)) return false;
        if (CustomTags.TryGetValue(name, out var ctpl))
        {
            if (CustomEvalDepth >= 4) return false;
            CustomEvalDepth++;
            try { result = Engine.EvaluateKeepContext(ctpl); return result != null; }
            finally { CustomEvalDepth--; }
        }
        if (!Tags.TryGetValue(name, out var entry)) { Console.WriteLine("  [FAIL] unknown tag " + name); return false; }
        int supplied = 0;
        if (args != null)
            for (int i = 0; i < args.Length && i < entry.ParamNames.Length; i++)
                if (args[i].Length > 0 || entry.ParamNames[i] == "player") supplied++;
        if (supplied < entry.RequiredCount)
        { Console.WriteLine("  [FAIL] missing required (" + supplied + "/" + entry.RequiredCount + ")"); return false; }
        var normalized = NormalizeArgs(entry, args);
        result = entry.InvokeMirror(normalized);
        return true;
    }

    private static object[] NormalizeArgs(Entry e, string[] args)
    {
        args ??= Array.Empty<string>();
        var normalized = new object[e.ParamTypes.Length];
        for (int i = 0; i < e.ParamTypes.Length; i++)
        {
            bool omitted = i >= args.Length;
            bool empty = !omitted && args[i].Length == 0;
            if ((omitted || empty) && e.ParamNames[i] == "player") normalized[i] = CurrentPlayer;
            else if (!omitted && !empty) normalized[i] = CoerceArg(args[i], e.ParamTypes[i]);
            else if (e.Defaults[i] != DBNull.Value) normalized[i] = CoerceArg(e.Defaults[i], e.ParamTypes[i]);
            else normalized[i] = e.ParamTypes[i].IsValueType ? Activator.CreateInstance(e.ParamTypes[i]) : null;
        }
        return normalized;
    }

    private static object CoerceArg(object value, Type target)
    {
        if (value == null) return target.IsValueType ? Activator.CreateInstance(target) : null;
        if (target.IsInstanceOfType(value)) return value;
        if (target == typeof(string)) return value.ToString();
        if (value is string text) return ConvertText(text, target);
        if (value is IConvertible && (target.IsPrimitive || target == typeof(decimal)))
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        throw new InvalidCastException("cannot convert");
    }

    private static object ConvertText(string text, Type target)
    {
        if (target == typeof(string)) return text;
        if (text.Length == 0) return target.IsValueType ? Activator.CreateInstance(target) : null;
        if (target == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        if (target == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        if (target == typeof(bool) && bool.TryParse(text, out var bl)) return bl;
        return Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
    }
}

internal static class Engine
{
    public static string EvaluateKeepContext(string template)
    {
        var saved = TagManager.CurrentPlayer;
        try { return Evaluate(template); }
        finally { TagManager.CurrentPlayer = saved; }
    }

    public static string Evaluate(string template)
    {
        var sb = new StringBuilder();
        int i = 0, n = template.Length;
        while (i < n)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < n && template[i + 1] == '{') { sb.Append('{'); i += 2; continue; }
                int close = FindClosingBrace(template, i + 1);
                if (close < 0) { sb.Append(template, i, n - i); break; }
                var content = template.Substring(i + 1, close - i - 1);
                i = close + 1;
                if (!TagArgParser.TrySplit(content, out var name, out var args)) { sb.Append("{" + content + "}"); continue; }
                if (!TagManager.TryInvoke(name, args, out var value) || value == null)
                { sb.Append("{" + content + "}"); continue; }
                sb.Append(value);
            }
            else if (c == '}' && i + 1 < n && template[i + 1] == '}') { sb.Append('}'); i += 2; }
            else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }

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
}

internal static class Stub
{
    public static double XAccuracy(object[] a) => 100.0;
    public static double XScoreRatio(object[] a) => 100.0;
    public static double Combo(object[] a) => 7.0;
    public static string ColorRange(object[] a)
    {
        string tag = (string)a[0];
        double min = (double)a[1];
        double max = (double)a[2];
        string atMax = a.Length > 7 ? (string)a[7] : null;
        if (!TagManager.TryInvoke(tag, Array.Empty<string>(), out var v)) return "";
        double value = (double)v;
        if (!string.IsNullOrEmpty(atMax) && value >= max) return atMax;
        return "FF0000";
    }
}

internal static class Program
{
    private static int pass, fail;
    private static void Check(string label, string actual, string expected)
    {
        bool ok = actual == expected;
        if (ok) pass++; else fail++;
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + label + " -> '" + actual + "'" + (ok ? "" : "  expect '" + expected + "'"));
    }

    private static void Main()
    {
        TagManager.Register("XAccuracy", new[] { "player" }, new[] { typeof(int) }, new object[] { 0 }, a => (object)Stub.XAccuracy(a));
        TagManager.Register("XScoreRatio", new[] { "player" }, new[] { typeof(int) }, new object[] { 0 }, a => (object)Stub.XScoreRatio(a));
        TagManager.Register("Combo", Array.Empty<string>(), Array.Empty<Type>(), Array.Empty<object>(), a => (object)Stub.Combo(a));
        TagManager.Register("ColorRange", new[] { "tag", "min", "max", "fromHex", "toHex", "ease", "maxLength", "atMaxHex" },
            new[] { typeof(string), typeof(double), typeof(double), typeof(string), typeof(string), typeof(string), typeof(int), typeof(string) },
            new object[] { DBNull.Value, 0.0, 0.0, DBNull.Value, DBNull.Value, null, -1, null }, a => (object)Stub.ColorRange(a));

        Check("9args-extra-dropped", Engine.Evaluate("<color=#{ColorRange(XAccuracy,0,100,FF0000,FF0000,,,,FF0000)}>{XAccuracy}%</color>"), "<color=#FF0000>100%</color>");
        Check("8args-atMax", Engine.Evaluate("<color=#{ColorRange(XAccuracy,0,100,FF0000,FF0000,,,FF0000)}>{XAccuracy}%</color>"), "<color=#FF0000>100%</color>");
        Check("quoted-args", Engine.Evaluate("<color=#{ColorRange(XAccuracy,0,100,@Q@FF0000@Q@@Q@FF0000@Q@)}>{XAccuracy}%</color>").Replace("@Q@", "\""), "<color=#FF0000>100%</color>");
        Check("bare-missing-required", Engine.Evaluate("<color=#{ColorRange}>X</color>"), "<color=#{ColorRange}>X</color>");
        Check("partial-required-raw", Engine.Evaluate("<color=#{ColorRange(FF0000)}>X</color>"), "<color=#{ColorRange(FF0000)}>X</color>");
        TagManager.SetCustomTags(new Dictionary<string, string> { { "MyGold", "{XScoreRatio}G" } });
        Check("custom-tag", Engine.Evaluate("{MyGold}"), "100G");
        TagManager.CurrentPlayer = 1;
        TagManager.Register("P2", new[] { "player" }, new[] { typeof(int) }, new object[] { 0 }, a => (object)TagManager.CurrentPlayer);
        Check("custom-tag-keeps-context", Engine.Evaluate("{P2}"), "1");
        TagManager.CurrentPlayer = 0;
        TagManager.SetCustomTags(new Dictionary<string, string> { { "A", "{B}" }, { "B", "{A}" } });
        Check("cycle-guard-terminates", Engine.Evaluate("{A}"), "{A}");

        Console.WriteLine("=== PASS " + pass + " / FAIL " + fail);
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
