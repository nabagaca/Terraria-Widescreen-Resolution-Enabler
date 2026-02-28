# Terraria Widescreen Resolution Enabler

`WidescreenTools` is a TerrariaModder plugin for Terraria 1.4.5 that:

- unlocks high and ultrawide display modes in Terraria's resolution list
- relaxes Terraria's forced minimum zoom on wide resolutions
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

This repo uses small handwritten reference assemblies for `Terraria` and `TerrariaModder.Core` so CI can build without redistributing game binaries.

```powershell
dotnet build src/WidescreenTools/WidescreenTools.csproj -c Release
```

Build output:

```text
artifacts/bin/WidescreenTools.dll
```

## Release Packaging

The GitHub Actions workflow:

- builds on Windows
- packages `WidescreenTools.dll` and `manifest.json`
- publishes a GitHub release for tags like `v0.1.0`

## Notes

- No Terraria binaries are included in this repository.
- The compiled mod runs against the real `Terraria` and `TerrariaModder.Core` assemblies provided by the game and loader at runtime.

