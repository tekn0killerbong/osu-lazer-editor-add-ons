using System.Reflection;
using HarmonyLib;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.EditorAddons;

internal static class Integration
{
	private const string patchId = "tekno_killerbong.editor-tools.v1";
	public static bool Installed { get; private set; }
	public static void Install()
	{
		if (Installed) return;
		Harmony harmony = new(patchId);
		try
		{
			foreach (Type type in new[] { typeof(ManiaRuleset), typeof(TaikoRuleset), typeof(CatchRuleset) })
			{
				Patch(type, nameof(Ruleset.CreateHitObjectComposer), nameof(CreateComposer), true);
				Patch(type, nameof(Ruleset.GetDefaultKeyBindings), nameof(AddBindings), false);
			}
			Patch(typeof(RulesetEditorBindingsSubsection), "CreateKeyBindingRow", nameof(CreateBindingRow), true);
			Patch(typeof(RealmKeyBinding), nameof(RealmKeyBinding.GetAction), nameof(GetAction), true);
			harmony.Patch(AccessTools.Method(typeof(osu.Game.Screens.Edit.Editor), "LoadComplete"), postfix: new HarmonyMethod(typeof(TestPlayback), nameof(TestPlayback.OnEditorLoaded)));
			CatchMenus.Install(harmony);
			RecorderGrid.Install(harmony);
			harmony.Patch(AccessTools.Method(typeof(osu.Game.Screens.Edit.Editor), "LoadComplete"), postfix: new HarmonyMethod(typeof(BindingMigration), nameof(BindingMigration.OnEditorLoaded)));
			// Hide only our carrier. No legacy or third-party ruleset is replaced in
			// the database, and map identities are never changed.
			Patch(typeof(RealmRulesetStore), "get_AvailableRulesets", nameof(HideCarrier), false);
			Patch(typeof(AssemblyRulesetStore), "get_AvailableRulesets", nameof(HideCarrier), false);
			Installed = true;
			Logger.Log("EditorAddons 1.0.0 installed: mania, taiko, catch. Shred is not required.");
		}
		catch
		{
			harmony.UnpatchAll(patchId);
			throw;
		}
		void Patch(Type type, string method, string patch, bool prefix)
		{
			MethodInfo target = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
			HarmonyMethod replacement = new(typeof(Integration), patch);
			if (prefix) harmony.Patch(target, prefix: replacement);
			else harmony.Patch(target, postfix: replacement);
		}
	}

	private static bool CreateComposer(Ruleset __instance, ref HitObjectComposer __result)
	{
		switch (__instance)
		{
			case ManiaRuleset: __result = new RecorderManiaComposer(__instance); return false;
			case TaikoRuleset: __result = new RecorderTaikoComposer(__instance); return false;
			case CatchRuleset: __result = new EnhancedCatchComposer(__instance); return false;
			default: return true;
		}
	}
	private static void AddBindings(Ruleset __instance, int variant, ref IEnumerable<KeyBinding> __result)
	{
		if (variant != Ruleset.EDITOR_VARIANT) return;
		__result = Bindings.Defaults(__instance).Concat(__result).ToArray();
	}
	private static void HideCarrier(ref IEnumerable<RulesetInfo> __result)
	{
		if (Installed) __result = __result.Where(r => r.ShortName != EditorAddonsRuleset.Id);
	}
	private static bool CreateBindingRow(object action, IEnumerable<KeyBinding> defaults, ref KeyBindingRow __result)
	{
		int id = Convert.ToInt32(action);
		if (!Bindings.IsEditorAction(id)) return true;
		__result = new KeyBindingRow((EditorAction)id) { AllowMainMouseButtons = true, Defaults = defaults.Select(d => d.KeyCombination) };
		return false;
	}
	private static bool GetAction(RealmKeyBinding __instance, ref object __result)
	{
		if (__instance.Variant != Ruleset.EDITOR_VARIANT || !Bindings.IsEditorAction(__instance.ActionInt)) return true;
		__result = (EditorAction)__instance.ActionInt;
		return false;
	}
}
