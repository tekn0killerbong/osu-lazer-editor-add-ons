# EditorAddons v1.0

Optional editor tools for osu!lazer, by tekno_killerbong. Internal version: 1.0.0. Repository: osu-editor-add-ons.

## Install

1. In lazer, use **Open osu! folder**, then close the game completely.
2. Open the `rulesets` subfolder; create it if needed.
3. Copy `osu.Game.Rulesets.EditorAddons.dll` into it.
4. If upgrading from EditorTools, remove `osu.Game.Rulesets.EditorTools.dll` from `rulesets`. Keep backups outside that folder; do not leave an older renamed copy ending in `.dll` there.
5. Restart lazer.

Existing preferences and curve guides keep their `editor-tools*` filenames and are reused. The extension embeds Harmony. Shred is not required.

Target: **osu!lazer 2026.916.0 / .NET 10**. Compatibility with other versions needs verification.

This release contains one extension DLL, its documentation and licences. See `docs/USER-GUIDE.fr.md`, `LICENSE.md`, `THIRD_PARTY_NOTICES.md` and `LICENSES/`.

To uninstall, close lazer and remove `osu.Game.Rulesets.EditorAddons.dll` from `rulesets`.
