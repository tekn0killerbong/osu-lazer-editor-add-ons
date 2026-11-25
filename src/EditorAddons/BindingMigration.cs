using osu.Framework.Input.Bindings;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.EditorAddons;

internal static class BindingMigration
{
	private const string marker = "editor-tools-sound-bindings-v1.txt";
	internal static readonly GlobalAction[] Actions =
	{
		GlobalAction.EditorToggleNewCombo, GlobalAction.EditorToggleWhistleSound,
		GlobalAction.EditorToggleFinishSound, GlobalAction.EditorToggleClapSound,
		GlobalAction.EditorToggleNormalAutoBank, GlobalAction.EditorToggleNormalNormalBank,
		GlobalAction.EditorToggleNormalSoftBank, GlobalAction.EditorToggleNormalDrumBank,
		GlobalAction.EditorToggleAdditionAutoBank, GlobalAction.EditorToggleAdditionNormalBank,
		GlobalAction.EditorToggleAdditionSoftBank, GlobalAction.EditorToggleAdditionDrumBank
	};
	internal static void OnEditorLoaded(Editor __instance)
	{
		Storage storage = __instance.Dependencies.Get<GameHost>().Storage;
		if (storage.Exists(marker)) return;
		try
		{
			__instance.Dependencies.Get<RealmAccess>().Write(r => Restore(r.All<RealmKeyBinding>().Where(b => b.RulesetName == null && b.Variant == null).ToArray()));
			using StreamWriter writer = new(storage.GetStream(marker, FileAccess.Write, FileMode.Create));
			writer.WriteLine("Restored previously unbound native editor sound shortcuts once. Later user changes are preserved.");
		}
		catch (Exception error) { Logger.Error(error, "EditorAddons could not restore native editor sound shortcuts."); }
	}
	internal static void Restore(IEnumerable<RealmKeyBinding> bindings)
	{
		foreach (KeyBinding original in GlobalActionContainer.GetDefaultBindingsFor(GlobalActionCategory.Editor).Where(b => Actions.Contains((GlobalAction)b.Action)))
		{
			RealmKeyBinding[] rows = bindings.Where(b => b.ActionInt == Convert.ToInt32(original.Action)).ToArray();
			// Never overwrite a customised combination. Only restore an action
			// whose slots were all cleared by a former Shred migration.
			if (rows.Length > 0 && rows.All(b => b.KeyCombination.Equals(new KeyCombination(InputKey.None))))
				rows[0].KeyCombination = original.KeyCombination;
		}
	}
}
