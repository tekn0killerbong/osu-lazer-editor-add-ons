using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Catch.Edit.Blueprints;
using osu.Game.Rulesets.Catch.Edit.Blueprints.Components;
using HarmonyLib;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.EditorAddons;

/// <summary>Modifier gestures select objects even while a placement tool is active.</summary>
internal partial class ModifierSelection : CompositeDrawable
{
	[Resolved] private EditorBeatmap map { get; set; } = null!;
	private readonly HitObjectComposer composer;
	private readonly DragBox box = new();
	private InputManager input = null!;
	private bool active;
	private bool dragging;
	private bool range;
	private bool applying;
	private HitObject? clicked;
	private HashSet<HitObject> before = new();
	private HashSet<HitObject> anchor = new();
	private HashSet<HitObject> target = new();
	private MouseButtonEvent? dragEvent;

	internal ModifierSelection(HitObjectComposer composer)
	{
		this.composer = composer;
		RelativeSizeAxes = Axes.Both;
		Depth = -900;
		InternalChild = box;
	}
	protected override void LoadComplete()
	{
		base.LoadComplete();
		input = GetContainingInputManager();
		map.SelectedHitObjects.CollectionChanged += SelectionChanged;
	}
	private void SelectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		if (applying || active) return;
		anchor = map.SelectedHitObjects.ToHashSet();
	}
	public override bool ReceivePositionalInputAt(Vector2 position)
	{
		if (active) return true;
		if (input == null || !composer.CursorInPlacementArea) return false;
		if (composer.BlueprintContainer.CurrentTool is SelectTool)
		{
			// Keep native vertex editing on an already selected Catch path.
			foreach (JuiceStreamSelectionBlueprint stream in composer.BlueprintContainer.SelectionBlueprints.AliveChildren.OfType<JuiceStreamSelectionBlueprint>().Where(b => b.IsSelected))
			{
				SelectionEditablePath path = (SelectionEditablePath)AccessTools.Field(typeof(JuiceStreamSelectionBlueprint), "editablePath").GetValue(stream)!;
				if (path.ReceivePositionalInputAt(position)) return false;
			}
		}
		return input.CurrentState.Keyboard.ControlPressed || input.CurrentState.Keyboard.ShiftPressed;
	}
	protected override bool OnMouseDown(MouseDownEvent e)
	{
		if (e.Button != MouseButton.Left || (!e.ControlPressed && !e.ShiftPressed)) return false;
		active = true; dragging = false; range = e.ShiftPressed;
		before = map.SelectedHitObjects.ToHashSet();
		anchor.IntersectWith(map.HitObjects);
		if (anchor.Count == 0) anchor = before.ToHashSet();
		clicked = composer.BlueprintContainer.SelectionBlueprints.AliveChildren
			.Where(b => b.IsSelectable && b.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
			.OrderByDescending(b => b.IsSelected)
			.ThenBy(b => Vector2.DistanceSquared(b.ScreenSpaceSelectionPoint, e.ScreenSpaceMousePosition))
			.Select(b => b.Item).FirstOrDefault();
		target.Clear();
		// Changing tools normally commits an unfinished placement. A selection
		// gesture must cancel it instead, never create an accidental object.
		composer.BlueprintContainer.CurrentPlacement?.EndPlacement(false);
		HarmonyLib.AccessTools.Method(composer.GetType(), "SetSelectTool").Invoke(composer, null);
		return true;
	}
	protected override bool OnDragStart(DragStartEvent e)
	{
		if (!active || e.Button != MouseButton.Left) return false;
		dragging = true;
		box.Show(); UpdateBox(e);
		return true;
	}
	protected override void OnDrag(DragEvent e) { if (active) UpdateBox(e); }
	private void UpdateBox(MouseButtonEvent e)
	{
		dragEvent = e;
		e.Target = this;
		box.HandleDrag(e);
		var quad = box.Box.ScreenSpaceDrawQuad;
		target = composer.BlueprintContainer.SelectionBlueprints.AliveChildren
			.Where(b => b.IsSelectable && quad.Contains(b.ScreenSpaceSelectionPoint)).Select(b => b.Item).ToHashSet();
		Apply(false);
	}
	private void Apply(bool toggle)
	{
		HashSet<HitObject> result = before.ToHashSet();
		if (range && target.Count > 0 && anchor.Count > 0)
		{
			double start = Math.Min(anchor.Min(h => h.StartTime), target.Min(h => h.StartTime));
			double end = Math.Max(anchor.Max(h => h.StartTime), target.Max(h => h.StartTime));
			result.UnionWith(map.HitObjects.Where(h => h.StartTime >= start && h.StartTime <= end));
		}
		else if (toggle)
		{
			foreach (HitObject item in target)
				if (!result.Remove(item)) result.Add(item);
		}
		else result.UnionWith(target);
		applying = true;
		try
		{
			foreach (HitObject item in map.SelectedHitObjects.Except(result).ToArray()) map.SelectedHitObjects.Remove(item);
			map.SelectedHitObjects.AddRange(result.Except(map.SelectedHitObjects).ToArray());
		}
		finally { applying = false; }
	}
	protected override void OnMouseUp(MouseUpEvent e)
	{
		if (!active || e.Button != MouseButton.Left) return;
		if (!dragging)
		{
			if (clicked != null) target.Add(clicked);
			Apply(!range);
		}
		if (!range && target.Count > 0) anchor = target.ToHashSet();
		else if (anchor.Count == 0) anchor = target.ToHashSet();
		active = false; dragEvent = null; box.Hide();
	}
	protected override bool OnClick(ClickEvent e) => e.Button == MouseButton.Left;
	protected override void OnDragEnd(DragEndEvent e) { dragEvent = null; box.Hide(); }
	protected override void Update()
	{
		base.Update();
		if (active && dragging && dragEvent != null) UpdateBox(dragEvent);
	}
	protected override void Dispose(bool isDisposing)
	{
		if (map != null) map.SelectedHitObjects.CollectionChanged -= SelectionChanged;
		base.Dispose(isDisposing);
	}
}
