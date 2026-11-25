using osu.Framework.Graphics;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Edit;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Screens.Edit;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Rulesets.EditorAddons;

internal partial class EnhancedCatchComposer : CatchHitObjectComposer
{
	public EnhancedCatchComposer(Ruleset ruleset) : base((CatchRuleset)ruleset) { }
	protected override IReadOnlyList<CompositionTool<CatchAction>> CompositionTools => new CompositionTool<CatchAction>[]
	{
		new FruitCompositionTool(), new FreehandJuiceTool(), new BananaShowerCompositionTool()
	};
	protected override void LoadComplete()
	{
		base.LoadComplete();
		AddInternal(new ModifierSelection(this));
		AddInternal(new RecorderController(this));
		CurveTracking.Attach(Dependencies.Get<EditorBeatmap>(), Dependencies.Get<Storage>(),
			message => Dependencies.Get<INotificationOverlay>()?.Post(new SimpleNotification { Text = message }));
	}
}

internal class FreehandJuiceTool : JuiceStreamCompositionTool
{
	public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new FreehandJuicePlacement();
}
