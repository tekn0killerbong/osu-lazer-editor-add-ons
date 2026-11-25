using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using System.Runtime.CompilerServices;

namespace osu.Game.Rulesets.EditorAddons;

public static class Grid
{
	private sealed class Memory { public BeatDivisorPresetCollection? Custom; }
	private static readonly ConditionalWeakTable<BindableBeatDivisor, Memory> memories = new();
	public static double Next(ControlPointInfo points, int divisor, double time, int direction = 1)
	{
		const double epsilon = 0.00001;
		double query = time;
		if (direction < 0) query -= epsilon;
		TimingControlPoint timing = points.TimingPointAt(query);
		double spacing = timing.BeatLength / Math.Max(1, divisor);
		double index = (time - timing.Time) / spacing;
		double target;
		if (direction > 0) target = timing.Time + (Math.Floor(index + epsilon / spacing) + 1) * spacing;
		else target = timing.Time + (Math.Ceiling(index - epsilon / spacing) - 1) * spacing;
		if (direction > 0)
		{
			TimingControlPoint? next = points.TimingPoints.FirstOrDefault(p => p.Time > time + epsilon);
			if (next != null) target = Math.Min(target, next.Time);
		}
		return Math.Max(0, target);
	}

	public static void Adjust(BindableBeatDivisor divisor, EditorAction action, bool reverseSpacing = false)
	{
		// Mania/taiko use the requested subdivision convention: 7 decreases the
		// divisor, 8 increases it. Catch retains its existing spacing convention.
		if (reverseSpacing)
		{
			if (action == EditorAction.DecreaseSpacing) { divisor.SelectPrevious(); return; }
			if (action == EditorAction.IncreaseSpacing) { divisor.SelectNext(); return; }
		}
		switch (action)
		{
			// Spacing is the time between lines, the inverse of the divisor.
			case EditorAction.DecreaseSpacing: divisor.SelectNext(); break;
			case EditorAction.IncreaseSpacing: divisor.SelectPrevious(); break;
			case EditorAction.DecreaseType: Cycle(divisor, -1); break;
			case EditorAction.IncreaseType: Cycle(divisor, 1); break;
		}
	}
	private static void Cycle(BindableBeatDivisor divisor, int direction)
	{
		Memory memory = memories.GetOrCreateValue(divisor);
		BeatDivisorPresetCollection current = divisor.ValidDivisors.Value;
		if (current.Type == BeatDivisorType.Custom) memory.Custom = current;
		List<BeatDivisorPresetCollection> modes = new() { BeatDivisorPresetCollection.COMMON, BeatDivisorPresetCollection.TRIPLETS };
		if (memory.Custom != null) modes.Add(memory.Custom);
		int index = modes.FindIndex(m => m.Type == current.Type);
		BeatDivisorPresetCollection next = modes[(index + direction + modes.Count) % modes.Count];
		divisor.ValidDivisors.Value = next;
		if (next.Type == BeatDivisorType.Common) divisor.Value = 4;
		if (next.Type == BeatDivisorType.Triplets) divisor.Value = 6;
	}
}
