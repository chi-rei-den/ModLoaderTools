# ModLoaderTools
Tools for building a tModLoader mod with GitHub Actions.

Usage:
```yaml
name: Mod Build

on: [push]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v1
      - uses: warrenbuckley/Setup-Nuget@v1
      - uses: chi-rei-den/ModLoaderTools@v1
        with:
          command: setup
      - run: nuget restore YourProjec
      - uses: chi-rei-den/ModLoaderTools@v1
        with:
          command: build
          path: PathToYourMod
      - uses: chi-rei-den/ModLoaderTools@v1
        with:
          command: publish
          path: PathToYourMod
        env:
          steamid64: ${{ secrets.YourSteamId64 }}
          passphrase: ${{ secrets.ModBrowserPassphrase }}
      - name: Clean artifact
        run: |
          mkdir .\Artifact\Artifact\
          Copy-Item -Path "$ENV:UserProfile\Documents\My Games\Terraria\ModLoader\Mods\*" -Destination .\Artifact\Artifact
          del .\Artifact\Artifact\enabled.json
      - uses: actions/upload-artifact@master
        with:
          name: Build Artifact
          path: Artifact
```