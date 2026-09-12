# JipperOverlayer
![C#](https://img.shields.io/badge/Lang-C%23-c9c8e4?logo=csharp)
![Visual Studio 2022](https://img.shields.io/badge/IDE-Visual%20Studio%202022-5C2D91?logo=visualstudio&logoColor=white)
[![Downloads](https://img.shields.io/github/downloads/2228293026/JipperOverlayer/total)](https://github.com/2228293026/JipperOverlayer/releases/latest)
[![Build](https://github.com/2228293026/JipperOverlayer/actions/workflows/build.yml/badge.svg)](https://github.com/2228293026/JipperOverlayer/actions/workflows/build.yml)

An in-game overlay mod for **A Dance of Fire and Ice (ADOFAI)** that displays progress, accuracy, BPM, combo, judgement, and more. Supports both **Unity Mod Manager** and **MelonLoader**.

## Features

- **Real-time Overlay** — Progress, Accuracy, XAccuracy, Music/Map Time, Checkpoints, Best Record
- **BPM Display** — Tile BPM, Current BPM, KPS, with pseudo-BPM detection
- **Combo Counter** — Animated combo display with color gradients
- **Judgement Display** — Hit margin breakdown (Miss, Bad, Good, Perfect, etc.)
- **Timing Scale** — Current timing scale percentage
- **Attempt Tracker** — Per-map attempt count with persistent storage
- **Progress Bar** — Visual progress indicator
- **Jongyeol Mode** — Extended overlay with FPS, State, Death count, Start position, Timing analysis, Debug text hiding
- **Co-op Support** — Per-player display for multiplayer
- **Text Effects** — Global shadow (TMP Underlay) and outline with toggles, RGBA color pickers, Width/Softness sliders
- **Per-Section Fonts** — Independent font and font-size per overlay slot (Main, BPM, Judgement, Combo, Timing, Attempt)
- **UI Patch Toggles** — Independent switches for Beta watermark, level name position, auto text reposition
- **Color Editor** — Interactive gradient editor for all overlay colors
- **XPerfect Integration** — Optional enhanced perfect display via [XPerfect](https://github.com/8100print/XPerfect)

## Settings UI Languages

- English
- 한국어 (Korean)
- 中文 (Chinese)

## Installation

### Unity Mod Manager
1. Install [Unity Mod Manager (UMM)](https://www.nexusmods.com/site/mods/21)
2. Download the **UMM** variant from [Releases](https://github.com/2228293026/JipperOverlayer/releases)
3. Install the mod via UMM, or extract the zip to `ADOFAI/Mods/JipperOverlayer/`

### MelonLoader
1. Install [MelonLoader](https://melonwiki.xyz/)
2. Download the **MelonLoader** variant from [Releases](https://github.com/2228293026/JipperOverlayer/releases)
3. Extract to `ADOFAI/Mods/JipperOverlayer-melon/`
4. Press **F7** in-game to open settings (rebindable)

## Manual Installation

### UMM variant
```
ADOFAI/Mods/JipperOverlayer/
├── Info.json
├── JipperOverlayer.dll
├── JipperOverlayer.Loader.UMM.dll
```

### MelonLoader variant
```
ADOFAI/Mods/JipperOverlayer-melon/
├── JipperOverlayer.dll
├── JipperOverlayer.Loader.Melon.dll
```

> **No asset files ship with the package.** The default font is embedded in `JipperOverlayer.dll`
> and self-extracts to `assets/MAPLESTORY_OTF_BOLD.OTF` on first run — only when that file is
> missing, so a font you replace yourself is never overwritten. The progress bar is built in code.

## Requirements

- A Dance of Fire and Ice (Steam version)
- One of: Unity Mod Manager 0.22.14+ **or** MelonLoader
- Supports game versions v136 and v141+

## Build from Source

### Prerequisites

- Visual Studio 2022+ with .NET Framework 4.8.1 SDK
- Steam installation of ADOFAI (for reference DLLs in `Libs/`)

> **Note:** `Libs/` is `.gitignore`d. Run `cp -r "$ADOFAI/A Dance of Fire and Ice_Data/Managed/"* Libs/` to populate it from your game installation (requires one-time setup).

> The default font lives at `JipperOverlayer/assets/MAPLESTORY_OTF_BOLD.OTF` and is embedded into the DLL at compile time — no packing step, and no Unity editor project is involved.

### Build

```bash
# Solution (core + both loaders)
msbuild -restore -p:Configuration=Release

# Or individual projects
msbuild JipperOverlayer/JipperOverlayer.csproj -restore -p:Configuration=Release
msbuild JipperOverlayer.Loader.UMM/JipperOverlayer.Loader.UMM.csproj -restore -p:Configuration=Release
msbuild JipperOverlayer.Loader.Melon/JipperOverlayer.Loader.Melon.csproj -restore -p:Configuration=Release
```

Compiled outputs:
- `JipperOverlayer/bin/Release/JipperOverlayer.dll`
- `JipperOverlayer.Loader.UMM/bin/Release/JipperOverlayer.Loader.UMM.dll`
- `JipperOverlayer.Loader.Melon/bin/Release/JipperOverlayer.Loader.Melon.dll`

## CI/CD

This project uses GitHub Actions for automated builds:

| Trigger | Action |
|---------|--------|
| Push to `master` | Build + package artifact |
| Pull request to `master` | Build verification |
| Tag `v*.*.*` | Build + package + GitHub Release |
| Tag containing `-` (e.g. `v1.0.0-pre1`) | Prerelease |

## License

- Primarily **MIT License** — see [LICENSE](./LICENSE.txt).

- Code adapted from [JipperResourcePack](https://github.com/Jongye0l/JipperResourcePack) by Jongyeol is under **BSD 3-Clause** — see [LICENSE-BSD](./LICENSE-BSD).

