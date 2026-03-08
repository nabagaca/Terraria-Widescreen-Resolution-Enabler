# Terraria Widescreen Resolution Enabler

`WidescreenTools` is a TerrariaModder plugin for Terraria 1.4.5 that:

- unlocks high and ultrawide display modes in Terraria's resolution list
- relaxes Terraria's forced minimum zoom on wide resolutions
- optionally expands the vanilla zoom slider range with a configurable multiplier
- clamps custom zoom multiplier to the current safe render range to avoid blank/unsupported zoom-out
- persists the selected high-resolution mode across launches

## Runtime Requirements

- Terraria 1.4.5
- TerrariaModder framework

Install by copying the contents of the release zip into:

```text
Terraria/TerrariaModder/mods/widescreen-tools/
```

That folder should contain:

- `WidescreenTools.dll`
- `manifest.json`

## Building

This project compiles against:

- `Terraria.exe` from your local install
- `TerrariaModder.Core` from `ref/TerrariaModder.Core`

```powershell
dotnet build src/WidescreenTools/WidescreenTools.csproj -c Release
```

Build output:

```text
artifacts/bin/WidescreenTools.dll
```

Default build deploys to TerrariaModder mods folder.

Opt out of deploy during build:

```powershell
dotnet build src/WidescreenTools/WidescreenTools.csproj -c Release -p:EnableTerrariaModderDeploy=false
```

## Config Notes

- `zoomRangeMultiplier = 1.0` means "use widened baseline behavior only" (no extra custom range expansion).
- `zoomRangeMultiplier > 1.0` expands slider range around vanilla zoom.
- At runtime, the mod may clamp multiplier down if your current resolution/render-target limits cannot safely support the requested zoom-out.

## Credits
Thank you to Star-F0rce for their [patching tool](https://github.com/Star-F0rce/terraria-145-widescreen) that inspired the zoom patching in this mod 

OpenAI Codex CLI tool with OpenAI Codex 5.3 was also used to help code this mod.
