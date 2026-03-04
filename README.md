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

## Credits
Thank you to Star-F0rce for their [patching tool](https://github.com/Star-F0rce/terraria-145-widescreen) that inspired the zoom patching in this mod 

OpenAI Codex CLI tool with OpenAI Codex 5.3 was also used to help code this mod.