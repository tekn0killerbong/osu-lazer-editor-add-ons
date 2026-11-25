using osu.Game.Rulesets.EditorAddons;

if (args.Contains("--playback")) EditorProbe.Run(true);
else if (args.Contains("--curve")) CurveBakeProbe.Run();
else if (args.Contains("--editor")) EditorProbe.Run();
else
{
	_ = new EditorAddonsRuleset();
	UnitProbe.Run(args);
}
