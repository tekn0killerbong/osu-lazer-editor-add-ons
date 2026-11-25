using osu.Game.Rulesets;
using osu.Game.Rulesets.EditorAddons;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Osu;
using osu.Game.Beatmaps.ControlPoints;
using osu.Framework.Input.Bindings;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Settings.Sections.Input;
using HarmonyLib;


internal static class UnitProbe
{
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	public static void Run(string[] args)
	{
		int checks = 0;
		void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
		if (args.Contains("--embedded")) Check(typeof(Harmony).Assembly.Location.Length == 0, "Harmony loaded from embedded resource without adjacent DLL");
		Check(!Harmony.GetAllPatchedMethods().Any(method => method.DeclaringType?.Namespace?.StartsWith("osu.Game.Rulesets.Catch.Skinning", StringComparison.Ordinal) == true
			&& Harmony.GetPatchInfo(method)?.Owners.Contains("tekno_killerbong.editor-tools.v1") == true), "EditorAddons owns no Catch skin or hyperdash patches");
		foreach (Ruleset ruleset in new Ruleset[] { new ManiaRuleset(), new TaikoRuleset(), new CatchRuleset() })
		{
			Check(ruleset.CreateHitObjectComposer()!.GetType().Assembly == typeof(EditorAddonsRuleset).Assembly, ruleset.ShortName + " native composer routed to independent extension");
			var keys = ruleset.GetDefaultKeyBindings(Ruleset.EDITOR_VARIANT).ToArray();
			int expected = 12;
			if (ruleset is CatchRuleset) expected = 8;
			Check(keys.Count(k => Convert.ToInt32(k.Action) >= 186000) == expected, ruleset.ShortName + " editor bindings and optional empty slots");
			Check(ruleset.GetDefaultKeyBindings().All(k => Convert.ToInt32(k.Action) < 186000), ruleset.ShortName + " gameplay bindings unchanged");
			using RulesetEditorBindingsSubsection section = new(ruleset.RulesetInfo);
			foreach (var group in keys.Where(k => Bindings.IsEditorAction(Convert.ToInt32(k.Action))).GroupBy(k => k.Action))
			{
				using KeyBindingRow row = (KeyBindingRow)AccessTools.Method(typeof(RulesetEditorBindingsSubsection), "CreateKeyBindingRow").Invoke(section, new object[] { group.Key, group })!;
				Check(row.Action is EditorAction && row.Defaults.Count() == 2, ruleset.ShortName + " editor settings row " + row.Action);
			}
		}
		Check(new OsuRuleset().CreateHitObjectComposer()!.GetType().Assembly != typeof(EditorAddonsRuleset).Assembly, "standard editor unchanged");
		using (AssemblyRulesetStore store = new()) Check(store.AvailableRulesets.All(r => r.ShortName != EditorAddonsRuleset.Id), "carrier hidden from available rulesets");
		ControlPointInfo timing = new();
		timing.Add(0, new TimingControlPoint { BeatLength = 500 });
		timing.Add(1125, new TimingControlPoint { BeatLength = 400 });
		Check(Grid.Next(timing, 4, 1000) == 1125, "grid enters BPM boundary");
		Check(Grid.Next(timing, 4, 1125) == 1225, "grid follows new BPM");
		Check(Grid.Next(timing, 4, 1125, -1) == 1000, "grid backward across BPM boundary");
		Check(Grid.Next(timing, 4, 0, -1) == 0, "grid cannot seek negative");
		List<RecorderModel.Entry> entries = new();
		double cursor = 0;
		RecorderModel model = new(1000, (t, d) => t + 125 * d, entries.Add, _ => {}, t => cursor = t);
		model.Press(0); model.Press(1); model.Release(0);
		Check(model.Position == 1000, "chord waits for final short-note release");
		model.Release(1); Check(cursor == 1125, "chord advances exactly one step");
		model.Press(0); model.Move(1);
		Check(entries.Last().End == 1250 && entries.Last().Extended, "empty step promotes held note to LN");
		model.Press(1); model.Release(1);
		Check(entries[^2].End == 1375, "short-note release extends held LN");
		model.Move(-1); Check(entries[^2].End == 1250, "backward navigation trims active LN");
		model.Release(0); Check(model.Position == 1250, "LN release does not add another step");
		int count = entries.Count; model.Press(2); model.Press(2); Check(entries.Count == count + 1, "held action does not create duplicate head");
		model.Clear(); Check(model.HeldCount == 0, "cancel clears held inputs");
		FreehandStroke stroke = new(0, 0, t => Math.Floor(t / 100 + 0.00001) * 100 + 100);
		stroke.Move(350, 350);
		Check(stroke.Points.Select(p => p.Time).SequenceEqual(new double[] { 0, 100, 200, 300 }), "fast mouse movement samples every crossed grid line");
		Check(stroke.Points.Last().X == 300, "interpolated positions preserve drawing");
		Check(stroke.Finish(400, 350).Last().Time == 400, "stroke includes snapped final fruit");
		stroke.Move(150, 100); Check(stroke.Points.All(p => p.Time <= 150), "backtracking removes future vertices");
		BindableBeatDivisor divisor = new(4);
		Grid.Adjust(divisor, EditorAction.DecreaseSpacing); Check(divisor.Value == 8, "decrease spacing makes finer grid");
		Grid.Adjust(divisor, EditorAction.IncreaseSpacing); Check(divisor.Value == 4, "increase spacing makes coarser grid");
		Grid.Adjust(divisor, EditorAction.DecreaseSpacing, true); Check(divisor.Value == 2, "mania/taiko decrease spacing uses reversed requested direction");
		Grid.Adjust(divisor, EditorAction.IncreaseSpacing, true); Check(divisor.Value == 4, "mania/taiko increase spacing uses reversed requested direction");
		Grid.Adjust(divisor, EditorAction.IncreaseType); Check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets && divisor.Value == 6, "grid type advances to triplets at 1/6");
		Grid.Adjust(divisor, EditorAction.IncreaseType); Check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Common && divisor.Value == 4, "grid type cycles to common at 1/4");
		divisor.ValidDivisors.Value = BeatDivisorPresetCollection.Custom(5);
		Grid.Adjust(divisor, EditorAction.IncreaseType); Grid.Adjust(divisor, EditorAction.DecreaseType);
		Check(divisor.ValidDivisors.Value.Type == BeatDivisorType.Custom && divisor.ValidDivisors.Value.Presets.Contains(5), "grid type retains custom division");
		RealmKeyBinding cleared = new(GlobalAction.EditorToggleWhistleSound, new KeyCombination(InputKey.None));
		RealmKeyBinding customised = new(GlobalAction.EditorToggleFinishSound, new KeyCombination(InputKey.X));
		BindingMigration.Restore(new[] { cleared, customised });
		Check(cleared.KeyCombination.Equals(new KeyCombination(InputKey.W)), "native hitsound shortcut restored from cleared slot");
		Check(customised.KeyCombination.Equals(new KeyCombination(InputKey.X)), "native hitsound migration preserves custom shortcut");
		Console.WriteLine($"SUCCESS: {checks} checks");
	}
}
