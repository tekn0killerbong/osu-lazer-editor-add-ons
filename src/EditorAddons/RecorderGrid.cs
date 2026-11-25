using HarmonyLib;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.EditorAddons;

internal interface IRecorderComposer
{
	RecorderController? Recorder { get; }
	BeatSnapGrid? RecorderGrid { get; }
}

internal static class RecorderGrid
{
	internal static void Install(Harmony harmony)
	{
		foreach (Type type in new[] { typeof(ScrollingHitObjectComposer<ManiaHitObject, ManiaAction>), typeof(ScrollingHitObjectComposer<TaikoHitObject, TaikoAction>) })
			harmony.Patch(AccessTools.Method(type, "updateBeatSnapGrid"), prefix: new HarmonyMethod(typeof(RecorderGrid), nameof(Update)));
	}
	private static bool Update(object __instance)
	{
		if (__instance is not IRecorderComposer composer || composer.Recorder?.Enabled.Value != true || composer.RecorderGrid == null) return true;
		composer.RecorderGrid.SelectionTimeRange = composer.Recorder.GridRange;
		return false;
	}
}
