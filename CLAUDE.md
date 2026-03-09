# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```powershell
dotnet build src/WidescreenTools/WidescreenTools.csproj -c Release
```

Output lands in `artifacts/bin/` (`WidescreenTools.dll` + `manifest.json`). After a successful build the MSBuild target `DeployModToTerrariaModsFolder` automatically copies both files into `{TerrariaInstallDir}/TerrariaModder/mods/widescreen-tools/`. To skip the deploy:

```powershell
dotnet build src/WidescreenTools/WidescreenTools.csproj -c Release -p:EnableTerrariaModderDeploy=false
```

There are no automated tests and no lint tooling.

## External dependencies

The project references three things that are **not** in this repo:

| Dependency | Default path |
|---|---|
| `Terraria.exe` | `C:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe` |
| `Microsoft.Xna.Framework.dll` | Same Terraria dir, or Windows GAC fallback |
| `TerrariaModder.Core.dll` | `ref/TerrariaModder.Core/` is a stub compile-only project; the real DLL ships with TerrariaModder |

Override paths via MSBuild properties `TerrariaInstallDir` or `XnaFrameworkPath` if needed.

All patches target **Terraria 1.4.5 + TerrariaModder** on **.NET Framework 4.8 / Windows**. `System.Windows.Forms` and `user32.dll` P/Invoke are used directly.

## Architecture

### Entry point — `Mod.cs`

`Mod` implements the TerrariaModder `IMod` interface. It:

- Loads config via `_context.Config.Get(key, default)` in `LoadConfigValues()`.
- Derives `_worldViewWidth`/`_worldViewHeight` at load time from `desiredResolutionWidth`/`desiredResolutionHeight`, falling back to `Screen.PrimaryScreen.Bounds` when either is 0, then clamping to at least `VanillaWidth`/`VanillaHeight`. These are **computed fields**, not config-backed.
- Drives a per-frame loop via `FrameEvents.OnPostUpdate`: applies overrides when dirty, tracks resolution changes, and debounces config saves (500 ms after last change).
- Persists active resolution to config automatically via `TrackResolutionChanges` → `FlushPendingResolutionSave`.

### Static override helpers

**`WidescreenZoomOverride`** — owns `Main.MaxWorldViewSize`.
This field controls Terraria's minimum zoom on large viewports. It may be `InitOnly`, so the class strips the `InitOnly` flag from `m_fieldAttributes` via reflection when a direct `SetValue` throws `FieldAccessException`. Also manages the custom zoom range (`_zoomTargetMin`/`_zoomTargetMax`) and exposes helpers used by the Harmony patches.

**`WidescreenResolutionOverride`** — unlocks high-res display modes.
Calls Win32 `EnumDisplaySettings` to enumerate every resolution the monitors support, raises `Main.maxScreenW/H`, bumps `Main._renderTargetMaxSize`, and registers each mode via `Main.RegisterDisplayResolution` (private, called by reflection).

### Harmony patches (`Patches/`)

| Patch class | Target | What it does |
|---|---|---|
| `WidescreenResolutionPatch` | `Main.CacheSupportedDisplaySizes` (postfix) | Triggers resolution unlock and saved-resolution restore at the point Terraria builds its display list |
| `InitTargetsPatch` | `Main.InitTargets` (transpiler) | Replaces every `Math.Min(backBuffer, worldView)` call with `Math.Max` so render targets are sized to the larger of the two axes |
| `SpriteViewMatrixZoomSetterPatch` | `SpriteViewMatrix.set_Zoom` (prefix) | Remaps the vanilla zoom value to the configured expanded range when custom zoom is active |
| `AreaToLightPatch` | `Main.GetAreaToLight` (postfix) | Inflates the lighting rectangle to match the zoomed-out viewport; new lighting engine only |
| `RenderToTargetsSafetyPatch` / `WorldSceneLayerTargetUpdateContentSafetyPatch` | `Main.RenderToTargets`, `WorldSceneLayerTarget.UpdateContent` (prefixes) | Short-circuits rendering when `Main.targetSet` is false to prevent drawing before targets are initialized |
| `TileDrawAreaPatch` | `TileDrawing.GetScreenDrawArea` (postfix) | Intentionally a no-op stub; expanding tile draw area caused performance regressions |

### Config schema (`manifest.json`)

All user-visible config is declared in `manifest.json` under `config_schema`. New config keys must be added there **and** read in `Mod.LoadConfigValues()`. The `desiredResolutionWidth`/`desiredResolutionHeight` keys serve dual purpose: they are both the persisted resolution (restored at startup) and the source for the world-view zoom reference computation.
