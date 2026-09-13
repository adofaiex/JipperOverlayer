# Changelog

## Unreleased

### Features

- **Timing display modes**: `Timing / AvgTiming / Both / BothInOneLine` — the average can now sit on its own stack line (`AvgTiming` element), and the per-text decimal slider (0-5) replaces the shared precision for the Timing text
- **XScore text** (r149+ only): the game's native XPerfect score (X=2, Perfect±=1) computed from `hitMarginsCount`, shown as a plain value, `x/max`, or `x (MAX-n)` — with a potential variant; `Midspin` hits are excluded from the judged-tile count, matching the game's scoring. Disabled on r148 where the game tracks no XScore
- **Potential values** for Accuracy / XAccuracy / XScore (math verified against the r150 `CalculatePercentAcc` source): "what the final value becomes if everything remaining is perfected". Accuracy potential reverse-engineers the game's effective denominator (the native formula lets acc exceed 100% — every Perfect adds +0.01%); XAccuracy potential mirrors the game's own `maxPossibleXAcc`. Modes: Current / Potential / Both / Both-in-one-line, with dedicated stack elements for the separate-line modes (single-player; coop renders inline per player)
- **Per-text decimal places**: Progress / Accuracy / XAccuracy / Best / Timing each get their own 0-4 (0-5 for Timing) slider in their settings section; the old global slider now only governs the extension texts (FPS, Start)
- **Timing auto-hit filtering**: autoplay, per-player auto and auto-floor hits no longer enter the timing statistics — on r149+ those hits carry the ideal timestamp (≈0 ms) and were dragging the average toward zero
- **Settings now declare what each text type means**: every mode selector got a distinct label (分数格式 / 潜力值显示 / 显示模式 — formerly four selectors all named「文本类型」), the cycle buttons show localized value names with meaning (仅当前值 / 仅潜力值 / 当前+潜力（两行）/ 当前 (潜力) 一行; 纯分数 / 分数 / 满分 / 分数 (MAX-n)…) instead of raw English enum names, and a dimmed one-line hint under each selector defines the concept — potential value, XScore scoring (X=2 / Perfect±=1, r149+ only), hit offset vs. average, and the coop inline merge — in all three languages

### Bug Fixes

- **XScore / 潜力值文本始终不显示**：`JongyeolDisplayOrder` 的字段初始值仍只到上一次新增元素为止（`10..15`），而 `Settings.Load` 只在数组为 null 或空时才回退到 `GetDefaultJongyeolOrder()` —— 于是五个新元素（`AvgTiming`/`XScore`/`P.Accuracy`/`P.XAccuracy`/`P.XScore`）从未被布局遍历到，新装与老配置都中招。默认数组已补全，`Load` 还会把保存的顺序里缺失的默认元素追加到末尾，已有 `Settings.json` 也会被修好
- **只开 XScore 时整块不可见**：主容器 `SetActive` 与 `Show` 的布局门控漏了 `ShowXScore`，关掉进度、只留 XScore 会让整条栈隐藏；两处改用新增的 `Settings.AnyStackedTextVisible`
- **关掉精度/X精度后 XScore 不更新**：精度补丁（`ScrMistakesCalcAccPatch` / `ScrMarginCalcAccPatch`）与逐格补丁只按 `ShowAccuracy || ShowXAccuracy` 注册，两者都关时 XScore 冻结在初始空串；`ShowXScore` 现已并入这些注册门控
- **「仅潜力值」模式下主文本行残留（冻结旧值）**：`SetDualText` 在 Potential 模式只写 `P.*` 文本、永不写主文本，但 `IsJongyeolElementEnabled` 的主元素门控不看 `*TextType` —— 主文本行继续占着栈位，且游戏中途从当前值切到仅潜力值后主文本永远停在切换前的旧值。主行现随模式隐藏（coop 例外：潜力值内联进主文本，主行恒显）
- **coop 潜力 X 精度分母错用 `seqID`**：`SetXAccuracy` 直接把 `seqID` 当已判定格数外推，未经 `GetJudgedTiles` 扣除 Midspin（r149+），含 Midspin 的图里潜力 X 精度有偏差；现与单人路径一致
- **XScore 满分途中显示 `MAX--2`（结算才恢复）**：游戏源码 `scrPlanet.cs` 里 `AddHit`（我们的更新在其内部的 `CalculatePercentAcc` 后缀触发，`hitMarginsCount` 已含本次）先于 `MoveToNextFloor` 更新 `currentSeqID`——打击瞬间 `seqID` 恒落后一格，`judged = seqID − midspin` 少算一格，满分会显示负的 MAX 差值，直到通关时序落定才变回 `MAX-0`。`GetJudgedTiles` 改为对 `hitMarginsCount` 求和（与 XScore 同一瞬间读取、天然自洽），不再依赖 `seqID`；检查点重试时游戏清零 `hitMarginsCount`（r150 2026-09-13 起在 `scnGame.Play` 里显式调用 `marginTracker.Reset()`），求和式随之归零，与原生 acc 重开同口径
- **XScore 的 MAX−n 与游戏结算完全同口径**：r150 结算界面公式为 `xScore (MAX-{maxXScore − xScore})`（`DetailedResults.cs:92`，普通局 num7=0）。现反射直读原生 `xScore`/`maxXScore`（公开成员，反射以过 compat-r148 编译门禁），实时显示为 `maxXScore − xScore − 2×剩余玩家打击格`——结算时剩余为 0、与游戏逐字相等；开局为 0；TooEarly 这类不前进格子的多余按压不影响分母（此前两版分母分别把 auto 地板算多、把多余按压算多，实测与结算差 +2 / +16）。潜力值 = 剩余玩家打击格全 XPerfect 的收敛值，目标是全图满分常量。另在 `MoveToNextFloor` 后缀补一次同帧刷新，消除 `currentSeqID` 滞后一格造成的瞬间毛刺
- **XScore 的 `(MAX-n)` 两处冗余省略**：n == 0（未掉分）时不再显示 `(MAX-0)`；潜力值的 MAX−n 恒等于当前值的（两者都 = maxXScore − xScore，潜力只是两边同加 2×剩余格），不再重复展示——一行双值从 `0 (MAX-0)(1022 (MAX-0))` 变为 `0 (1022)`

### Refactor

- **移除 AssetBundle 管线，默认字体改为内嵌自释放**：bundle 里实际只有一份 698KB 的 OTF 字体和一个三个 Image 全部使用 Unity 内置 `UISprite` 的 prefab —— 却为此维护着一个 Unity 编辑器工程、三份 bundle 二进制（其中 `AssetBundles2022` 与 `AssetBundles6000` 字节完全相同且 manifest 都写着 `UnityVersion: 6000.3.16f1`，双包拆分早已名存实亡）以及一份 4.29MB 的 `jipperresourcepackbundle` 死资产。现在：字体作为 `EmbeddedResource` 直接内嵌主 DLL（不经压缩，无需打包脚本、无 gitignored 构建输入、全新克隆零额外步骤），首次运行释放到 `ModPath\assets\`——仅写缺失文件，用户替换过的永不覆盖，`.tmp` + `Move` 原子落盘；进度条改为纯代码构建，逐字段对齐原 prefab（子物体顺序、锚点、pivot、尺寸、颜色、Sliced 类型）。整个 `JipperOverlayer-Unity`（63 个文件、约 44MB）与全部 bundle 产物删除，`BundleLoader` 由 `AssetLoader` 取代，CI 打包步骤与 README 同步精简
- **清理与防御**：`AssetBundles/`（无后缀）目录及其 4.29MB `jipperresourcepackbundle` 运行时零引用，已一并清除；代码重建的进度条在取不到 Unity 内置 `UISprite` 时退化为直角矩形并记日志，不再静默出错（原 prefab 的 Sliced 类型与 11px 九宫格边框在 14px 高的元素上本就退化为直角，此处保持原观感不变）

## v1.1.5 — 2026.09.12

### Features

- **Judgement timing window display** (PR #5 by Wang125510, closes #4): four judgement tiers (optional XPerfect / Perfect / Great / Good) shown as ±milliseconds appended under the BPM block, computed through the game's own boundary function so the numbers match real judgement exactly (on r148+external XPerfect mod the X row follows the mod's `max(15° × marginScale, 16.67 ms)`; on r149+ it reads the native boundary, which after the r150 hotfix is `max(12.5° × marginScale, 16.67 ms)` and scales with pitch). Adds the `ShowTimingWindow` toggle, four customizable tier labels, and EN/KO/CN localization
- **One build for r148 and r150** (`GameCompat` / `HitMarginCompat`): every r150 breaking change is absorbed by runtime probing — the 12→16-value `HitMargin` enum is resolved by name (no compile-time constants anywhere), `GetAdjustedAngleBoundaryInDeg` is bound per actual signature, the removed `scrMisc.GetHitMargin` is replaced by version-selected `GetHitMarginInDeg`/`GetHitMarginInSec` patches, and `CalculatePercentAcc` picks its overload at runtime. The source compiles clean against both DLL sets; `VerifyGameApi` now checks signatures and version-disjoint groups instead of names only, and CI gained an r148-baseline gate job plus API verification on both
- **Jongyeol master switch removed**: the six extra texts (FPS / Author / State / Death / Start / Timing) are now independent toggles sharing one stack and one order editor with the main texts; EL-perfect combo, orange combo, pseudo-BPM and hide-debug became standalone behavior toggles (orange combo now also works in the normal combo path); new explicit style toggles `DetailedProgress` (cur/total \[-remaining\]) and `TimeDecimals`; decimal precision defaults to 2 and applies to all texts. Legacy configs migrate once based on the presence of the old `JongyeolMode` key — ex-Jongyeol users keep their look, ex-normal users keep theirs, and the migration never re-fires after the first save

### Bug Fixes

- **r150 silent data corruption**: judgement counts, death totals, pure-perfect checks and combo classification all used raw `HitMargin` indices that shift wholesale on r149+ (e.g. `h[3]` becomes PerfectMinus, Auto moves 10→12) — now resolved by name per running version
- **±Perfect display order**: the XPerfect breakdown printed `+ X −`, putting slightly-early counts in the right-hand slot; now `− X +`, matching the early-left/late-right convention of the whole line (and `HitMarginHelper` on r150)
- **Midspin keeps combo** on r149+: midspins no longer reset the combo or trigger the non-perfect title
- **XPerfect colour follows the game on r150** (`colourXPerfect`, white by default) instead of the external mod's blue `#4DCCFF`, which remains the r148 fallback
- **Timing data flow**: the hit-margin patches are now applied unconditionally with the `ShowTiming` gate inside `UpdateTiming` — previously the patch only mounted if a `RefreshPatches` happened after the toggle, leaving the Timing text frozen until some other switch was flipped; `_timings` also self-initializes
- **Settings migration is one-shot**: the legacy-Jongyeol migration only runs while the old `JongyeolMode` key is still present in the JSON — a missing key no longer re-disables the user's toggles on every launch; the XML fallback also migrates before serializing (its results previously never reached disk)
- **Stack layout guard**: `Show()` now lays out the text stack when any stackable element is enabled — previously an Accuracy-only setup never positioned its texts

- **XPerfect probe hardening**: `EnsureInitialized` wraps the UMM probe in try/catch and permanently disables probing when the UnityModManager assembly is absent (pure MelonLoader installs), instead of throwing once per frame
- **MelonLoader `ModEnabled` preference honored**: startup no longer auto-enables the mod when the preference is off; a lightweight per-frame watcher enables/disables live as the preference changes
- **Best-record multiplier key**: `LastMultiplier` now uses song pitch only — planet speed (which drifts mid-level with BPM events) no longer splits one map's best records across multiple keys; attempt counting already used pitch-only
- **Repository.json update source**: the download URL now points at the actually published `JipperOverlayer-UMM.zip` release asset
- **Lazy patch poller busy-wait**: the 100 ms `Task.Delay` moved outside the scene-loading branch, so the background loop yields during scene loads instead of spinning
- **Time-text color NaN guards**: all six `time / totalTime` divisions (Overlay + JongyeolModule, music/map time) fall back to white when the total duration is unavailable
- **Custom labels apply instantly**: editing any of the 31 labels (including while paused) forces a full overlay text refresh via the new `Overlay.RefreshAllTexts` / `IOverlayTextManager.DirtyTextCaches` — previously Checkpoint/Best/BPM/TimingScale/Death/Author/Timing edits stayed stale until their underlying value next changed
- **Combo title state consistency**: `Show()` syncs the custom main label on fresh starts (checkpoint keeps keep the alt title as designed); label edits respect the Jongyeol alt-title state via the new `IsAltComboTitle`
- **Language preset buttons refresh**: applying the EN/KO/CN presets now routes through the overlay refresh path
- **Out-of-range language clamp**: `Settings.Load` clamps a hand-edited `CurrentLanguage` back to EN/KO/CN instead of crashing every GUI draw

### Performance

- **Timing window per-frame cost**: `TimingWindowCalculator` memoizes its result keyed on every input the game function secretly reads (`GCS.difficulty` / `currentSpeedTrial` / `HITMARGIN_COUNTED`, plus bpm × speed, pitch, marginScale, XPerfect availability) — unchanged inputs skip the four game-function calls entirely; the ms-conversion lambda became a static method (no per-frame closure allocation); persistent failures warn once instead of every frame. The overlay throttle compares rounded whole milliseconds, so TMP re-meshes only when a displayed value actually changes
- **Settings GUI throttle**: `OnGUI` runs the full `RefreshVisibility` pass only during the Layout phase or after an actual edit, instead of on every IMGUI event
- **Coop text pooling**: per-player death/state rendering reuses a static `StringBuilder` instead of allocating per call
- **Slide inputs keep raw text**: slider text fields preserve intermediate input (cleared box, minus sign) instead of snapping back, matching the `PosSlide2` behavior

### Removed

- `ShadowManager.ApplyDarkShadow` — an exact duplicate of `ApplyShadow`; combo texts follow the global text-effect settings like every other slot

## v1.1.4 — 2026.08.12

### Features

- **Asynchronous lazy-loaded patches** (PatchManager): register patches with a `lazyTrigger` — applied asynchronously only when both the toggle and the trigger allow, without touching the update loop. A background poller applies/unapplies on demand, isolates permanently failing patches (`_failedPatches`) instead of retrying every tick, and is torn down safely on disable/quit (cc616d2)
- **PatchManager scene-awareness**: the lazy poller pauses while a scene loads and cancels on application quit; `Initialize` now resets every cache and the quitting flag so re-enabling starts clean (7a0d892)

### Performance

- **GameRefs centralized state access layer**: new static facade (`GameRefs`) exposes controller/conductor/level-maker/RDC/etc. through delegate calls instead of scattered reflection and direct type access; `PatchManager` gained `CreateMemberGetter` and friends with full delegate caching, so high-frequency reads cost a plain delegate call (b66d84b)
- **Pre-baked hex color LUT**: `ColorPerDictionary` bakes a 256-step RGB/RGBA hex lookup table (built through `ColorUtility`, byte-identical output) and the Progress / BPM / coop Accuracy hot paths now read it by index instead of interpolating a color and converting to hex per frame; the LUT invalidates itself on any color edit. The per-hit player nameplate hex in `VersionSafe` is memoized. All rich-text overlay colors now carry alpha (single-player Progress included, matching the color editor's alpha slider). Removed the unused `BpmCalculator.ColorToHex`
- **Dynamic glyph pre-baking**: the bounded overlay charset — ASCII printable, every character used by the three UI languages, and user-custom labels — is baked into all TMP dynamic font atlases at boot via `TMP_FontAsset.TryAddCharacters` (overload resolved at runtime across the string/`uint[]`/`IEnumerable<char>` signatures TMP versions use; multi-atlas enabled; static atlases skipped in silence). Removes main-thread atlas re-rack + texture-rebuild spikes on first render and on EN/KO/CN language switches. Glyphs a font can't produce (e.g. Hangul in a Latin-only ttf) simply stay unbaked and keep falling back to the CJK font
- **XPerfect probing gated**: when XPerfect is not installed the probe is disabled permanently (it can't be installed without a restart); when installed-but-disabled it keeps polling every frame (it may be enabled in-game); when active it stops after the first successful cache and recovers automatically via the toggle event when disabled again

### Bug Fixes

- **Level-name position no longer reset**: `ApplyLevelNamePatch()` / `ResetLevelName()` keep `scrController.txtLevelNameOriginalPosition` in sync with the applied position, so later `SetDefaultText` events no longer yank the title back to its pre-patch position (84fd26f)
- **No more stray best records from mid-level starts**: a run is only saved/settled when it started from floor 0 (`_lastSavedFromStart`); checkpoints and mid-level restarts no longer pollute the best/attempt stats (3b300a2)

### Refactors

- **Overlay code decoupled from concrete game types**: all state access now goes through `GameRefs`; `VersionSafe` and callers were adapted, and `.csproj`/`.gitignore` updated alongside (b66d84b)
- **Pause handling flattened**: removed the `_lastPaused` edge-detection field; canvas visibility now reads `GameRefs.IsPaused` directly each frame in `OverlayMono` (ac86aea)
- **SeedProgress**: progress managers seed the start tile explicitly on show via the new `IOverlayTextManager.SeedProgress` (single-player and coop), giving an immediate refresh when progress display is enabled and showing the start-progress range for mid-level starts; start progress is now calculated as `(floor + 1) / totalTiles` (02a0553)

## v1.1.3 — 2026.07.16

### Features

- **Per-section fonts & font sizes**: separate font and font-size configuration for each overlay section (Main, BPM, Judgement, ComboTitle, ComboVal, Timing, Attempt); "Use Global Font" toggle per slot (ac9a396)
- **UI patch toggles**: independent boolean toggles in Settings — Patch Beta Watermark, Patch Level Name, Reposition Auto Text — each can be enabled/disabled and dynamically reset (46b9dcc)
- **Text effects panel**: global shadow (TMP Underlay) and outline (TMP Outline keyword) with toggles, RGBA color pickers, Width/Softness sliders; changes apply live via `ApplyFontToAll()` + `ShadowManager.ClearCache()`
- **Trilingual localization**: all new UI sections fully translated (EN/KO/CN) via `Tr.Get()` — Text Effects (session) + Section-level font options + UI toggle labels (46b9dcc)
- **Dual-loader architecture**: support for both MelonLoader and UMM via `IModLoader` interface; separate entry assemblies (`JipperOverlayer.Loader.Melon` / `.UMM`); platform-specific build artifacts (e985450)

### Performance

- **Music time throttle**: update interval tightened from 1s → 0.1s (`_lastMusicTimeTick`) for smoother display at minimal CPU cost; added clip‑null/zero‑length fallback using last floor `entryTime` as total duration (71d73ec)

### Bug Fixes

- **Toggle UI**: checkbox and label merged into a single clickable area (c2e80a4)
- **Level name position drifts after death**: `Hide()` now calls `ResetLevelName()` to restore original position/scale/size before nulling cache fields, preventing cumulative offset on scene restart
- **Level name size not restored on mod disable**: `Destroy()` now calls `ResetLevelName()` so scale/sizeDelta revert when mod unloads

### Refactors

- **PatchManager**: removed `Main` dependency; `HarmonyId` stored internally instead of requiring assembly‑scoped id (9f989fc)
- **Awake_Rewind patch removed**: level‑name positioning moved from Harmony patch into `Overlay.UpdateSize()` / `Show()`, giving finer control over when and whether the patch applies (46b9dcc)
- **Extracted `ApplyLevelNamePatch()`**: unified level-name positioning logic from `Show()` and `UpdateSize()` into a single method; always applies patch on `Show()` regardless of cache state (separated save-once from apply-always)
- `ResetLevelName()`: always nulls cache fields at exit (removed early-return skip), ensuring clean state

## v1.1.2 — 2026.07.08

### Features

- **Alpha slider in color editor**: RGBA four-slider layout replaces the previous RGB-only UI; hex input already supported 8-char alpha values

### Bug Fixes

- **Jongyeol `song.time` log spam**: added `_lastMusicTimeSec` guard to `UpdateTime()`, preventing per-frame `AudioSource.time` access when `song.clip` is null (r142+ levels without standard audio clips); log no longer flooded with Unity warnings
- **v136 XPerfect per-player fallback**: when XPerfect doesn't provide `GetPlayerXPerfectCount` methods (v136), per-player getters now redirect to main static values instead of always returning 0
- **DetectApiVersion static property binding**: fixed `CreatePropertyGetter<T, F>` → `CreateStaticPropertyGetter<TField>` for `ADOBase.playerManager` (was throwing on r14x, causing version detection to fall back to v136 path and breaking judgement/XAccuracy display)

### Refactors

- **PatchManager thread safety**: added `lock (_lock)` to all public methods; `List<Type>` → `HashSet<Type>` for O(1) `Contains` lookups
- **Cached reflection utilities**: added 9 helper methods (`GetMethodInfo`, `GetFieldInfo`, `CreateFieldRef`, `CreatePropertyGetter/Setter`, `CreateStaticFieldGetter/Setter`, `CreateStaticPropertyGetter/Setter`) with dictionary caching; used by `VersionSafe` v136 path and `ShadowManager`
- **VersionSafe v136 bindings**: replaced bare `GetField`/`GetValue` reflection with PatchManager cached variants; missing fields now log a warning before falling back to defaults
- **Settings JSON migration**: replaced UMM default XML serialization with `Newtonsoft.Json`; existing `Settings.xml` auto-migrates to `Settings.json` on first load, then deletes the old XML file

## v1.1.1 — 2026.06.04

### Features

- **ComboLineReversed**: new toggle to swap the vertical order of the combo number and combo title label; animation anchor adapts to both orientations (3 Tr keys: EN/KO/CN)
- **ShowAutoInXPerfect**: when XPerfect mode is active, optionally show the auto-tile perfect count in orange (`#FF8000`) after the `−Perfect` value

### Refactors

- **Jongyeol State/Death/PurePerfect via `IOverlayTextManager`**: moved all three Jongyeol-mode helper methods (`UpdateDeath`, `UpdateState`, `CheckPurePerfect`, `GetTooJudgement`) out of `JongyeolModule` and into `IOverlayTextManager`; both `OverlayTextManagerNormal` and `OverlayTextManagerCoop` now implement them, enabling full coop-aware Jongyeol display
- **Coop Jongyeol Death/State**: `OverlayTextManagerCoop` now renders per-player death counts and state labels in each player's color, with `IsPurePerfect` checked independently per player
- **`_mono` field cache**: `OverlayMono` component reference stored as `_mono` field at construction; replaces repeated `GetComponent<OverlayMono>()` calls in `UpdateCombo`, `Show()`, and `Hide()`
- **Static `StringBuilder` pools**: `_judgementSb`, `_attemptSb`, `_bpmSb`, `_comboSb`, `_timingSb` declared as static fields; eliminates per-frame heap allocations across `BuildJudgementString`, `UpdateAttempts`, `BuildBpmText`, `UpdateCombo`, and `UpdateTimingScale`
- **`UpdateTimingScale` early-exit**: value cached in `_lastTimingScale`; text rebuild skipped when scale changes less than 0.001%
- **`OutExpoChange` lookup table**: combo animation easing replaced with a 31-entry pre-built float array (`_expTable`), removing `Math.Pow` calls per animation frame
- **`OverlayMono.Update` guard**: added `!Overlay.GameObject.activeSelf` check to skip update loop when overlay is hidden

## v1.1.0 — 2026.06.02

### Features
- Full Jongyeol color customization: 3 gradients (JCombo/JDeath/JTiming) + 11 static colors (8 states + FPS/Author/Start)
- Display order: reorder main stack elements with ▲▼ (General 7 items, Jongyeol 13 items)
- BPM line order: reorder TBPM/CBPM/KPS lines independently
- BPM line visibility: per-line ✓ toggle to show/hide each BPM line
- Attempt line order: swap Attempt/Full Attempt display order
- 14 new Tr keys for state colors + 23 keys for display order (EN/KO/CN)

### Refactors
- Settings folder namespace: JipperOverlayer.Overlayer.Settings → JipperOverlayer.Overlayer (fixes Settings type collision)
- DrawReorderList helper: unified ▲▼/✓ UI for all order lists, 3 callers share 1 implementation (-45 lines)
- BPM text building: extracted shared BuildBpmText() static method, used by both General and Jongyeol mode

### Bug Fixes
- ColorPerDictionary.GetColor cache: added noCache parameter for static color updates
- EnsureDefaults() migration: detect stale colors.json and reset transparent (a==0) colors
- FPS/Author/Start text: label white via <color=white>, value takes configured color
- BPM/Combo colors: added missing reset buttons in General section
- State colors: all 8 conditions fully translated with "Color"/"색상"/"颜色" suffix
- Empty reference guard: null-check in SetupLocationMain to prevent NRE from stale config
- Config migration: auto-filter invalid IDs from display order arrays on load
- BPM cache: DirtyBpmCache() forces text rebuild when visibility/order changes

## v1.0.8 — 2026.06.02

feat: integrate XPerfect counter into judgement display

Add optional integration with XPerfect mod:
- Detect XPerfect availability via UnityModManager and cache delegates
- Replace perfect counts in judgement line with +Perfect / X-Perfect / -Perfect
- Ensure correct execution order with HarmonyAfter
- Handle dynamic enable/disable of XPerfect via OnToggle event
- Add settings toggle "Show XPerfect in Judgement"

## v1.0.7 — 2026-05-31

- All custom positions changed to pixel offsets (position += offset), not affected by alignment
- New PosSlide2: XY on the same line, -2000~2000 range, integer pixel values
- Tr.cs: Added Coop and 11 position tags Key, trilingual translation
- Position grouping fold: Main/BPM, Judge(P1~P4), Others
- FPS refresh rate slider indented below ShowFPS, hidden when turned off
- DecimalPrecision remove extra {} blocks, indentation alignment
- Attempt added Coop independent offset field
- ApplyFontToAll remove redundant try-catch
- Configuration migration: ConfigVersion 0→2, old PX/PY converted to offset

- Change JudgementText/_judgementObject to [4] array
- SetupLocationJudgement: P1/P3 x=-250, P2/P4 x=250  First row y=35, Second row y=5 (same as single-player default height)
- UpdateJudgement: Read per-player marginTrackers in coop mode
- Settings: P1~P4 JudgePX/PY sliders, default values aligned with two-column layout
- Move Attempt text to x=550 in multiplayer mode to avoid overlap
- In Show(), set up SetupLocationJudgement first, then UpdateJudgement

- Title text updates in real time, pausing switching Jongyeol does not lose text, DecimalPrecision injection, code cleanup

## v1.0.6 — 2026-05-30

### Bug Fixes
- Fix version detection for game API changes: detect v141+ via scrMarginTracker and ADOBase.playerManager instead of removed scrController.playerManager
- Fix percentAcc/percentXAcc delegate bindings: use ADOBase.playerManager (static) instead of scrController.instance.playerManager (removed property)

## v1.0.5 — 2026-05-30

### Features
- Customizable text labels: all overlay labels can be customized via Custom Labels settings panel
- Label presets: English / Korean / Chinese one-click presets
- FPS refresh rate slider (0.05~1.0s) for Jongyeol mode
- Settings UI reorganized with collapsible panels (General/Display/Jongyeol/Alignment/Labels)

### Refactors
- Replaced JOverlay inheritance with JongyeolModule composition (-405 lines)
- Removed all unused virtual keywords (Overlay 13, OverlayTextManagerCoop 4, OverlayTextManagerNormal 3)
- Renamed YellowCombo → AllowELCombo for accurate naming (EL = Early/Late judgment)
- Settings.OnGUI split into 5 collapsible sections with sub-folders

### Bug Fixes
- PlanetMoveToNextFloorPatch: include Jongyeol settings in registration condition
- JCombo patches: require ShowCombo guard
- Show(): call SetupLocationMain when Jongyeol is active regardless of standard settings
- RefreshVisibility: actively refresh BPM/Combo/Judgement/TimingScale/ProgressBar when toggled on
- Fix UpdateDeath division-by-zero when currentSeqID == StartTile
- Fix GUI.changed false-positive triggering unnecessary overlay updates
- Fix time label cache not refreshing when edited in Custom Labels panel

## v1.0.4.2-preview — 2026-05-30

- Fix Chinese translations
- Fix value update issues when toggling settings

## v1.0.4.1-preview — 2026-05-30

Same as v1.0.4, preview release for testing.

## v1.0.4 — 2026-05-30

### Refactors
- Replaced JOverlay inheritance with JongyeolModule composition (-405 lines)
  - Deleted JOverlay.cs, JOverlayTextManagerNormal.cs, JOverlayTextManagerCoop.cs, IJOverlayTextManager.cs
  - Created JongyeolModule.cs as composable module
  - Overlay fields changed from protected to internal for JongyeolModule access
  - PurePerfectColor changed to public static readonly
- Removed all unused virtual keywords (Overlay 13, OverlayTextManagerCoop 4, OverlayTextManagerNormal 3)
- Removed redundant Jbpm.BpmColorMax wrapper; callers use Main.Settings.BpmColorMax directly
- Renamed YellowCombo → AllowELCombo for accurate naming (EL = Early/Late judgment)
- Removed redundant UpdateState() call in RdcSetAutoPatch

### Bug Fixes
- PlanetMoveToNextFloorPatch: include Jongyeol settings in registration condition so State/Death/Start/Timing update when all standard settings are off
- JCombo patches: require ShowCombo to prevent combo updating when disabled
- Show(): call SetupLocationMain when Jongyeol is active regardless of standard settings
- RefreshVisibility: actively refresh BPM/Combo/Judgement/TimingScale/ProgressBar when toggled on

## v1.0.2 — 2026-05-29

### Features
- Configurable text alignment: per-element 3x3 alignment grid (TL/T/TR/L/C/R/BL/B/BR)
- Font style toggles: Bold, Italic, Underline, Strikethrough, Highlight per element

### Fixes
- Custom fonts: shadow material cache keyed by font asset (not alpha), GetFontMaterial
  reflection for cross-Unity-version compatibility, font selection persisted by name
- Font list no longer polluted by other mods' file-loaded fonts (path-name filter)
- PlayCount.Save: null-data Hash keys no longer cause NRE
- Combo "Perfect" text animation restored after ContentSizeFitter removal
- RefreshPatches empty catches now log warnings
- PatchManager.ApplyAll skips already-applied patches (no double-patch)
  Fix Combo title-value spacing: restore ContentSizeFitter with Unconstrained width

- horizontalFit = Unconstrained (keep 300px width for alignment)
- verticalFit = PreferredSize (auto-height, proper title-to-value spacing)
- OverlayMono.ComboAnim reverted to sizeDelta.y (not preferredHeight)

  Remove planet speed from PlayCount Multiplier

  Multiplier no longer includes VersionSafe.GetPlanetSpeed (which changes
  mid-level with BPM events), only song.pitch (constant per level).
  Fixes attempt count key mismatch when speed changes during gameplay.

### Refactors
- RegisterChangeStatePatch: 30-line reflection search replaced with direct [HarmonyPatch]
- Game API method targets use nameof() where compile-accessible
- Tr.cs: removed obsolete Get(string) overload and _keyMap dictionary
- All settings labels unified through Tr.Get(Key) (no hardcoded strings)
- RegisterPatchesSafe removed (dead duplicate)

### Architecture & Performance
- OverlayMono MonoBehaviour: per-frame update moved out of UMM OnUpdate
- Combo animation: Stopwatch polling replaced with coroutine (zero idle cost)
- OverlayMono disabled when overlay hidden (no per-frame overhead in menus)
- Merged 3 MoveToNextFloor Harmony patches into 1 (fewer detours per tile)
- Tr.cs: flattened to array index instead of Dictionary lookup
- StringBuilder for BPM/Judgement/FPS text building
- ColorToHex: char array lookup instead of ToString(X2)
- Shadow materials cached by alpha
- ColorPerDictionary: cached GUIStyle, one-entry color cache
- Coop string arrays cached (no re-allocation per update)
- JOverlay timing list: running sum instead of O(n) per hit
- Time labels cached, only rebuilt on change

### Bug Fixes
- PlayCount.Save: write to .tmp first, then atomically replace (was truncating file on failure)
- Added null guard in Save() preventing empty file writes
- ColorChanged no longer calls redundant RefreshVisibility on every edit
- RepositionAutoText caches component reference (was FindObjectsOfTypeAll per frame)
- PlayCount data now persists correctly across sessions
