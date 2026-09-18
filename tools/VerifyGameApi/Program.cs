// VerifyGameApi — checks JipperOverlayer's game-DLL dependencies against Libs/*.dll
// Run:  dotnet run -c Release            (from this folder)
//       VerifyGameApi.exe                (works from anywhere: searches upward for Libs/)
//       VerifyGameApi.exe <libs-folder>  (verify an arbitrary set of game DLLs)
//
// What it checks — three levels:
//   1. TYPE / MEMBER existence by name (reflection getters are try/catch-wrapped,
//      so a missing member degrades gracefully — but we still want to know).
//   2. SIGNATURE-level checks for the members the mod binds tightly to:
//      Harmony injects postfix parameters BY NAME, so a rename is as fatal as a removal.
//   3. VERSION-DISJOINT groups: the mod probes at runtime between an r148 shape and an
//      r150 shape (e.g. scrMisc.GetHitMargin vs GetHitMarginInDeg). At least one
//      alternative must exist; which one is present also tells us the detected version.
//
// This is the precise definition of a "mod vs game conflict": a missing/renamed type or
// member, or a signature change that compile-time checking cannot see.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

class Program
{
    static readonly Dictionary<string, TypeDef> Types = new(StringComparer.Ordinal);
    static int Total, FailCount;
    static bool IsR150;

    static int Main(string[] args)
    {
        string libs = FindLibs(args);
        if (libs == null)
        {
            Console.Error.WriteLine("Libs/ not found — pass the folder as an argument or run from inside the repo.");
            return 2;
        }
        Console.WriteLine("Using Libs: " + Path.GetFullPath(libs));
        Load(Path.Combine(libs, "Assembly-CSharp.dll"));
        Load(Path.Combine(libs, "UnityEngine.AudioModule.dll"));
        Load(Path.Combine(libs, "UnityEngine.CoreModule.dll"));

        // r150 检测方式与运行时 HitMarginCompat 一致：枚举里出现 XPerfect
        IsR150 = Types.TryGetValue("HitMargin", out var hm) && HasEnumValue(hm, "XPerfect");
        Console.WriteLine("Detected game API: " + (IsR150 ? "r150+ (native XPerfect)" : "r148 and earlier"));

        void CT(string n) { if (Types.ContainsKey(n)) Pass("TYPE  " + n); else Fail("TYPE  " + n); }
        void CM(string tn, string m)
        {
            if (!Types.TryGetValue(tn, out var t)) { Fail($"MEMBER {tn}.{m} (type missing)"); return; }
            if (HasMember(t, m)) Pass($"MEMBER {tn}.{m}"); else Fail($"MEMBER {tn}.{m}");
        }
        // 方法存在且 postfix 声明的参数名都能在原方法参数表里找到（Harmony 按名注入，
        // patch 只需声明原参数的一个子集，如 scnGame.Play(seqID, remakeFloors) 只声明 seqID）
        void MS(string tn, string m, params string[] paramNames)
        {
            if (!Types.TryGetValue(tn, out var t)) { Fail($"SIG   {tn}.{m}{Pm(paramNames)} (type missing)"); return; }
            if (HasMethodWithParamNames(t, m, paramNames)) Pass($"SIG   {tn}.{m}{Pm(paramNames)}");
            else Fail($"SIG   {tn}.{m}{Pm(paramNames)}");
        }
        // 仅报告不计分：v136 旧路径的目标，现代 DLL 上本来就可能不存在（运行时不注册）
        void OPT(string tn, string m)
        {
            if (!Types.TryGetValue(tn, out var t)) { Console.WriteLine($"  --    v136-legacy {tn}.{m} (type missing)"); return; }
            Console.WriteLine(HasMember(t, m) ? $"  --    v136-legacy {tn}.{m} present" : $"  --    v136-legacy {tn}.{m} absent (v136-only)");
        }
        // 版本二选一：r148 形态或 r150 形态，至少命中一个
        void Alt(string label, (string tn, string m, string[] p)[] alts)
        {
            var hits = alts.Where(a => Types.TryGetValue(a.tn, out var t) && HasMethodWithParamNames(t, a.m, a.p)).ToArray();
            if (hits.Length == 0) Fail($"ALT   {label} — no variant matched");
            else Pass($"ALT   {label} -> matched {string.Join(" | ", hits.Select(h => h.tn + "." + h.m))}");
        }

        Console.WriteLine("=== Game types referenced by mod ===");
        foreach (var n in new[] {
            "ADOBase","RDC","scrController","scrConductor","scrLevelMaker","scnGame","scnEditor",
            "scrPlanet","scrShowIfDebug","scrEnableIfBeta","scrMisc","scrPressToStart","scrUIController",
            "scrPlayer","scrMarginTracker","scrMistakesManager","scrPlayerManager","scrFloor","GCS",
            "RDConstants","MonsterLove.StateMachine.StateBehaviour","States","HitMargin","Platform",
            "UnityEngine.AudioSource","PlanetarySystem","PlanetColor" })
            CT(n);

        Console.WriteLine("\n=== GameRefs reflection members (v141 default path) ===");
        CM("ADOBase","controller"); CM("ADOBase","conductor"); CM("ADOBase","lm"); CM("ADOBase","isScnGame"); CM("ADOBase","playerManager"); CM("ADOBase","platform");
        CM("scrController","instance"); CM("scrController","_instance"); CM("scrController","paused"); CM("scrController","currentSeqID");
        CM("scrController","currFloor"); CM("scrController","firstFloor"); CM("scrController","noFail"); CM("scrController","percentComplete");
        CM("scrController","checkpointsUsed"); CM("scrController","playerOne"); CM("scrController","mistakesManager");
        CM("scrConductor","instance"); CM("scrConductor","song"); CM("scrConductor","isGameWorld"); CM("scrConductor","addoffset"); CM("scrConductor","songposition_minusi");
        CM("UnityEngine.AudioSource","pitch"); CM("RDC","auto"); CM("scnGame","instance"); CM("scnGame","levelData"); CM("scnEditor","instance");

        Console.WriteLine("\n=== VersionSafe reflection members ===");
        CM("scrMarginTracker","hitMarginsCount"); CM("scrMarginTracker","AddHit"); CM("scrMarginTracker","Reset"); CM("scrMarginTracker","CalculatePercentAcc");
        CM("scrMistakesManager","marginTrackers"); CM("scrMistakesManager","percentAcc"); CM("scrMistakesManager","percentXAcc");
        CM("scrPlayerManager","playerColors"); CM("scrPlayerManager","playerCount"); CM("scrPlayerManager","instance"); CM("scrPlayerManager","allPlayers");
        CM("scrPlayer","planetarySystem"); CM("PlanetarySystem","speed"); CM("scrFloor","seqID"); CM("scrFloor","nextfloor"); CM("scrFloor","prevfloor"); CM("scrFloor","angleLength");
        CM("PlanetColor","ToRealColor");

        Console.WriteLine("\n=== Harmony patch targets: postfix declares original params (name-bound) ===");
        MS("MonsterLove.StateMachine.StateBehaviour","ChangeState","newState");
        MS("scnGame","Play","seqID");
        MS("scrMarginTracker","AddHit","hit");
        // ExtendedOverlay 计时：r148 是 GetHitMargin，r150 拆成 InDeg / InSec —— 参数名各不相同
        Alt("scrMisc hit-margin fn (ExtendedOverlay timing)", new[] {
            ("scrMisc","GetHitMargin",     new[]{ "hitangle","refangle","isCW","bpmTimesSpeed","conductorPitch","marginScale" }),
            ("scrMisc","GetHitMarginInDeg",new[]{ "difficulty","hitAngle","refAngle","clockwise","floorBpm","conductorPitch","marginScale" }),
            ("scrMisc","GetHitMarginInSec",new[]{ "difficulty","timeDiff","floorBpm","conductorPitch","marginScale" }),
        });

        Console.WriteLine("\n=== v136 legacy patch targets (registered only on v136 games) ===");
        OPT("scrController","Hit");
        OPT("scrMistakesManager","AddHit");
        OPT("scrMistakesManager","Reset");
        OPT("scrMistakesManager","CalculatePercentAcc");

        Console.WriteLine("\n=== Harmony patch targets: name-bound only (postfix takes no original params) ===");
        CM("scrPressToStart","ShowText"); CM("scrUIController","WipeToBlack");
        CM("scnEditor","ResetScene"); CM("scrController","StartLoadingScene"); CM("scrPlanet","MoveToNextFloor");
        CM("scrShowIfDebug","Update"); CM("scrShowIfDebug","Awake"); CM("scrShowIfDebug","txt");
        CM("RDC","auto"); CM("scrEnableIfBeta","Awake"); CM("scrEnableIfBeta","setBuildText");
        CM("scrPlayer","Hit"); CM("scrMarginTracker","Reset"); CM("scrMarginTracker","CalculatePercentAcc");
        CM("scrMistakesManager","SetPlayerCount");

        Console.WriteLine("\n=== Compat layer runtime probes (GameCompat / VersionSafe / HitMarginCompat) ===");
        // 判定边界：r148 (HitMarginGeneral,...)->double 或 r150 (Difficulty,...)->结构体
        Alt("scrMisc.GetAdjustedAngleBoundaryInDeg", new[] {
            ("scrMisc","GetAdjustedAngleBoundaryInDeg", new[]{ "marginType","bpmTimesSpeed","conductorPitch","marginMult" }),
            ("scrMisc","GetAdjustedAngleBoundaryInDeg", new[]{ "difficulty","bpmTimesSpeed","conductorPitch","marginMult" }),
        });
        // 精度刷新：无参（r148，以及 2026-09-13 起的 r150）或带 bool 的 r150 早期构建
        Alt("scrMarginTracker.CalculatePercentAcc", new[] {
            ("scrMarginTracker","CalculatePercentAcc", new string[0]),
            ("scrMarginTracker","CalculatePercentAcc", new[]{ "increaseRemainingPlayerHits" }),
        });
        // HitMargin 语义名（r150 把 Perfect 拆成 PerfectMinus/XPerfect/PerfectPlus）
        if (Types.TryGetValue("HitMargin", out var hmT))
        {
            foreach (var n in new[]{ "TooEarly","VeryEarly","EarlyPerfect","LatePerfect","VeryLate","TooLate",
                                     "Multipress","FailMiss","FailOverload","Auto","OverPress" })
                if (HasEnumValue(hmT, n)) Pass($"ENUM  HitMargin.{n}"); else Fail($"ENUM  HitMargin.{n}");
            bool legacy = HasEnumValue(hmT, "Perfect");
            bool split = HasEnumValue(hmT, "PerfectMinus") && HasEnumValue(hmT, "XPerfect") && HasEnumValue(hmT, "PerfectPlus");
            if (legacy || split)
                Pass($"ENUM  HitMargin perfect group ({(legacy ? "Perfect" : "PerfectMinus/XPerfect/PerfectPlus")})");
            else
                Fail("ENUM  HitMargin perfect group (neither Perfect nor PerfectMinus/XPerfect/PerfectPlus)");
        }
        else Fail("ENUM  HitMargin (type missing)");

        Console.WriteLine("\n=== States enum values ===");
        if (Types.TryGetValue("States", out var st))
            foreach (var f in st.Fields) if (!f.Name.StartsWith("value__")) Console.WriteLine($"  {f.Name} = {f.Constant?.Value}");

        int rc = FailCount == 0 ? 0 : 1;
        Console.WriteLine($"\n==== RESULT: {FailCount} missing / {Total} checked  -> {(rc == 0 ? "NO CONFLICTS" : "CONFLICTS FOUND")} ====");
        return rc;
    }

    static string Pm(string[] p) => "(" + string.Join(",", p) + ")";

    // Libs location: explicit argument wins; otherwise walk up from the executable
    // so the tool works from bin/, the project folder, or the repo root regardless
    // of the caller's working directory.
    static string FindLibs(string[] args)
    {
        if (args.Length > 0 && File.Exists(Path.Combine(args[0], "Assembly-CSharp.dll")))
            return args[0];
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "Libs");
            if (File.Exists(Path.Combine(candidate, "Assembly-CSharp.dll"))) return candidate;
        }
        return null;
    }

    static void Load(string path)
    {
        try
        {
            var m = ModuleDefMD.Load(path);
            foreach (var t in m.GetTypes())
                if (t.FullName != null && !Types.ContainsKey(t.FullName)) Types[t.FullName] = t;
        }
        catch (Exception e) { Console.WriteLine($"  [warn] cannot load {path}: {e.Message}"); }
    }
    static void Pass(string s) { Total++; Console.WriteLine("  OK   " + s); }
    static void Fail(string s) { Total++; FailCount++; Console.WriteLine("  FAIL " + s); }
    static bool HasMember(TypeDef t, string name)
    {
        foreach (var f in t.Fields) if (f.Name == name) return true;
        foreach (var p in t.Properties) if (p.Name == name) return true;
        foreach (var m in t.Methods) if (m.Name == name) return true;
        return false;
    }
    static bool HasEnumValue(TypeDef t, string name)
    {
        foreach (var f in t.Fields) if (f.Name == name) return true;
        return false;
    }
    // 方法名一致，且期望的每个参数名都出现在原方法真实参数表里（Harmony 按名绑定子集）
    static bool HasMethodWithParamNames(TypeDef t, string name, string[] paramNames)
    {
        foreach (var m in t.Methods)
        {
            if (m.Name != name) continue;
            var ps = m.Parameters.Where(p => p.MethodSigIndex >= 0).Select(p => p.Name).ToList();
            if (paramNames.All(n => ps.Contains(n))) return true;
        }
        return false;
    }
}
