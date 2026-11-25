using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Edit;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.EditorAddons;

internal partial class RecorderManiaComposer : ManiaHitObjectComposer, IRecorderComposer
{
	public RecorderController? Recorder { get; private set; }
	public BeatSnapGrid? RecorderGrid { get; private set; }
	protected override BeatSnapGrid CreateBeatSnapGrid() => RecorderGrid = new ManiaBeatSnapGrid();
	public RecorderManiaComposer(Ruleset ruleset) : base(ruleset) { }
	protected override void LoadComplete()
	{
		base.LoadComplete();
		AddInternal(new ModifierSelection(this));
		RecorderController controller = new(this);
		Recorder = controller;
		AddInternal(controller);
		AddControls(LeftToolbox, controller);
	}
	internal static void AddControls(ExpandingToolboxContainer toolbox, RecorderController controller)
	{
		RecorderControls controls = new(controller.Enabled, controller.ReturnOnPause);
		toolbox.Add(controls);
		if (controls.Parent is FillFlowContainer flow) flow.SetLayoutPosition(controls, float.MinValue);
	}
}

internal partial class RecorderTaikoComposer : TaikoHitObjectComposer, IRecorderComposer
{
	public RecorderController? Recorder { get; private set; }
	public BeatSnapGrid? RecorderGrid { get; private set; }
	protected override BeatSnapGrid CreateBeatSnapGrid() => RecorderGrid = new TaikoBeatSnapGrid();
	public RecorderTaikoComposer(Ruleset ruleset) : base((TaikoRuleset)ruleset) { }
	protected override void LoadComplete()
	{
		base.LoadComplete();
		AddInternal(new ModifierSelection(this));
		RecorderController controller = new(this);
		Recorder = controller;
		AddInternal(controller);
		RecorderManiaComposer.AddControls(LeftToolbox, controller);
	}
}
