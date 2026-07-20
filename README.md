# SleepStrap 6.7

SleepStrap is a custom, purple-themed Fishstrap/Bloxstrap fork for Windows 10 and newer.

## Custom features

- Purple SleepStrap branding and interface
- Separate **Skybox** and **Textures** pages
- Independent **Clear / Blurry** texture quality with aggressive mip/compositor fallbacks and FastFlag backup/restore
- A **Rivals** page with reversible horizontal display stretching
- A **Fonts** page with Montserrat pinned first and installed Roblox fonts for all GUI family mappings
- An **Other** page with the SleepStrap Discord, credits, version information, and a detailed Terms of Use and risk notice
- Scrollable three-column gallery with 25 included skybox previews
- Conversion of every selected face into Roblox-native 1024×1024 mipmapped BC1/DDS texture files
- A **None** card that removes the selected sky and restores the previous sky files
- Reversible Basic/Dark material texture switching
- Backups of pre-existing texture mods before replacement
- Custom skyboxes remain layered correctly while switching texture packs
- Upstream self-updating is disabled so this custom build is not overwritten

## Visual-mod notice

Skybox and material options replace local files under Roblox's installed texture folders when Roblox is launched through SleepStrap. They do not inject code or modify the Roblox executable, but they are unofficial client asset modifications. No third-party launcher can promise zero account-enforcement risk.

SleepStrap displays this warning and requires confirmation before applying visual mods for the first time.

Read the full [Terms of Use and Risk Notice](TERMS.md) before using SleepStrap.

## Building

```powershell
dotnet build SleepStrap.sln -c Release
dotnet publish Bloxstrap /p:PublishProfile=Publish-x64
```

## Credits and licenses

SleepStrap is based on Fishstrap and Bloxstrap. Their original MIT license notices are retained in `LICENSE` and `LICENSE.Bloxstrap`.
