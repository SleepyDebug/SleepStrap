# SleepStrap 6.7

![SleepStrap icon](SleepStrap/SleepStrap.png)

SleepStrap is a purple Windows launcher for Roblox with skyboxes, texture presets, visual modes, fonts, and Rivals display tools.

[Download SleepStrap 6.7](https://github.com/SleepyDebug/SleepStrap/releases/tag/sleepstrap-6.7) · [Discord](https://discord.gg/W3CMjx8C7s) · [Report an issue](https://github.com/SleepyDebug/SleepStrap/issues)

## Repository layout

| Path | Purpose |
| --- | --- |
| `SleepStrap/` | SleepStrap application source, resources, and publish profile |
| `vendor/Wpf.Ui/` | Vendored WPF UI dependency |
| `scripts/` | Repository maintenance utilities |
| `docs/` | Terms, risk notice, and upstream license notices |
| `.github/` | CI and issue templates |

## Features

- Purple SleepStrap interface, animated page transitions, and Sleep icon branding
- Scrollable skybox gallery with 25 included presets
- Reversible basic and dark texture packs
- Clear, blurry, and polished-metal RTX-style visual modes
- Montserrat-first selector for Roblox GUI and direct in-game text fonts
- Rivals horizontal display stretching, FPS presets, and in-game FPS counter
- Automatic GitHub release checks with a confirmation prompt before updating
- Other page with Discord, version, attribution, and terms

## Build

```powershell
dotnet restore SleepStrap.sln
dotnet build SleepStrap.sln -c Release --no-restore
dotnet publish .\SleepStrap\SleepStrap.csproj -c Release --no-restore /p:PublishProfile=Publish-x64
```

The Windows x64 publish profile produces a self-contained single-file build, so users do not need to install the .NET Desktop Runtime separately.

## Safety notice

SleepStrap is unofficial software. Its visual options replace local Roblox assets and settings; they do not inject code into the Roblox executable. No third-party launcher can guarantee protection from account enforcement. Read the full [Terms of Use and Risk Notice](docs/TERMS.md) before use.

## Credits and licenses

SleepStrap is derived from the open-source Fishstrap and Bloxstrap projects. Their names and copyrights belong to their respective owners. Required MIT notices are retained in [LICENSE](LICENSE) and [docs/licenses](docs/licenses).
