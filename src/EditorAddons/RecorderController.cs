using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Input;

namespace osu.Game.Rulesets.EditorAddons;

internal partial class RecorderController : Container
{
	[Resolved] private EditorClock clock { get; set; } = null!;
	[Resolved] private EditorBeatmap beatmap { get; set; } = null!;
	[Resolved] private IEditorChangeHandler changes { get; set; } = null!;
	[Resolved] private IBeatSnapProvider snap { get; set; } = null!;
	[Resolved] private BindableBeatDivisor divisor { get; set; } = null!;
	[Resolved] private RealmAccess realm { get; set; } = null!;
	[Resolved] private GameHost host { get; set; } = null!;
	[Resolved(canBeNull: true)] private Editor? editor { get; set; }
	public readonly BindableBool Enabled = new(false);
	public readonly BindableBool ReturnOnPause = new(false);
	internal (double start, double end) GridRange
	{
		get
		{
			double centre = Math.Floor(clock.CurrentTime / 500) * 500;
			double range = Math.Max(1000, composer.Dependencies.Get<IScrollingInfo>().TimeRange.Value);
			return (centre - range, centre + range + 500);
		}
	}
	private readonly HitObjectComposer composer;
	private readonly Dictionary<RecorderModel.Entry, ManiaHitObject> notes = new();
	private readonly HashSet<int> active = new();
	private RecorderModel? model;
	private Hit? taikoNote;
	private readonly HashSet<int> taikoGesture = new();
	private List<IKeyBinding> gameplay = new();
	private List<IKeyBinding> commands = new();
	private IDisposable? subscription;
	private bool changing;
	private bool wasRunning;
	private double returnPosition;
	private double lastTime;
	private bool started;
	private InputManager? input;
	private bool isCatch => composer.Ruleset is CatchRuleset;
	private bool isMania => composer.Ruleset is ManiaRuleset;
	public override bool AcceptsFocus => true;
	public override bool RequestsFocus
	{
		get
		{
			if (!started || host.IsActive.Value == false || !IsPresent || editor?.Mode.Value != EditorScreenMode.Compose) return false;
			Drawable? focused = input?.FocusedDrawable;
			return focused == null || focused == this;
		}
	}
	public RecorderController(HitObjectComposer composer)
	{
		this.composer = composer;
		RelativeSizeAxes = Axes.Both;
		Depth = -1000;
		Enabled.BindValueChanged(_ => Finish());
	}
	public override bool ReceivePositionalInputAt(osuTK.Vector2 screenSpacePos) => false;

	protected override void LoadComplete()
	{
		base.LoadComplete();
		input = GetContainingInputManager();
		string name = composer.Ruleset.ShortName;
		int variant = composer.Ruleset.GetVariantForBeatmap(beatmap.BeatmapInfo);
		if (isMania) variant = ((ManiaHitObjectComposer)composer).Playfield.TotalColumns;
		subscription = realm.RegisterForNotifications(r => r.All<RealmKeyBinding>().Where(k => k.RulesetName == name), (items, _) =>
		{
			List<RealmKeyBinding> copy = items.AsQueryable().Detach();
			gameplay = copy.Where(k => k.Variant == variant).Cast<IKeyBinding>().ToList();
			commands = copy.Where(k => k.Variant == Ruleset.EDITOR_VARIANT && Bindings.IsEditorAction(k.ActionInt)).Cast<IKeyBinding>().ToList();
			if (gameplay.Count == 0) gameplay = composer.Ruleset.GetDefaultKeyBindings(variant).Cast<IKeyBinding>().ToList();
			if (commands.Count == 0) commands = Bindings.Defaults(composer.Ruleset).Cast<IKeyBinding>().ToList();
		});
		Preferences.Bind(host.Storage, name, Enabled, ReturnOnPause);
		lastTime = clock.CurrentTimeAccurate;
		wasRunning = clock.IsRunning;
		started = true;
	}

	private bool Ready => started && IsPresent && host.IsActive.Value && (editor == null || editor.Mode.Value == EditorScreenMode.Compose);
	protected override bool OnKeyDown(KeyDownEvent e)
	{
		if (!Ready || !HasFocus) return false;
		foreach (IKeyBinding binding in commands)
		{
			if (!binding.KeyCombination.Keys.Contains(KeyCombination.FromKey(e.Key))) continue;
			if (!binding.KeyCombination.IsPressed(KeyCombination.FromInputState(e.CurrentState), e.CurrentState, KeyCombinationMatchingMode.Modifiers)) continue;
			EditorAction action = (EditorAction)Convert.ToInt32(binding.Action);
			if (!e.Repeat) Command(action);
			return true;
		}
		if (!isCatch && e.Key == Key.Space && (e.ControlPressed || !Enabled.Value || !MatchesGameplay(e)))
		{
			if (!e.Repeat) TogglePlayback(e.ControlPressed);
			return true;
		}
		if (!isCatch && Enabled.Value && (e.Key == Key.Left || e.Key == Key.Right) && !e.ControlPressed && !e.AltPressed && !e.ShiftPressed)
		{
			int direction = 1;
			if (e.Key == Key.Left) direction = -1;
			Navigate(direction);
			return true;
		}
		if (e.ControlPressed || e.SuperPressed)
		{
			Finish();
			return false;
		}
		if (Enabled.Value && !clock.IsRunning && !isCatch && MatchesGameplay(e))
		{
			if (!e.Repeat) SyncGameplay(e.CurrentState);
			return true;
		}
		if (e.Key == Key.Escape) Finish();
		return false;
	}
	private bool MatchesGameplay(KeyDownEvent e) => gameplay.Any(b => b.KeyCombination.Keys.Contains(KeyCombination.FromKey(e.Key)) && b.KeyCombination.IsPressed(KeyCombination.FromInputState(e.CurrentState), e.CurrentState, KeyCombinationMatchingMode.Modifiers));
	protected override void OnKeyUp(KeyUpEvent e)
	{
		base.OnKeyUp(e);
		// A release must never introduce a new note (for example when Ctrl is
		// released after an editing shortcut while another key remains down).
		if (Ready && HasFocus && Enabled.Value && !clock.IsRunning) SyncGameplay(e.CurrentState, false);
		else Finish();
	}
	private void SyncGameplay(osu.Framework.Input.States.InputState state, bool allowNew = true)
	{
		HashSet<int> pressed = gameplay.Where(b => b.KeyCombination.IsPressed(KeyCombination.FromInputState(state), state, KeyCombinationMatchingMode.Modifiers))
			.Select(b => Convert.ToInt32(b.Action)).Where(a => a >= 0 && a < 10000).ToHashSet();
		if (allowNew)
			foreach (int action in pressed.Except(active).ToArray()) { active.Add(action); Press(action); }
		foreach (int action in active.Except(pressed).ToArray())
		{
			active.Remove(action);
			if (isMania) model?.Release(action);
		}
		if (!isMania && active.Count == 0 && taikoNote != null)
		{
			clock.SeekSmoothlyTo(Grid.Next(beatmap.ControlPointInfo, divisor.Value, snap.SnapTime(taikoNote.StartTime)));
			taikoNote = null; taikoGesture.Clear();
		}
		if (active.Count == 0) EndChange();
	}
	private void Command(EditorAction action)
	{
		switch (action)
		{
			case EditorAction.ToggleRecorder: Enabled.Value = !Enabled.Value; break;
			case EditorAction.EmptyStep: if (Enabled.Value) Navigate(1); break;
			default: Grid.Adjust(divisor, action, !isCatch); break;
		}
	}
	private void Navigate(int direction)
	{
		if (clock.IsRunning) { Finish(); clock.SeekSmoothlyTo(Grid.Next(beatmap.ControlPointInfo, divisor.Value, clock.CurrentTimeAccurate, direction)); }
		else if (isMania) { EnsureModel(); model!.Move(direction); }
		else { Finish(); clock.SeekSmoothlyTo(Grid.Next(beatmap.ControlPointInfo, divisor.Value, clock.CurrentTimeAccurate, direction)); }
		returnPosition = clock.CurrentTimeAccurate;
	}
	private void EnsureModel()
	{
		if (model != null) return;
		model = new RecorderModel(snap.SnapTime(clock.CurrentTimeAccurate), (t, d) => Grid.Next(beatmap.ControlPointInfo, divisor.Value, t, d), AddNote, ExtendNote, clock.SeekSmoothlyTo);
	}
	private void BeginChange() { if (changing) return; beatmap.BeginChange(); changing = true; }
	private void EndChange() { if (!changing) return; beatmap.EndChange(); changing = false; notes.Clear(); model = null; }
	private void Press(int action)
	{
		BeginChange();
		if (isMania)
		{
			if (action >= ((ManiaHitObjectComposer)composer).Playfield.TotalColumns) return;
			EnsureModel(); model!.Press(action); return;
		}
		taikoGesture.Add(action);
		HitType type = HitType.Centre;
		if (action == (int)TaikoAction.LeftRim || action == (int)TaikoAction.RightRim) type = HitType.Rim;
		bool strong = taikoGesture.Contains((int)TaikoAction.LeftCentre) && taikoGesture.Contains((int)TaikoAction.RightCentre);
		if (type == HitType.Rim) strong = taikoGesture.Contains((int)TaikoAction.LeftRim) && taikoGesture.Contains((int)TaikoAction.RightRim);
		if (taikoNote == null)
		{
			double time = snap.SnapTime(clock.CurrentTimeAccurate);
			// Imported timestamps can be rounded to whole milliseconds.
			Hit[] matches = beatmap.HitObjects.OfType<Hit>().Where(h => Math.Abs(h.StartTime - time) <= 1).ToArray();
			taikoNote = matches.FirstOrDefault();
			foreach (Hit duplicate in matches.Skip(1)) beatmap.Remove(duplicate);
			if (taikoNote == null)
			{
				taikoNote = new Hit { StartTime = time };
				ApplyPlacementState(taikoNote);
				beatmap.Add(taikoNote);
			}
		}
		taikoNote.Type = type; taikoNote.IsStrong = strong;
		Refresh(taikoNote);
	}
	private void ApplyPlacementState(HitObject note)
	{
		// Use lazer's own sample/bank/volume/new-combo inheritance.
		using PlacementStateManager manager = new(note);
		LoadComponent(manager);
	}
	private void AddNote(RecorderModel.Entry entry)
	{
		ManiaHitObject[] matches = beatmap.HitObjects.OfType<ManiaHitObject>().Where(n => n.Column == entry.Column && Math.Abs(n.StartTime - entry.Start) <= 1).ToArray();
		ManiaHitObject? existing = matches.FirstOrDefault();
		foreach (ManiaHitObject duplicate in matches.Skip(1)) beatmap.Remove(duplicate);
		foreach (HoldNote prior in beatmap.HitObjects.OfType<HoldNote>().Where(n => n.Column == entry.Column && n.StartTime < entry.Start - 1 && n.EndTime >= entry.Start - 1).ToArray())
		{
			TrimHold(prior, Grid.Next(beatmap.ControlPointInfo, divisor.Value, entry.Start, -1));
		}
		if (existing is Note) { notes[entry] = existing; return; }
		Note note = new() { StartTime = entry.Start, Column = entry.Column };
		if (existing != null) { note.Samples = existing.Samples; beatmap.Remove(existing); }
		else ApplyPlacementState(note);
		Refresh(note); beatmap.Add(note); notes[entry] = note;
	}
	private void TrimHold(HoldNote hold, double end)
	{
		if (end > hold.StartTime + 1) { hold.Duration = end - hold.StartTime; Refresh(hold); return; }
		Note replacement = new() { StartTime = hold.StartTime, Column = hold.Column, Samples = hold.Samples };
		beatmap.Remove(hold); Refresh(replacement); beatmap.Add(replacement);
		foreach (RecorderModel.Entry entry in notes.Where(pair => pair.Value == hold).Select(pair => pair.Key).ToArray()) notes[entry] = replacement;
	}
	private void ExtendNote(RecorderModel.Entry entry)
	{
		if (!notes.TryGetValue(entry, out ManiaHitObject? note)) return;
		double nextHead = beatmap.HitObjects.OfType<ManiaHitObject>().Where(n => n != note && n.Column == entry.Column && n.StartTime > entry.Start)
			.Select(n => n.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min();
		double limit = nextHead;
		if (double.IsFinite(nextHead)) limit = Grid.Next(beatmap.ControlPointInfo, divisor.Value, nextHead, -1);
		double duration = Math.Max(0, Math.Min(entry.End, limit) - entry.Start);
		if (note is HoldNote hold && duration > 0) { hold.Duration = duration; Refresh(hold); return; }
		if (note is Note && duration == 0) return;
		ManiaHitObject replacement = new Note();
		if (duration > 0) replacement = new HoldNote { Duration = duration };
		replacement.StartTime = note.StartTime; replacement.Column = note.Column; replacement.Samples = note.Samples;
		beatmap.Remove(note); Refresh(replacement); beatmap.Add(replacement); notes[entry] = replacement;
	}
	private void Refresh(HitObject note) { note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty); beatmap.Update(note); }
	private void Finish()
	{
		model?.Clear(); active.Clear(); taikoGesture.Clear(); taikoNote = null;
		if (started) EndChange();
		model = null;
	}
	private void TogglePlayback(bool stay)
	{
		Finish();
		if (clock.IsRunning)
		{
			clock.Stop();
			if (ReturnOnPause.Value && !stay) clock.Seek(returnPosition);
		}
		else { returnPosition = clock.CurrentTimeAccurate; clock.Start(); }
		wasRunning = clock.IsRunning;
		lastTime = clock.CurrentTimeAccurate;
	}
	protected override void Update()
	{
		base.Update();
		if (!started) return;
		if (!Ready || (input?.FocusedDrawable != null && input.FocusedDrawable != this)) Finish();
		if (clock.IsRunning && !wasRunning) { Finish(); returnPosition = lastTime; }
		if (wasRunning && !clock.IsRunning && !isCatch)
		{
			if (ReturnOnPause.Value) clock.Seek(returnPosition);
		}
		// External seeks (timeline/wheel) become the new return point.
		if (clock.IsRunning && clock.IsSeeking) returnPosition = clock.CurrentTimeAccurate;
		if (!clock.IsRunning && model != null && Math.Abs(clock.CurrentTimeAccurate - model.Position) > 1) Finish();
		wasRunning = clock.IsRunning;
		lastTime = clock.CurrentTimeAccurate;
	}
	protected override void Dispose(bool isDisposing)
	{
		Finish(); subscription?.Dispose();
		base.Dispose(isDisposing);
	}
}
