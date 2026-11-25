// Portions of the sampling sequence follow osu! (ppy Pty Ltd), MIT.
// See LICENSES/osu-MIT.txt. Original additions: EditorAddons, LICENSE.md.
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.EditorAddons;

/// <summary>Writes inverse tiny-droplet offsets into ordinary slider geometry.</summary>
internal static class CurveBaker
{
	internal sealed record Result(JuiceStream? Stream, string Message);
	internal static Result Create(IBeatmap map, JuiceStream source, JuiceStream? sequenceTarget = null, bool capturePositions = false)
	{
		if (source.RepeatCount != 0)
			return new(null, "Repeated streams share one path but have different offsets on each pass. This conversion requires a stream without repeats.");
		if (!source.NestedHitObjects.OfType<TinyDroplet>().Any())
			return new(null, "This stream has no scattered tiny droplets to align.");
		if (map.BeatmapVersion < 8 || Math.Abs(source.TickDistanceMultiplier - 1) > 0.000001)
			return new(null, "This legacy tick layout cannot be preserved by this conversion.");
		List<double> progress = SampleProgress(source);
		int[] offsets = Offsets(map, sequenceTarget ?? source);
		if (capturePositions) offsets = new int[source.NestedHitObjects.Count];
		CatchHitObject[] original = source.NestedHitObjects.Cast<CatchHitObject>().ToArray();
		if (progress.Count != original.Length || offsets.Length != original.Length)
			return new(null, "The native droplet layout does not match the supported format.");
		JuiceStreamPath path = new();
		float headX = MathF.Round(source.OriginalX);
		if (capturePositions) headX = source.OriginalX;
		float Target(int index)
		{
			if (capturePositions) return original[index].EffectiveX;
			return Math.Clamp(original[index].OriginalX, 0, 512);
		}
		for (int i = 1; i < original.Length; i++)
		{
			float target = Target(i);
			path.Add(progress[i] * source.SpanDuration, target - offsets[i] - headX);
		}
		double factor = source.Velocity / source.SliderVelocityMultiplier;
		double required = path.ComputeRequiredVelocity() / factor;
		if (!double.IsFinite(required) || required > 10)
			return new(null, "The required compensation exceeds the native slider-velocity limit. The stream has not been changed.");
		JuiceStream candidate = new()
		{
			StartTime = source.StartTime, OriginalX = headX,
			LegacyConvertedY = MathF.Round(source.LegacyConvertedY),
			NewCombo = source.NewCombo, ComboOffset = source.ComboOffset,
			Samples = source.Samples.Select(s => s.With()).ToList(),
			NodeSamples = source.NodeSamples.Select(n => (IList<osu.Game.Audio.HitSampleInfo>)n.Select(s => s.With()).ToList()).ToList(),
			SliderVelocityMultiplier = Math.Max(source.SliderVelocityMultiplier, Math.Ceiling(required))
		};
		candidate.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
		path.ConvertToSliderPath(candidate.Path, candidate.LegacyConvertedY, candidate.Velocity);
		if (!capturePositions) Quantize(candidate);
		candidate.Path.ExpectedDistance.Value = source.SpanDuration * candidate.Velocity;
		candidate.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
		CatchHitObject[] baked = candidate.NestedHitObjects.Cast<CatchHitObject>().ToArray();
		if (baked.Length != original.Length)
			return new(null, "This conversion would change the droplet count. The stream has not been changed.");
		for (int i = 0; i < baked.Length; i++)
		{
			float effective = Math.Clamp(baked[i].OriginalX + offsets[i], 0, 512);
			if (baked[i].GetType() != original[i].GetType() || Math.Abs(baked[i].StartTime - original[i].StartTime) > 0.01
				|| Math.Abs(effective - Target(i)) > 1.0)
				return new(null, "This stream cannot be aligned without changing its native notes or timing. The stream has not been changed.");
		}
		return new(candidate, "Droplet positions aligned. Earlier edits are compensated automatically in this editor. The original curve is retained locally.");
	}

	private static void Quantize(JuiceStream stream)
	{
		// Legacy export rounds control points. Bake that rounding now, correcting
		// cumulative arc length at every vertex instead of letting errors add up.
		Vector2 previous = Vector2.Zero;
		Vector2 roundedPrevious = Vector2.Zero;
		double intendedLength = 0;
		double roundedLength = 0;
		foreach (PathControlPoint point in stream.Path.ControlPoints.Skip(1))
		{
			Vector2 original = point.Position;
			intendedLength += Vector2.Distance(previous, original);
			float x = MathF.Round(original.X);
			double dx = x - roundedPrevious.X;
			double distance = Math.Max(0, intendedLength - roundedLength);
			double dy = Math.Sqrt(Math.Max(0, distance * distance - dx * dx));
			if (original.Y < previous.Y) dy = -dy;
			double wantedY = roundedPrevious.Y + dy;
			float low = Math.Clamp((float)Math.Floor(wantedY), -stream.LegacyConvertedY, 384 - stream.LegacyConvertedY);
			float high = Math.Clamp((float)Math.Ceiling(wantedY), -stream.LegacyConvertedY, 384 - stream.LegacyConvertedY);
			Vector2 rounded = new(x, low);
			Vector2 alternate = new(x, high);
			if (Math.Abs(Vector2.Distance(roundedPrevious, alternate) - distance) < Math.Abs(Vector2.Distance(roundedPrevious, rounded) - distance)) rounded = alternate;
			roundedLength += Vector2.Distance(roundedPrevious, rounded);
			point.Position = rounded;
			previous = original; roundedPrevious = rounded;
		}
	}

	// Ask the unmodified native processor for the random sequence on disposable
	// stand-ins. Centre tiny droplets so border clamping cannot hide the raw offset.
	private static int[] Offsets(IBeatmap map, JuiceStream target)
	{
		CatchBeatmap probe = new() { Difficulty = map.Difficulty.Clone() };
		OffsetStream? result = null;
		foreach (HitObject obj in map.HitObjects)
		{
			if (obj is BananaShower shower) probe.HitObjects.Add(new OffsetShower(shower));
			else if (obj is JuiceStream stream)
			{
				OffsetStream copy = new(stream); probe.HitObjects.Add(copy);
				if (ReferenceEquals(stream, target)) { result = copy; break; }
			}
			else if (obj is CatchHitObject fruit) probe.HitObjects.Add(new Fruit { StartTime = fruit.StartTime, OriginalX = fruit.OriginalX });
		}
		if (result == null) return Array.Empty<int>();
		new CatchBeatmapProcessor(probe).ApplyPositionOffsets(probe);
		return result.NestedHitObjects.Cast<CatchHitObject>().Select(h => (int)h.XOffset).ToArray();
	}
	private sealed class OffsetStream : JuiceStream
	{
		public OffsetStream(JuiceStream stream)
		{
			StartTime = stream.StartTime; OriginalX = stream.OriginalX; Path = stream.Path;
			foreach (CatchHitObject item in stream.NestedHitObjects.Cast<CatchHitObject>())
			{
				CatchHitObject copy = new Fruit();
				if (item is TinyDroplet) copy = new TinyDroplet();
				else if (item is Droplet) copy = new Droplet();
				copy.StartTime = item.StartTime; copy.OriginalX = 256; AddNested(copy);
			}
		}
	}
	private sealed class OffsetShower : BananaShower
	{
		public OffsetShower(BananaShower shower)
		{
			StartTime = shower.StartTime; Duration = shower.Duration;
			foreach (Banana item in shower.NestedHitObjects.OfType<Banana>())
				AddNested(new Banana { StartTime = item.StartTime });
		}
	}

	private static List<double> SampleProgress(JuiceStream stream)
	{
		List<double> positions = new();
		SliderEventDescriptor? previous = null;
		foreach (SliderEventDescriptor item in SliderEventGenerator.Generate(stream.StartTime, stream.SpanDuration, stream.Velocity, stream.TickDistance, stream.Path.Distance, 1))
		{
			if (previous is SliderEventDescriptor last)
			{
				double span = (int)item.Time - (int)last.Time;
				if (span > 80)
				{
					double spacing = span;
					while (spacing > 100) spacing /= 2;
					for (double t = spacing; t < span; t += spacing)
						positions.Add(last.PathProgress + t / span * (item.PathProgress - last.PathProgress));
				}
			}
			previous = item;
			if (item.Type != SliderEventType.LegacyLastTick) positions.Add(item.PathProgress);
		}
		return positions;
	}
}
