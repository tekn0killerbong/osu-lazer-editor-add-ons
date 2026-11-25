# EditorAddons v1.0

First public release of **osu-editor-add-ons**, by **tekno_killerbong**. EditorTools is now named **EditorAddons**, with internal version **1.0.0**.

## Features

- Mania and Taiko step recording with smooth navigation and a persistent timing grid.
- Note replacement and long-note trimming during recording.
- Ctrl/Shift selection in Mania, Taiko and Catch.
- Freehand Catch juice streams and persistent Stick to curve tracking.
- Playback speed and pitch controls in the editor and Test mode.

## Install or upgrade

Download `osu-editor-add-ons-v1.0-release.zip`. In lazer, select **Open osu! folder**, close the game, and put **`osu.Game.Rulesets.EditorAddons.dll`** in `rulesets`.

**Upgrading from EditorTools:** remove `osu.Game.Rulesets.EditorTools.dll` from `rulesets` before restarting. The two versions must not be installed together. Existing settings and curve guides are reused.

**Target: osu!lazer 2026.916.0 / .NET 10.** Shred is not required. The Source code downloads are for developers.

The public package contains one extension DLL. Original contributions retain the non-commercial licence in `LICENSE.md`. Third-party notices and installation instructions are included.
