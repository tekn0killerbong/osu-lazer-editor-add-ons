using System.ComponentModel;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Taiko;

namespace osu.Game.Rulesets.EditorAddons;

public enum EditorAction
{
	[Description("Toggle step recorder")] ToggleRecorder = 186000,
	[Description("Empty step")] EmptyStep,
	[Description("Decrease grid spacing")] DecreaseSpacing,
	[Description("Increase grid spacing")] IncreaseSpacing,
	[Description("Decrease grid type")] DecreaseType,
	[Description("Increase grid type")] IncreaseType
}

internal static class Bindings
{
	public static bool IsEditorAction(int action) => Enum.IsDefined(typeof(EditorAction), action);
	public static IEnumerable<KeyBinding> Defaults(Ruleset ruleset)
	{
		Type actionType = typeof(ManiaAction);
		if (ruleset is TaikoRuleset) actionType = typeof(TaikoAction);
		if (ruleset is CatchRuleset) actionType = typeof(CatchAction);
		List<KeyBinding> result = new();
		void Add(EditorAction action, params InputKey[] keys)
		{
			object typed = Enum.ToObject(actionType, (int)action);
			result.Add(new KeyBinding(keys, typed));
			result.Add(new KeyBinding(InputKey.None, typed));
		}
		if (ruleset is not CatchRuleset)
		{
			Add(EditorAction.ToggleRecorder, InputKey.Control, InputKey.Q);
			// Physical Slash is é on the user's Canadian French layout.
			Add(EditorAction.EmptyStep, InputKey.Slash);
			Add(EditorAction.DecreaseSpacing, InputKey.Number7);
			Add(EditorAction.IncreaseSpacing, InputKey.Number8);
			Add(EditorAction.DecreaseType, InputKey.Number9);
			Add(EditorAction.IncreaseType, InputKey.Number0);
		}
		else
		{
			Add(EditorAction.DecreaseSpacing, InputKey.S);
			Add(EditorAction.IncreaseSpacing, InputKey.A);
			Add(EditorAction.DecreaseType, InputKey.D);
			Add(EditorAction.IncreaseType, InputKey.F);
		}
		return result;
	}
}
