# tekno's editor add ons for osu!lazer

Optional editor tools for osu!lazer, by **tekno_killerbong**, Creator of [shred](https://github.com/tekn0killerbong/shred-osu-lazer). GitHub release tag: **v1.0**.

This repository builds one extension: **`osu.Game.Rulesets.EditorAddons.dll`**. Shred is not required. The extension does not add a playable ruleset.

## Install

Download the .dll file from [latest release](https://github.com/tekn0killerbong/shred-osu-lazer/releases/latest).
In lazer, use 'Open osu! folder' the settings.
Place the .dll file into the `rulesets` folder of the osu!lazer folder.
Restart lazer.

## Features

- Step recorder in mania and taiko, using configured gameplay keys to create notes one grid spacing at a time with automatic scrolling.
- Ctrl/Shift selection gestures in Mania, Taiko and Catch.
- Freehand Catch juice streams and persistent **Stick to curve** tracking.
- Playback speed: 1–200% and pitch control in editor and Test mode.


##(ChatGPT notes)

Built against **osu!lazer 2026.916.0 / .NET 10**. Compatibility with other versions must be checked. The release ZIP includes the DLL, installation instructions and licence notices. GitHub's automatic **Source code** downloads are for developers.

Existing preferences and curve guides are retained through their historical `editor-tools*` filenames. Custom key bindings retain their numeric identifiers.

### Build and check

Install the **.NET 10 SDK**. Open `EditorAddons.sln`, or run from the repository root:

```powershell
.\scripts\build.ps1
```

On Linux/macOS:

```sh
bash scripts/build.sh
```

The scripts restore locked dependencies, compile the solution and recreate `dist/` with the single extension DLL and its notices. Game dependencies are supplied by lazer; Harmony is embedded in the extension.

After building:

```sh
dotnet run --project tests/EditorAddons.Tests -c Release --no-build
dotnet run --project tests/EditorAddons.Tests -c Release --no-build -- --curve
python scripts/verify-discovery.py
```

Python 3 is required for the discovery check. Optional headless editor and playback probes use `--editor` and `--playback`.

GitHub Actions runs builds and checks on Windows and Linux, then uploads distribution artifacts. It does not publish a release automatically. See [validation](docs/VALIDATION.md) for checks executed locally and their limitations.

## Licence

Original contributions use the existing [non-commercial licence](LICENSE.md), copyright 2026 **tekno_killerbong**. Its historical Editor Tools title and identifier are retained. Third-party components keep their respective licences: [notices](THIRD_PARTY_NOTICES.md) and [licence texts](LICENSES/).
