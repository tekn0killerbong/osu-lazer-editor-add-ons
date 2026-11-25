using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.EditorAddons;

// The only public, non-nested Ruleset subclass in the assembly. The stock loader
// discovers it; it is removed from the available mode list after installation.
public sealed class EditorAddonsRuleset : Ruleset
{
	// Keep the stored carrier identity stable across the EditorTools rename.
	public const string Id = "editor-tools";
	static EditorAddonsRuleset()
	{
		AssemblyLoadContext.Default.Resolving += ResolveEmbedded;
		try { Install(); }
		catch (Exception error) { Logger.Error(error, "EditorAddons could not start. The original editors remain available."); }
	}

	private static Assembly? ResolveEmbedded(AssemblyLoadContext context, AssemblyName name)
	{
		if (name.Name != "0Harmony") return null;
		using Stream? stream = typeof(EditorAddonsRuleset).Assembly.GetManifestResourceStream("EditorAddons.0Harmony.dll");
		if (stream == null) return null;
		return context.LoadFromStream(stream);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Install() => Integration.Install();
	public override string Description => "EditorAddons";
	public override string ShortName => Id;
	public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
	public override IEnumerable<int> GameplayVariants => Array.Empty<int>();
	public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();
	public override Drawable CreateIcon() => new Footprints();
	// Required by lazer's discovery compatibility check. This carrier has no
	// selectable gameplay mode once integration has successfully installed.
	public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new ManiaRuleset().CreateBeatmapConverter(beatmap);
	public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new ManiaRuleset().CreateDifficultyCalculator(beatmap);
	public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
		=> new ManiaRuleset().CreateDrawableRulesetWith(beatmap, mods);
}
