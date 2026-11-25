using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.EditorAddons;
using osu.Game.Rulesets.Objects;

internal static class CurveBakeProbe
{
	public static void Run()
	{
		foreach (int scenario in Enumerable.Range(0, 6))
		{
			CatchBeatmap map = new() { BeatmapVersion = 14 };
			map.BeatmapInfo.Ruleset = new CatchRuleset().RulesetInfo;
			map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
			JuiceStream source = new() { StartTime = 2000, OriginalX = 256 };
			source.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
			JuiceStreamPath shape = new();
			if (scenario % 2 == 0) { shape.Add(500, 80); shape.Add(1000, -90); }
			else { shape.Add(500, 0); shape.Add(1000, 0); }
			shape.Add(1500, 0);
			shape.ConvertToSliderPath(source.Path, source.LegacyConvertedY, source.Velocity);
			source.Path.ExpectedDistance.Value = 1500 * source.Velocity;
			if (scenario >= 2)
			{
				BananaShower before = new() { StartTime = 0, Duration = 700 };
				before.ApplyDefaults(map.ControlPointInfo, map.Difficulty); map.HitObjects.Add(before);
			}
			map.HitObjects.Add(source);
			source.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
			var targets = source.NestedHitObjects.Cast<CatchHitObject>().Select(h => (Kind: h.GetType(), Time: h.StartTime, X: h.OriginalX)).ToArray();
			CurveBaker.Result result = CurveBaker.Create(map, source);
			if (result.Stream == null) throw new Exception(result.Message);
			map.HitObjects[^1] = result.Stream;
			new CatchBeatmapProcessor(map).PostProcess();
			IBeatmap playable = map;
			if (scenario >= 4)
			{
				// Invoke the official export mutation (including integer coordinate rounding).
				osu.Framework.Platform.NativeStorage storage = new(Path.GetTempPath());
				LegacyBeatmapExporter exporter = new(storage);
				typeof(LegacyBeatmapExporter).GetMethod("MutateBeatmap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(exporter, new object[] { new BeatmapSetInfo(), map });
			}
			if (scenario >= 1)
			{
				using StringWriter writer = new(); new LegacyBeatmapEncoder(map, null, null).Encode(writer);
				using MemoryStream bytes = new(Encoding.UTF8.GetBytes(writer.ToString()));
				using LineBufferedReader reader = new(bytes);
				Beatmap decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
				FlatWorkingBeatmap working = new(decoded);
				playable = working.GetPlayableBeatmap(new CatchRuleset().RulesetInfo);
			}
			new CatchBeatmapProcessor(playable).PostProcess();
			var actual = playable.HitObjects.OfType<JuiceStream>().Single().NestedHitObjects.Cast<CatchHitObject>().ToArray();
			if (actual.Length != targets.Length) throw new Exception("Droplet count changed");
			double maxX = actual.Select((h, i) => Math.Abs(h.EffectiveX - targets[i].X)).Max();
			double maxTime = actual.Select((h, i) => Math.Abs(h.StartTime - targets[i].Time)).Max();
			if (actual.Where((h, i) => h.GetType() != targets[i].Kind).Any()) throw new Exception("Types changed");
			Console.WriteLine($"SCENARIO {scenario}: objects={actual.Length} max-position-error={maxX:F6}px max-time-error={maxTime:F6}ms");
			if (maxX > 1 || maxTime > 0.01) throw new Exception("Alignment changed after export");
			int pathVersion = source.Path.Version.Value;
			source.RepeatCount = 1;
			if (CurveBaker.Create(map, source).Stream != null || source.Path.Version.Value != pathVersion) throw new Exception("Repeated stream should remain unchanged");
		}
		if (HarmonyLib.Harmony.GetPatchInfo(typeof(CatchBeatmapProcessor).GetMethod(nameof(CatchBeatmapProcessor.ApplyPositionOffsets))!) != null)
			throw new Exception("Gameplay position processor must not be patched");
		Console.WriteLine("PASS repeated streams refused without altering their path");
		Console.WriteLine("PASS native gameplay position processor has no runtime patch");
		Console.WriteLine("SUCCESS: 6 round-trip scenarios plus native-engine and refusal checks");
	}
}
