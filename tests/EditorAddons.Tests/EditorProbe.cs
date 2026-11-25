using System.IO.Compression;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.EditorAddons;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osuTK.Input;
using osuTK;
using HarmonyLib;
using osu.Framework.Input.Bindings;
using osu.Game.Audio;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Edit.Blueprints;
using osu.Game.Rulesets.Catch.Edit.Blueprints.Components;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Compose.Components;

internal partial class EditorProbe : OsuGame
{
	private readonly string folder;
	private readonly DateTime start = DateTime.UtcNow;
	private UserInputManager input = null!;
	private IEnumerator<Func<bool>> steps = null!;
	private Func<bool>? pending;
	private Exception? failure;
	private bool passed;
	private int checks;
	private string stage = "startup";
	private EditorProbe(string folder) => this.folder = folder;
	protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(folder);
	protected override UserInputManager CreateUserInputManager() { input = base.CreateUserInputManager(); return input; }
	protected override void LoadComplete()
	{
		base.LoadComplete(); steps = Steps(); if (playbackOnly) steps = PlaybackSteps(); Scheduler.AddDelayed(Tick, 20, true);
	}
	private void Tick()
	{
		if (passed || failure != null) return;
		try
		{
			if ((DateTime.UtcNow - start).TotalSeconds > 420) throw new TimeoutException(stage);
			if (pending != null && !pending()) return;
			if (steps.MoveNext()) { pending = steps.Current; return; }
			passed = true; Console.WriteLine($"SUCCESS: {checks} real editor checks"); Exit();
		}
		catch (Exception error) { failure = error; Console.WriteLine("FAIL at " + stage + ": " + error); Exit(); }
	}
	private void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
	private void SendKey(Key key, bool down) => new KeyboardKeyInput(key, down).Apply(input.CurrentState, input);
	private void Tap(Key key) { SendKey(key, true); SendKey(key, false); }
	private void Mouse(Vector2 position) => new MousePositionAbsoluteInput { Position = position }.Apply(input.CurrentState, input);
	private void Button(bool down) => new MouseButtonInput(MouseButton.Left, down).Apply(input.CurrentState, input);
	private IEnumerator<Func<bool>> Steps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Check(Integration.Installed, "DLL bootstrapped by lazer without Shred");
		Check(!Dependencies.Get<RulesetStore>().AvailableRulesets.Any(r => r.ShortName == EditorAddonsRuleset.Id), "carrier invisible in real client");
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(Fixture()), "editor-tools-tests.osz") });
		yield return () => import.IsCompleted; import.GetAwaiter().GetResult();
		foreach (string mode in new[] { "mania", "taiko", "fruits", "osu" }.Where(m => Environment.GetEnvironmentVariable("EDITOR_TOOLS_TEST_MODE") == null || m == Environment.GetEnvironmentVariable("EDITOR_TOOLS_TEST_MODE")))
		{
			float[]? alignedPositions = null;
			stage = mode + " editor load";
			BeatmapInfo? info = null;
			yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "tools-" + mode)) != null;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info!, refetch: true); Ruleset.Value = info!.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse;
			Editor editor = (Editor)ScreenStack.CurrentScreen;
			editor.Mode.Value = EditorScreenMode.Compose;
			yield return () => editor.ChildrenOfType<HitObjectComposer>().Any(c => c.IsLoaded);
			HitObjectComposer composer = editor.ChildrenOfType<HitObjectComposer>().First(c => c.IsLoaded);
			if (mode == "osu")
			{
				Check(!editor.ChildrenOfType<RecorderController>().Any(), "standard has no step recorder");
			}
			else
			{
				yield return () => editor.ChildrenOfType<RecorderController>().Any(c => c.IsLoaded);
				RecorderController recorder = editor.ChildrenOfType<RecorderController>().Single();
				EditorClock clock = composer.Dependencies.Get<EditorClock>();
				EditorBeatmap map = composer.Dependencies.Get<EditorBeatmap>();
				BindableBeatDivisor divisor = composer.Dependencies.Get<BindableBeatDivisor>();
				divisor.Value = 4; clock.Stop(); clock.Seek(1000);
				yield return () => recorder.HasFocus;
				Check(editor.ChildrenOfType<RecorderControls>().Count() == (mode == "fruits" ? 0 : 1), mode + " requested sidebar layout");
				if (mode == "fruits")
				{
					Tap(Key.S); Check(divisor.Value == 8, "catch S finer spacing"); Tap(Key.A); Check(divisor.Value == 4, "catch A coarser spacing");
					stage = "catch mouse drawing";
					ComposeBlueprintContainer blueprints = editor.ChildrenOfType<ComposeBlueprintContainer>().Single();
					blueprints.CurrentTool = new FreehandJuiceTool();
					ScrollingHitObjectContainer objects = (ScrollingHitObjectContainer)composer.Playfield.HitObjectContainer;
					Vector2 At(double time, float x) => objects.ToScreenSpace(new Vector2(x, objects.PositionAtTime(time) + objects.DrawHeight));
					Mouse(At(1125, 100)); yield return () => blueprints.CurrentPlacement is FreehandJuicePlacement;
					Button(true); yield return () => true;
					Mouse(At(1250, 220)); yield return () => true;
					Mouse(At(1500, 340)); yield return () => true;
					Button(false); yield return () => true;
					JuiceStream? stream = map.HitObjects.OfType<JuiceStream>().FirstOrDefault();
					Check(stream != null, "catch mouse drag commits a native juice stream");
					Check(Math.Abs(stream!.EndTime - 1500) < 2, "catch drag preserves snapped endpoint");
					blueprints.CurrentTool = new SelectTool(); map.SelectedHitObjects.Add(stream);
					yield return () => editor.ChildrenOfType<JuiceStreamSelectionBlueprint>().Any(b => b.Item == stream && b.IsSelected);
					JuiceStreamSelectionBlueprint blueprint = editor.ChildrenOfType<JuiceStreamSelectionBlueprint>().Single(b => b.Item == stream);
					SelectionEditablePath path = blueprint.ChildrenOfType<SelectionEditablePath>().Single();
					var states = (IReadOnlyList<VertexState>)AccessTools.Property(typeof(EditablePath), "VertexStates").GetValue(path)!;
					Check(path.Vertices.Count >= 1 + (int)Math.Round(stream.Duration / 125), "catch drag retains every crossed grid vertex");
					states[1].IsSelected = true; states[2].IsSelected = true;
					Check(!path.ContextMenuItems.Any(i => i.Text.ToString().Contains("Convert selected")), "catch misleading partial conversion removed");
					Check(blueprint.ContextMenuItems.Any(i => i.Text.ToString() == "Convert to stream"), "catch native full conversion retained");
					float[] desired = stream.NestedHitObjects.OfType<CatchHitObject>().Select(h => h.OriginalX).ToArray();
					Vector2[] originalPath = stream.Path.ControlPoints.Select(p => p.Position).ToArray();
					blueprint.ContextMenuItems.Single(i => i.Text.ToString().StartsWith("Stick to curve")).Action.Value!();
					yield return () => true;
					Check(stream.NestedHitObjects.OfType<TinyDroplet>().Any() && stream.NestedHitObjects.OfType<CatchHitObject>().Select((h, i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch baked positions align droplets through native gameplay offsets");
					int version = stream.Path.Version.Value;
					CatchMenus.Bake(map, stream);
					Check(stream.Path.Version.Value == version, "catch repeat click does not bake twice");
					AccessTools.Method(typeof(Editor), "Undo").Invoke(editor, null); yield return () => true;
					Vector2[] restoredPath = map.HitObjects.OfType<JuiceStream>().Single().Path.ControlPoints.Select(p => p.Position).ToArray();
					// Native history serialises absolute positions and decodes relative positions.
					// Allow float round-off from that addition and subtraction.
					Check(restoredPath.Length == originalPath.Length && restoredPath.Select((p, i) => Vector2.Distance(p, originalPath[i])).Max() < 0.0001f, "catch native undo restores pre-alignment curve");
					AccessTools.Method(typeof(Editor), "Redo").Invoke(editor, null); yield return () => true;
					Check(map.HitObjects.OfType<JuiceStream>().Single().NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch native redo restores baked alignment");
					stream = map.HitObjects.OfType<JuiceStream>().Single();
					BananaShower shower = new() { StartTime = 0, Duration = 700 };
					map.Add(shower); yield return () => true;
					Check(stream.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch alignment recalculates after earlier banana shower insertion");
					map.BeginChange(); shower.Duration = 850; map.Update(shower); map.EndChange(); yield return () => true;
					Check(stream.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch alignment recalculates after banana shower duration change");
					AccessTools.Method(typeof(Editor), "Undo").Invoke(editor, null); yield return () => true;
					Check(map.HitObjects.OfType<JuiceStream>().Single().NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch undo of earlier edit preserves alignment");
					AccessTools.Method(typeof(Editor), "Redo").Invoke(editor, null); yield return () => true;
					Check(map.HitObjects.OfType<JuiceStream>().Single().NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - desired[i])).Max() <= 1, "catch redo of earlier edit preserves alignment");
					alignedPositions = desired;

				}
				else
				{
					SendKey(Key.ControlLeft, true); Tap(Key.Q); SendKey(Key.ControlLeft, false);
					Check(recorder.Enabled.Value, mode + " Ctrl+Q enables recorder");
					yield return () => true;
					BeatSnapGrid visibleGrid = ((IRecorderComposer)composer).RecorderGrid!;
					Check(AccessTools.Field(typeof(BeatSnapGrid), "selectionTimeRange").GetValue(visibleGrid) != null, mode + " recorder keeps native grid visible in Select with no selection");
					var grids = (List<ScrollingHitObjectContainer>)AccessTools.Field(typeof(BeatSnapGrid), "grids").GetValue(visibleGrid)!;
					// The composer sets the range after child updates; line creation follows next frame.
					DateTime gridDeadline = DateTime.UtcNow.AddSeconds(5);
					yield return () => grids.All(g => g.Objects.Any()) || DateTime.UtcNow > gridDeadline;
					Check(grids.Count > 0 && grids.All(g => g.Objects.Any(l => Math.Abs(l.HitObject.StartTime - 1125) < 1 && l.Alpha == 1)), mode + " native grid actually contains opaque lines at current subdivision");
					Tap(Key.Number7); Check(divisor.Value == 2, mode + " 7 decreases subdivision"); Tap(Key.Number8); Check(divisor.Value == 4, mode + " 8 increases subdivision");
					stage = mode + " note input";
					if (mode == "mania")
					{
						SendKey(Key.D, true); SendKey(Key.F, true); SendKey(Key.D, false);
						Check(Math.Abs(clock.CurrentTimeAccurate - 1000) < 1, "mania waits for chord release");
						SendKey(Key.F, false);
						Check(map.HitObjects.OfType<Note>().Count(n => Math.Abs(n.StartTime - 1000) < 1) == 2, "mania DF creates two native notes in Select");
						Check(Math.Abs(clock.CurrentTimeAccurate - 1125) < 1, "mania chord advances one grid step");
						Check(clock.CurrentTime < 1124, "mania automatic step uses a smooth clock transition");
						SendKey(Key.J, true); Tap(Key.Slash); Tap(Key.Slash); SendKey(Key.J, false);
						Check(map.HitObjects.OfType<HoldNote>().Any(n => Math.Abs(n.StartTime - 1125) < 1 && Math.Abs(n.Duration - 250) < 1), "mania held gameplay key plus empty step creates native LN");
						stage = "mania live remapping";
						Dependencies.Get<RealmAccess>().Write(r =>
						{
							var binding = r.All<RealmKeyBinding>().First(b => b.RulesetName == "mania" && b.Variant == 4 && b.ActionInt == 0);
							binding.KeyCombination = new KeyCombination(InputKey.W);
						});
						yield return () => true; yield return () => true;
						clock.Seek(3000); Tap(Key.W);
						Check(map.HitObjects.OfType<Note>().Any(n => n.StartTime == 3000 && n.Column == 0), "mania follows a live gameplay remap to W");
						Check(composer.SelectionSampleStates[HitSampleInfo.HIT_WHISTLE].Value != TernaryState.True, "recorder gameplay key takes priority over W hitsound");
						recorder.Enabled.Value = false; Tap(Key.W);
						Check(composer.SelectionSampleStates[HitSampleInfo.HIT_WHISTLE].Value == TernaryState.True, "native W hitsound works with recorder disabled");
						recorder.Enabled.Value = true; clock.Seek(3500); Tap(Key.K);
						Check(map.HitObjects.OfType<Note>().Any(n => n.StartTime == 3500 && n.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE)), "recorded notes inherit native hitsound selection");
						SendKey(Key.ControlLeft, true); Tap(Key.Z); SendKey(Key.ControlLeft, false);
						yield return () => true;
						Check(!map.HitObjects.Any(n => n.StartTime == 3500), "recorder note is one native undo action");
						HoldNote overlap = new() { StartTime = 6000, Duration = 1000, Column = 1 }; map.Add(overlap);
						clock.Seek(6500); Tap(Key.F);
						Check(overlap.EndTime == 6375, "mania note in LN body leaves one current grid step before new head");
						HoldNote tail = new() { StartTime = 8000, Duration = 1000, Column = 1 }; map.Add(tail);
						clock.Seek(9000); Tap(Key.F);
						Check(tail.EndTime == 8875, "mania note at exact LN tail also leaves one grid step");
						clock.Seek(8000); Tap(Key.F);
						Check(map.HitObjects.OfType<ManiaHitObject>().Count(n => n.Column == 1 && n.StartTime == 8000) == 1 && !map.HitObjects.Contains(tail), "mania note at LN head replaces hold without duplicate");
						map.Add(new HoldNote { StartTime = 10000, Duration = 125, Column = 1 }); clock.Seek(10125); Tap(Key.F);
						Check(map.HitObjects.OfType<Note>().Any(n => n.Column == 1 && n.StartTime == 10000), "mania collapsed LN becomes a short note");
						map.Add(new Note { StartTime = 12125, Column = 1 }); clock.Seek(12000); SendKey(Key.F, true); Tap(Key.Slash); Tap(Key.Slash); SendKey(Key.F, false);
						Check(!map.HitObjects.OfType<HoldNote>().Any(n => n.Column == 1 && n.StartTime == 12000), "mania extending LN reserves one grid step before following note");
						divisor.ValidDivisors.Value = BeatDivisorPresetCollection.TRIPLETS; divisor.Value = 6; map.Add(new HoldNote { StartTime = 14000, Duration = 1000, Column = 1 }); clock.Seek(14500); Tap(Key.F);
						Check(Math.Abs(map.HitObjects.OfType<HoldNote>().Single(n => n.StartTime == 14000).EndTime - (14500 - 500d / 6)) < 0.01, "mania LN separation follows current triplet grid");
						divisor.ValidDivisors.Value = BeatDivisorPresetCollection.COMMON; divisor.Value = 4;
					}
					else
					{
						SendKey(Key.F, true); SendKey(Key.J, true); SendKey(Key.F, false); SendKey(Key.J, false);
						Check(map.HitObjects.OfType<Hit>().Any(h => h.StartTime == 1000 && h.Type == HitType.Centre && h.IsStrong), "taiko two centre keys create Big Don");
						Check(clock.CurrentTime < 1124 && clock.CurrentTimeAccurate == 1125, "taiko automatic step uses a smooth clock transition");
						SendKey(Key.D, true); SendKey(Key.K, true); SendKey(Key.D, false); SendKey(Key.K, false);
						Check(map.HitObjects.OfType<Hit>().Any(h => h.StartTime == 1125 && h.Type == HitType.Rim && h.IsStrong), "taiko two rim keys create Big Katsu");
						clock.Seek(1125); Tap(Key.F);
						Check(map.HitObjects.OfType<Hit>().Count(h => Math.Abs(h.StartTime - 1125) <= 1) == 1, "taiko repeated recording replaces hit at same timestamp");
						map.Add(new Hit { StartTime = 1333 }); map.Add(new Hit { StartTime = 1333 }); divisor.ValidDivisors.Value = BeatDivisorPresetCollection.TRIPLETS; divisor.Value = 3; clock.Seek(4000d / 3); Tap(Key.F);
						Check(map.HitObjects.OfType<Hit>().Count(h => Math.Abs(h.StartTime - 4000d / 3) <= 1) == 1, "taiko imported rounded timestamp and existing duplicates collapse to one hit");
						Check(Math.Abs(clock.CurrentTimeAccurate - 1500) < 0.01, "taiko reused rounded note advances a full grid step");
						divisor.ValidDivisors.Value = BeatDivisorPresetCollection.COMMON; divisor.Value = 4;
					}
					recorder.ReturnOnPause.Value = true; recorder.Enabled.Value = false; clock.Seek(2000);
					yield return () => true;
					Check(AccessTools.Field(typeof(BeatSnapGrid), "selectionTimeRange").GetValue(visibleGrid) == null, mode + " recorder off restores native grid visibility");
					Tap(Key.Space); yield return () => clock.CurrentTimeAccurate > 2100; Tap(Key.Space);
					Check(!clock.IsRunning && Math.Abs(clock.CurrentTimeAccurate - 2000) < 1, mode + " Return on pause independent of recorder");
					Tap(Key.Space); yield return () => clock.CurrentTimeAccurate > 2150;
					SendKey(Key.ControlLeft, true); Tap(Key.Space); SendKey(Key.ControlLeft, false);
					Check(!clock.IsRunning && clock.CurrentTimeAccurate > 2100, mode + " Ctrl+Space pauses at current position");
				}
				foreach (Func<bool> wait in SelectionChecks(composer, map, clock, recorder, mode)) yield return wait;
			}
			// Save and exit normally; this also exercises native serialization.
			EditorBeatmap savedMap = composer.Dependencies.Get<EditorBeatmap>();
			var expected = savedMap.HitObjects.Select(h => (Type: h.GetType(), Time: h.StartTime)).ToArray();
			typeof(Editor).GetMethod("Save", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(editor, null); editor.Exit();
			yield return () => ScreenStack.CurrentScreen is MainMenu;
			BeatmapInfo savedInfo = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "tools-" + mode)!;
			IBeatmap decoded = BeatmapManager.GetWorkingBeatmap(savedInfo, refetch: true).GetPlayableBeatmap(savedInfo.Ruleset);
			Check(decoded.HitObjects.Count == expected.Length && expected.All(e => decoded.HitObjects.Any(h => h.GetType() == e.Type && Math.Abs(h.StartTime - e.Time) < 1.1)), mode + " native save and reload preserves note types and timings");
			if (alignedPositions != null)
			{
				Check(decoded.HitObjects.OfType<JuiceStream>().Single().NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - alignedPositions[i])).Max() <= 1, "catch alignment survives native save reload and gameplay processing");
				Beatmap.Value = BeatmapManager.GetWorkingBeatmap(savedInfo, refetch: true); Ruleset.Value = savedInfo.Ruleset;
				ScreenStack.Push(new EditorLoader());
				yield return () => ScreenStack.CurrentScreen is Editor reopened && reopened.ReadyForUse;
				Editor reopenedEditor = (Editor)ScreenStack.CurrentScreen;
				reopenedEditor.Mode.Value = EditorScreenMode.Compose;
				yield return () => reopenedEditor.ChildrenOfType<EnhancedCatchComposer>().Any(c => c.IsLoaded);
				EditorBeatmap reopenedMap = reopenedEditor.ChildrenOfType<EnhancedCatchComposer>().Single().Dependencies.Get<EditorBeatmap>();
				JuiceStream target = reopenedMap.HitObjects.OfType<JuiceStream>().Single();
				CurveTracking book = CurveTracking.For(reopenedMap)!;
				Check(book.IsTracked(target), "catch saved guide is recognised after reopening editor");
				JuiceStream prefix = new() { StartTime = 100, OriginalX = 256, Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(0, 100)) }, 100) };
				reopenedMap.Add(prefix); yield return () => true;
				Check(target.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - alignedPositions[i])).Max() <= 1, "catch persisted guide compensates earlier stream insertion");
				reopenedMap.BeginChange(); prefix.Path.ExpectedDistance.Value = 200; reopenedMap.Update(prefix); reopenedMap.EndChange(); yield return () => true;
				Check(target.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - alignedPositions[i])).Max() <= 1, "catch alignment compensates earlier stream length edit");
				reopenedMap.Remove(prefix); yield return () => true;
				Check(target.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - alignedPositions[i])).Max() <= 1, "catch alignment compensates earlier stream removal");
				book.RestoreOriginal(target); yield return () => true;
				Check(!book.IsTracked(target) && target.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.OriginalX - alignedPositions[i])).Max() < 0.01, "catch restore original recovers editable guide after reopening");
				book.Bake(target); yield return () => true;
				Check(book.IsTracked(target) && target.NestedHitObjects.Cast<CatchHitObject>().Select((h,i) => Math.Abs(h.EffectiveX - alignedPositions[i])).Max() <= 1, "catch original can be aligned again without cumulative drift");
				// Simulate a v0.2 conversion with no saved guide, then adopt its visible positions.
				reopenedMap.Add(prefix); yield return () => true;
				JuiceStream legacy = CurveBaker.Create(reopenedMap, prefix).Stream!;
				reopenedMap.BeginChange(); prefix.SliderVelocityMultiplier = legacy.SliderVelocityMultiplier; prefix.Path = legacy.Path; reopenedMap.Update(prefix); reopenedMap.EndChange(); yield return () => true;
				float[] adopted = prefix.NestedHitObjects.Cast<CatchHitObject>().Select(h => h.EffectiveX).ToArray();
				Check(!book.IsTracked(prefix), "catch old conversion starts without a saved guide");
				book.TrackCurrentPositions(prefix); yield return () => true;
				Check(book.IsTracked(prefix) && prefix.NestedHitObjects.Cast<CatchHitObject>().Select((h, i) => Math.Abs(h.EffectiveX - adopted[i])).Max() <= 1, "catch adopts existing droplet positions without baking the zigzag as a new target");
				BananaShower olderShower = new() { StartTime = 0, Duration = 80 }; reopenedMap.Add(olderShower); yield return () => true;
				Check(prefix.NestedHitObjects.Cast<CatchHitObject>().Select((h, i) => Math.Abs(h.EffectiveX - adopted[i])).Max() <= 1, "catch adopted old conversion stays aligned after preceding edits");
				CheckExportAlignment(reopenedMap, prefix, adopted);
				reopenedMap.Remove(olderShower); reopenedMap.Remove(prefix); yield return () => true;
				typeof(Editor).GetMethod("Save", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(reopenedEditor, null); reopenedEditor.Exit();
				yield return () => ScreenStack.CurrentScreen is MainMenu;
			}
		}
	}
	private void CheckExportAlignment(EditorBeatmap map, JuiceStream stream, float[] target)
	{
		using StringWriter encoded = new(); new osu.Game.Beatmaps.Formats.LegacyBeatmapEncoder(map, null, null).Encode(encoded);
		using MemoryStream data = new(Encoding.UTF8.GetBytes(encoded.ToString()));
		using osu.Game.IO.LineBufferedReader reader = new(data);
		Beatmap decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
		IBeatmap playable = new FlatWorkingBeatmap(decoded).GetPlayableBeatmap(new osu.Game.Rulesets.Catch.CatchRuleset().RulesetInfo);
		LegacyBeatmapExporter exporter = new(new NativeStorage(Path.GetTempPath()));
		AccessTools.Method(typeof(LegacyBeatmapExporter), "MutateBeatmap").Invoke(exporter, new object[] { new BeatmapSetInfo(), playable });
		using StringWriter exported = new(); new osu.Game.Beatmaps.Formats.LegacyBeatmapEncoder(playable, null, null).Encode(exported);
		using MemoryStream exportData = new(Encoding.UTF8.GetBytes(exported.ToString()));
		using osu.Game.IO.LineBufferedReader exportReader = new(exportData);
		Beatmap reloaded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(exportReader).Decode(exportReader);
		IBeatmap game = new FlatWorkingBeatmap(reloaded).GetPlayableBeatmap(new osu.Game.Rulesets.Catch.CatchRuleset().RulesetInfo);
		CatchHitObject[] actual = game.HitObjects.OfType<JuiceStream>().Single(h => Math.Abs(h.StartTime - stream.StartTime) < 1).NestedHitObjects.Cast<CatchHitObject>().ToArray();
		Check(actual.Length == target.Length && actual.Select((h, i) => Math.Abs(h.EffectiveX - target[i])).Max() <= 1, "catch automatic compensation survives classic export and unmodified native gameplay");
	}

	private IEnumerable<Func<bool>> SelectionChecks(HitObjectComposer composer, EditorBeatmap map, EditorClock clock, RecorderController recorder, string mode)
	{
		stage = mode + " modifier selection";
		recorder.Enabled.Value = false; clock.Stop(); clock.Seek(19875);
		map.SelectedHitObjects.Clear();
		HitObject[] items = Enumerable.Range(0, 5).Select(i =>
		{
			HitObject item = new Note { Column = 2 };
			if (mode == "taiko") item = new Hit();
			if (mode == "fruits") item = new Fruit { OriginalX = 256 };
			item.StartTime = 20000 + i * 125;
			return item;
		}).ToArray();
		foreach (HitObject item in items) map.Add(item);
		ComposeBlueprintContainer blueprints = composer.BlueprintContainer;
		yield return () => items.All(h => blueprints.SelectionBlueprints.AliveChildren.Any(b => b.Item == h && b.IsSelectable));
		Vector2 At(int index) => blueprints.SelectionBlueprints.AliveChildren.Single(b => b.Item == items[index]).ScreenSpaceSelectionPoint;
		CompositionTool creationTool = new osu.Game.Rulesets.Mania.Edit.NoteCompositionTool();
		if (mode == "taiko") creationTool = new osu.Game.Rulesets.Taiko.Edit.HitCompositionTool();
		if (mode == "fruits") creationTool = new osu.Game.Rulesets.Catch.Edit.FruitCompositionTool();
		composer.ChildrenOfType<HitObjectCompositionToolButton>().First(b => b.Tool.GetType() == creationTool.GetType()).Select();
		int count = map.HitObjects.Count;
		SendKey(Key.ControlLeft, true); Mouse(At(0)); yield return () => true;
		Button(true); Button(false); SendKey(Key.ControlLeft, false); yield return () => true;
		Check(blueprints.CurrentTool is SelectTool && map.SelectedHitObjects.SequenceEqual(new[] { items[0] }) && map.HitObjects.Count == count, mode + " Ctrl click switches placement to Select without creating a note");
		SendKey(Key.ControlLeft, true); Mouse(At(4)); yield return () => true;
		Button(true); Button(false); SendKey(Key.ControlLeft, false); yield return () => true;
		Check(map.SelectedHitObjects.ToHashSet().SetEquals(new[] { items[0], items[4] }), mode + " Ctrl click selects separate groups");
		SendKey(Key.ShiftLeft, true); Mouse(At(2)); yield return () => true;
		Button(true); Button(false); SendKey(Key.ShiftLeft, false); yield return () => true;
		Check(map.SelectedHitObjects.ToHashSet().SetEquals(new[] { items[0], items[2], items[3], items[4] }), mode + " Shift click extends range from last selected group");
		SendKey(Key.ControlLeft, true); Mouse(At(0)); yield return () => true;
		Button(true); Button(false); SendKey(Key.ControlLeft, false); yield return () => true;
		Check(!map.SelectedHitObjects.Contains(items[0]) && map.SelectedHitObjects.Count == 3, mode + " Ctrl click toggles one selected object off");
		map.SelectedHitObjects.Clear(); composer.ChildrenOfType<HitObjectCompositionToolButton>().First(b => b.Tool.GetType() == creationTool.GetType()).Select();
		Vector2 first = Vector2.ComponentMin(At(0), At(1)) - new Vector2(8);
		Vector2 end = Vector2.ComponentMax(At(0), At(1)) + new Vector2(8);
		SendKey(Key.ControlLeft, true); Mouse(first); yield return () => true;
		Button(true); Mouse(end); yield return () => true;
		Button(false); SendKey(Key.ControlLeft, false); yield return () => true;
		Check(map.SelectedHitObjects.ToHashSet().SetEquals(new[] { items[0], items[1] }) && map.HitObjects.Count == count, mode + " Ctrl drag from creation tool selects a rectangle without placement");
		first = At(4) - new Vector2(8); end = At(4) + new Vector2(8);
		SendKey(Key.ControlLeft, true); Mouse(first); yield return () => true;
		Button(true); Mouse(end); yield return () => true;
		Button(false); SendKey(Key.ControlLeft, false); yield return () => true;
		Check(map.SelectedHitObjects.ToHashSet().SetEquals(new[] { items[0], items[1], items[4] }), mode + " Ctrl drag adds a separate rectangle");
		first = At(2) - new Vector2(8); end = At(2) + new Vector2(8);
		SendKey(Key.ShiftLeft, true); Mouse(first); yield return () => true;
		Button(true); Mouse(end); yield return () => true;
		Button(false); SendKey(Key.ShiftLeft, false); yield return () => true;
		Check(map.SelectedHitObjects.ToHashSet().SetEquals(items), mode + " Shift drag fills the time range from the last rectangle");
		Check(items.Select((h, i) => h.StartTime == 20000 + i * 125).All(v => v), mode + " modified drags do not move selected notes");
		map.SelectedHitObjects.Clear(); composer.ChildrenOfType<HitObjectCompositionToolButton>().First(b => b.Tool.GetType() == creationTool.GetType()).Select();
		Mouse(At(4)); yield return () => true;
		new MouseButtonInput(MouseButton.Right, true).Apply(input.CurrentState, input);
		new MouseButtonInput(MouseButton.Right, false).Apply(input.CurrentState, input);
		yield return () => true;
		Check(!map.HitObjects.Contains(items[4]) && map.HitObjects.Count == count - 1, mode + " native right click deletes while a creation tool is active");
		// Keep these test objects out of curve save/reload fixtures.
		foreach (HitObject item in items.Where(map.HitObjects.Contains).ToArray()) map.Remove(item);
		map.SelectedHitObjects.Clear(); composer.BlueprintContainer.CurrentTool = new SelectTool();
	}

	private string Fixture()
	{
		string path = Path.Combine(folder, "fixture.osz");
		using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
		using (BinaryWriter wav = new(zip.CreateEntry("tone.wav").Open()))
		{
			const int samples = 8000 * 30;
			wav.Write(Encoding.ASCII.GetBytes("RIFF")); wav.Write(36 + samples * 2); wav.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); wav.Write(16);
			wav.Write((short)1); wav.Write((short)1); wav.Write(8000); wav.Write(16000); wav.Write((short)2); wav.Write((short)16);
			wav.Write(Encoding.ASCII.GetBytes("data")); wav.Write(samples * 2);
			for (int i = 0; i < samples; i++) wav.Write((short)0);
		}
		string[] names = { "osu", "taiko", "fruits", "mania" };
		for (int i = 0; i < names.Length; i++)
		{
			using StreamWriter w = new(zip.CreateEntry(names[i] + ".osu").Open());
			w.Write($"osu file format v14\n[General]\nAudioFilename:tone.wav\nMode:{i}\n[Metadata]\nTitle:EditorAddons tests\nArtist:tests\nCreator:tests\nVersion:tools-{names[i]}\nBeatmapID:-1\nBeatmapSetID:-1\n[Difficulty]\nCircleSize:4\nOverallDifficulty:5\nHPDrainRate:5\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,1,0,50,1,0\n[HitObjects]\n64,192,5000,1,0,0:0:0:0:\n");
		}
		return path;
	}
	public static void Run(bool playback = false)
	{
		string folder = Path.Combine(Path.GetTempPath(), "editor-tools-test-" + Guid.NewGuid());
		Directory.CreateDirectory(Path.Combine(folder, "rulesets"));
		if (playback) File.WriteAllText(Path.Combine(folder, "shred-editor-playback.ini"), "Pitched = False\n");
		File.Copy(typeof(EditorAddonsRuleset).Assembly.Location, Path.Combine(folder, "rulesets", "osu.Game.Rulesets.EditorAddons.dll"));
		using (OsuConfigManager config = new(new NativeStorage(folder))) config.SetValue(OsuSetting.ShowFirstRunSetup, false);
		Console.WriteLine("TEST DATA " + folder);
		using HeadlessGameHost host = new("editor-tools-tests"); using EditorProbe game = new(folder); game.playbackOnly = playback; host.Run(game);
		if (!game.passed) throw new Exception("Editor integration failed; logs: " + folder, game.failure);
	}
}
