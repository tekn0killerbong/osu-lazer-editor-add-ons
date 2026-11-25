using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.EditorAddons;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Input;

internal partial class EditorProbe
{
	private bool playbackOnly;
	private static bool CloseRate(double a, double b) => Math.Abs(a - b) < 0.001;
	private IEnumerator<Func<bool>> PlaybackSteps()
	{
		yield return () => ScreenStack?.CurrentScreen is MainMenu;
		Task import = BeatmapManager.Import(new[] { new ImportTask(File.OpenRead(Fixture()), "playback-tests.osz") });
		yield return () => import.IsCompleted; import.GetAwaiter().GetResult();
		bool rememberedPitch = false;
		foreach (string mode in new[] { "mania", "taiko", "fruits", "osu" })
		{
			stage = mode + " playback load";
			BeatmapInfo? info = null;
			yield return () => (info = BeatmapManager.QueryBeatmap(b => b.DifficultyName == "tools-" + mode)) != null;
			Beatmap.Value = BeatmapManager.GetWorkingBeatmap(info!, refetch: true); Ruleset.Value = info!.Ruleset;
			ScreenStack.Push(new EditorLoader());
			yield return () => ScreenStack.CurrentScreen is Editor e && e.ReadyForUse && e.ChildrenOfType<PlaybackRateControl>().Any(c => c.IsLoaded);
			Editor editor = (Editor)ScreenStack.CurrentScreen;
			EditorClock editorClock = editor.Dependencies.Get<EditorClock>();
			PlaybackRateControl control = editor.ChildrenOfType<PlaybackRateControl>().Single();
			PlaybackPitchControl header = editor.ChildrenOfType<PlaybackPitchControl>().Single();
			RoundedSliderBar<double> slider = control.ChildrenOfType<RoundedSliderBar<double>>().Single();
			OsuSpriteText percentage = control.ChildrenOfType<OsuSpriteText>().Single();
			yield return () => slider.DrawWidth > 50 && header.DrawWidth > 0;
			Check(CloseRate(control.Current.Value, 1) && CloseRate(control.Current.Default, 1), mode + " editor opens at 100 percent");
			Check(header.Pitched.Value == rememberedPitch, mode + " pitch preference migrates from Shred and persists between editors");
			Check(editor.ChildrenOfType<PlaybackControl.PlaybackTabControl>().All(t => !t.IsPresent), mode + " native percentage tabs replaced by one slider");
			Check(percentage.ScreenSpaceDrawQuad.TopLeft.X > slider.ScreenSpaceDrawQuad.BottomRight.X && header.ScreenSpaceDrawQuad.BottomRight.Y <= control.ScreenSpaceDrawQuad.TopLeft.Y + 1, mode + " slider and numeric value fit below the pitch header");
			Mouse(slider.ToScreenSpace(new Vector2(0, slider.DrawHeight / 2))); Button(true); Button(false); yield return () => true;
			Check(CloseRate(control.Current.Value, 0.01) && percentage.Text.ToString() == "1%", mode + " actual mouse click reaches 1 percent");
			Mouse(slider.ToScreenSpace(new Vector2(slider.DrawWidth - 1, slider.DrawHeight / 2))); Button(true); Button(false); yield return () => true;
			Check(CloseRate(control.Current.Value, 2) && percentage.Text.ToString() == "200%", mode + " actual mouse click reaches 200 percent");
			Tap(Key.Left); yield return () => true;
			Check(CloseRate(control.Current.Value, 1.99), mode + " focused slider adjusts by one percent with keyboard");
			Mouse(slider.ToScreenSpace(new Vector2(0, slider.DrawHeight / 2))); Button(true); yield return () => true;
			Mouse(slider.ToScreenSpace(new Vector2(slider.DrawWidth * 0.6f, slider.DrawHeight / 2))); yield return () => true;
			Button(false); yield return () => true;
			Check(control.Current.Value > 1 && control.Current.Value < 1.7, mode + " mouse drag changes playback value");
			foreach (bool pitched in new[] { true, false })
			foreach (double rate in new[] { 0.01, 0.5, 1.37, 2 })
			{
				stage = mode + " Test rate " + rate + " pitch " + pitched;
				editorClock.Stop(); editorClock.Seek(1000);
				header.Pitched.Value = pitched; control.Current.Value = rate;
				PlaybackRateControl.GetAudioRate(rate, pitched, out double tempo, out double frequency);
				Check(CloseRate(editorClock.AudioAdjustments.AggregateTempo.Value, tempo) && CloseRate(editorClock.AudioAdjustments.AggregateFrequency.Value, frequency), stage + " editor audio factors");
				editorClock.Start(); yield return () => CloseRate(((IClock)editorClock).Rate, rate);
				Check(CloseRate(Beatmap.Value.Track.AggregateTempo.Value, tempo) && CloseRate(Beatmap.Value.Track.AggregateFrequency.Value, frequency), stage + " editor track factors");
				editorClock.Stop(); editor.TestGameplay();
				yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p.IsLoaded;
				EditorPlayer player = (EditorPlayer)ScreenStack.CurrentScreen;
				GameplayClockContainer clock = player.ChildrenOfType<GameplayClockContainer>().Single();
				yield return () => !clock.IsPaused.Value && CloseRate(((IClock)clock).Rate, rate);
				Check(CloseRate(clock.AdjustmentsFromMods.AggregateTempo.Value, tempo) && CloseRate(clock.AdjustmentsFromMods.AggregateFrequency.Value, frequency), stage + " Test audio factors");
				Check(CloseRate(Beatmap.Value.Track.AggregateTempo.Value, tempo) && CloseRate(Beatmap.Value.Track.AggregateFrequency.Value, frequency), stage + " Test track factors");
				if (rate == 0.5)
				{
					clock.Stop(); yield return () => clock.IsPaused.Value;
					clock.Start(); yield return () => !clock.IsPaused.Value;
					Check(CloseRate(((IClock)clock).Rate, rate), stage + " quick pause resume retains speed");
					Check(player.Restart(true), stage + " retry accepted");
					yield return () => ScreenStack.CurrentScreen is EditorPlayer p && p != player && p.IsLoaded;
					Check(CloseRate(clock.AdjustmentsFromMods.AggregateTempo.Value, 1) && CloseRate(clock.AdjustmentsFromMods.AggregateFrequency.Value, 1), stage + " old Test factors released on retry");
					player = (EditorPlayer)ScreenStack.CurrentScreen; clock = player.ChildrenOfType<GameplayClockContainer>().Single();
					yield return () => !clock.IsPaused.Value && CloseRate(((IClock)clock).Rate, rate);
					Check(CloseRate(clock.AdjustmentsFromMods.AggregateTempo.Value, tempo) && CloseRate(clock.AdjustmentsFromMods.AggregateFrequency.Value, frequency), stage + " retried Test keeps chosen speed and pitch");
				}
				player.Exit(); yield return () => ScreenStack.CurrentScreen == editor;
				Check(CloseRate(clock.AdjustmentsFromMods.AggregateTempo.Value, 1) && CloseRate(clock.AdjustmentsFromMods.AggregateFrequency.Value, 1), stage + " exit releases Test factors");
				Check(CloseRate(control.Current.Value, rate) && header.Pitched.Value == pitched, stage + " return preserves editor controls");
				editorClock.Start(); yield return () => CloseRate(((IClock)editorClock).Rate, rate);
				Check(CloseRate(((IClock)editorClock).Rate, rate), stage + " returning editor rate is not multiplied"); editorClock.Stop();
			}
			stage = mode + " playback timing and normal game";
			control.Current.Value = 0.5; editorClock.Start();
			foreach (bool pitched in new[] { true, false })
			{
				header.Pitched.Value = pitched; yield return () => true;
				Check(CloseRate(((IClock)editorClock).Rate, 0.5), mode + " live pitch toggle preserves speed");
			}
			editorClock.Stop(); control.Current.Value = 1.37;
			editor.Mode.Value = EditorScreenMode.Timing; yield return () => control.Current.Disabled;
			Check(CloseRate(control.Current.Value, 1.37), mode + " Timing mode remembers chosen speed");
			Mouse(slider.ScreenSpaceDrawQuad.Centre); Button(true); Button(false); yield return () => true;
			Check(CloseRate(control.Current.Value, 1.37), mode + " Timing slider ignores edits while disabled");
			editor.Mode.Value = EditorScreenMode.Compose; yield return () => !control.Current.Disabled;
			control.Current.Value = 0.5; rememberedPitch = !rememberedPitch; header.Pitched.Value = rememberedPitch;
			editor.Exit(); yield return () => ScreenStack.CurrentScreen is MainMenu;
			ScreenStack.Push(new PlayerLoader(() => new SoloPlayer(new PlayerConfiguration { ShowResults = false })));
			yield return () => ScreenStack.CurrentScreen is Player p && p.IsLoaded;
			Player normal = (Player)ScreenStack.CurrentScreen;
			GameplayClockContainer normalClock = normal.ChildrenOfType<GameplayClockContainer>().Single();
			Check(CloseRate(normalClock.AdjustmentsFromMods.AggregateTempo.Value, 1) && CloseRate(normalClock.AdjustmentsFromMods.AggregateFrequency.Value, 1), mode + " normal gameplay has no editor rate");
			Check(CloseRate(Beatmap.Value.Track.AggregateTempo.Value, 1) && CloseRate(Beatmap.Value.Track.AggregateFrequency.Value, 1), mode + " normal gameplay audio is clean");
			normal.Exit(); yield return () => ScreenStack.CurrentScreen is MainMenu;
		}
		Check(File.ReadAllText(Path.Combine(folder, "editor-tools-playback.ini")).Contains("Pitched"), "pitch preference saved independently of Shred");
	}
}
