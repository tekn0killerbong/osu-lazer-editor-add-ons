using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.EditorAddons;

/// <summary>
/// Editor-only recipes. The playable/exported map always contains normal geometry.
/// Recipes are indexed by baked geometry, so native undo/redo selects the right recipe
/// without depending on object identity. Original geometry is not an active recipe.
/// </summary>
internal sealed class CurveTracking
{
	internal sealed record Point(float X, float Y, PathType? Type);
	internal sealed class Guide
	{
		public float X { get; set; }
		public float Y { get; set; }
		public double Velocity { get; set; }
		public double Distance { get; set; }
		public List<Point> Points { get; set; } = new();
		public static Guide Capture(JuiceStream stream) => new()
		{
			X = stream.OriginalX, Y = stream.LegacyConvertedY, Velocity = stream.SliderVelocityMultiplier,
			Distance = stream.Path.ExpectedDistance.Value ?? stream.Path.Distance,
			Points = stream.Path.ControlPoints.Select(p => new Point(p.Position.X, p.Position.Y, p.Type)).ToList()
		};
		public JuiceStream Restore(IBeatmap map, JuiceStream current)
		{
			JuiceStream source = new()
			{
				StartTime = current.StartTime, OriginalX = X, LegacyConvertedY = Y,
				SliderVelocityMultiplier = Velocity, NewCombo = current.NewCombo, ComboOffset = current.ComboOffset,
				Samples = current.Samples.Select(s => s.With()).ToList(),
				NodeSamples = current.NodeSamples.Select(n => (IList<osu.Game.Audio.HitSampleInfo>)n.Select(s => s.With()).ToList()).ToList(),
				Path = new SliderPath(Points.Select(p => new PathControlPoint(new Vector2(p.X, p.Y), p.Type)).ToArray(), Distance)
			};
			source.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
			return source;
		}
	}

	private static readonly ConditionalWeakTable<EditorBeatmap, CurveTracking> books = new();
	private readonly EditorBeatmap map;
	private readonly Storage storage;
	private readonly Action<string> notify;
	private readonly string filename;
	private Dictionary<string, Guide> recipes = new();
	private bool reconciling;
	private bool dirty;

	private CurveTracking(EditorBeatmap map, Storage storage, Action<string> notify)
	{
		this.map = map; this.storage = storage; this.notify = notify;
		filename = $"editor-tools-curves-{map.BeatmapInfo.ID:N}.json";
		try
		{
			if (storage.Exists(filename))
			{
				using Stream input = storage.GetStream(filename);
				recipes = JsonSerializer.Deserialize<Dictionary<string, Guide>>(input) ?? new();
			}
		}
		catch (Exception error) { Logger.Error(error, "EditorAddons could not restore curve guides."); notify("The saved curve guides could not be read. Automatic alignment is unavailable for these streams."); }
		map.BeatmapReprocessed += Reconcile;
	}

	internal static CurveTracking Attach(EditorBeatmap map, Storage storage, Action<string> notify)
	{
		CurveTracking book = books.GetValue(map, key => new CurveTracking(key, storage, notify));
		book.Reconcile();
		return book;
	}
	internal static CurveTracking? For(EditorBeatmap map)
	{
		books.TryGetValue(map, out CurveTracking? book);
		return book;
	}
	internal bool IsTracked(JuiceStream stream) => recipes.ContainsKey(Signature(stream));

	internal string Bake(JuiceStream stream)
	{
		if (IsTracked(stream)) { Reconcile(); return "This stream is already aligned and is updated automatically. Restore original curve before editing its shape."; }
		return BakeFrom(stream, stream);
	}
	internal string TrackCurrentPositions(JuiceStream stream)
	{
		if (IsTracked(stream)) return "This stream already has an automatic alignment guide.";
		// v0.2 did not retain the original path. Explicitly adopt its CURRENT
		// playable positions; never guess that an arbitrary path was baked.
		CurveBaker.Result guide = CurveBaker.Create(map, stream, capturePositions: true);
		if (guide.Stream == null) return guide.Message;
		return BakeFrom(stream, guide.Stream);
	}
	private string BakeFrom(JuiceStream stream, JuiceStream original)
	{
		Guide guide = Guide.Capture(original);
		CurveBaker.Result result = CurveBaker.Create(map, original, stream);
		if (result.Stream == null) return result.Message;
		Remember(result.Stream, guide);
		// Persist the guide before changing the map, so a storage error cannot leave
		// an apparently managed stream with no recoverable original geometry.
		if (!Save()) { recipes.Remove(Signature(result.Stream)); return "The original curve could not be saved. The stream has not been changed."; }
		map.BeginChange();
		try { Apply(stream, result.Stream); }
		finally { map.EndChange(); }
		return result.Message;
	}
	internal string RestoreOriginal(JuiceStream stream)
	{
		if (!recipes.TryGetValue(Signature(stream), out Guide? guide)) return "No saved original curve is available for this stream.";
		map.BeginChange();
		try { Apply(stream, guide.Restore(map, stream)); }
		finally { map.EndChange(); }
		return "Original curve restored. Edit it, then use Stick to curve again when ready.";
	}

	private void Apply(JuiceStream stream, JuiceStream candidate)
	{
		stream.SliderVelocityMultiplier = candidate.SliderVelocityMultiplier;
		stream.OriginalX = candidate.OriginalX;
		stream.LegacyConvertedY = candidate.LegacyConvertedY;
		stream.Path = candidate.Path;
		stream.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
		map.Update(stream);
	}
	private void Remember(JuiceStream baked, Guide guide)
	{
		string key = Signature(baked);
		if (recipes.ContainsKey(key)) return;
		recipes.Add(key, guide); dirty = true;
	}
	internal void Reconcile()
	{
		if (reconciling) return;
		reconciling = true;
		try
		{
			bool updated = false;
			foreach (JuiceStream stream in map.HitObjects.OfType<JuiceStream>().ToArray())
			{
				string key = Signature(stream);
				if (!recipes.TryGetValue(key, out Guide? guide)) continue;
				JuiceStream original = guide.Restore(map, stream);
				CurveBaker.Result result = CurveBaker.Create(map, original, stream);
				if (result.Stream == null)
				{
					// Never silently leave an invalid compensation. Restore the guide,
					// which remains editable with the native tool, and explain the refusal.
					Apply(stream, original); updated = true;
					notify("Automatic alignment could not preserve this stream. Its original curve was restored. " + result.Message);
					continue;
				}
				if (Signature(result.Stream) == key) continue;
				Remember(result.Stream, guide);
				Apply(stream, result.Stream); updated = true;
			}
			if (updated) new CatchBeatmapProcessor(map).PostProcess();
			if (dirty && !Save()) notify("Curve alignment was updated, but its guide could not be saved. Check write access to the lazer data folder.");
		}
		finally { reconciling = false; }
	}
	private bool Save()
	{
		try
		{
			string temporary = filename + ".tmp";
			using (Stream output = storage.GetStream(temporary, FileAccess.Write, FileMode.Create)) JsonSerializer.Serialize(output, recipes);
			File.Move(storage.GetFullPath(temporary), storage.GetFullPath(filename), true);
			dirty = false; return true;
		}
		catch (Exception error) { Logger.Error(error, "EditorAddons could not save curve guides."); return false; }
	}
	internal static string Signature(JuiceStream stream)
	{
		using MemoryStream data = new();
		using (BinaryWriter writer = new(data, System.Text.Encoding.UTF8, true))
		{
			writer.Write(Math.Round(stream.OriginalX, 5)); writer.Write(Math.Round(stream.LegacyConvertedY, 5));
			writer.Write(Math.Round(stream.SliderVelocityMultiplier, 5));
			writer.Write(Math.Round(stream.Path.ExpectedDistance.Value ?? stream.Path.Distance, 5));
			writer.Write(stream.RepeatCount);
			foreach (PathControlPoint point in stream.Path.ControlPoints)
			{
				writer.Write(Math.Round(point.Position.X, 5)); writer.Write(Math.Round(point.Position.Y, 5));
				writer.Write(point.Type.HasValue);
				if (point.Type is PathType type) { writer.Write((int)type.Type); writer.Write(type.Degree ?? -1); }
			}
		}
		return Convert.ToHexString(SHA256.HashData(data.ToArray()));
	}
}
