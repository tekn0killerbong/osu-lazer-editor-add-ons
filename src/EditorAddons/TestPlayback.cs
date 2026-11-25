using System;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.GameplayTest;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.EditorAddons
{
	// Installed once when an editor receives our playback controls.
	// Work only at screen transitions: no per-frame editor or hit-object traversal.
	internal sealed class TestPlayback
	{
		private static readonly ConditionalWeakTable<OsuGame, TestPlayback> services = new ConditionalWeakTable<OsuGame, TestPlayback>();
		private readonly ConditionalWeakTable<EditorPlayerLoader, PlaybackRate> loaderRates = new ConditionalWeakTable<EditorPlayerLoader, PlaybackRate>();
		private readonly ConditionalWeakTable<EditorPlayer, AppliedRate> playerRates = new ConditionalWeakTable<EditorPlayer, AppliedRate>();

		internal static void OnEditorLoaded(Editor __instance)
		{
			try
			{
				if (EditorPlayback.Install(__instance))
					services.GetValue(__instance.Dependencies.Get<OsuGame>(), game => new TestPlayback(game));
			}
			catch (Exception error) { Logger.Error(error, "EditorAddons could not install playback controls."); }
		}

		private TestPlayback(OsuGame game)
		{
			game.ScreenStack.ScreenPushed += screenPushed;
			game.ScreenStack.ScreenExited += screenExited;
		}

		private void screenPushed(IScreen previous, IScreen next)
		{
			try
			{
				if (previous is Editor editor && next is EditorPlayerLoader loader && editor.ChildrenOfType<EditorPlayback>().Any())
				{
					// The shared audio track has already been unbound during suspension.
					// Read the editor's own adjustment instead of the track's current rate.
					AudioAdjustments adjustments = editor.Dependencies.Get<EditorClock>().AudioAdjustments;
					loaderRates.Add(loader, new PlaybackRate(adjustments.AggregateTempo.Value, adjustments.AggregateFrequency.Value));
				}
				else if (previous is EditorPlayerLoader source && next is EditorPlayer player && loaderRates.TryGetValue(source, out PlaybackRate? rate))
				{
					// ScreenStack invokes this before OnEntering starts the player clock.
					player.OnLoadComplete += _ => apply(player, rate);
				}
			}
			catch (Exception error)
			{
				Logger.Log($"EditorAddons test playback: {error.Message}", LoggingTarget.Runtime, LogLevel.Important);
			}
		}

		private void apply(EditorPlayer player, PlaybackRate rate)
		{
			if (!player.LoadedBeatmapSuccessfully || !player.ValidForResume)
				return;

			GameplayClockContainer? clock = player.ChildrenOfType<GameplayClockContainer>().FirstOrDefault();
			if (clock == null || playerRates.TryGetValue(player, out _))
				return;

			AppliedRate applied = new AppliedRate(clock, rate);
			clock.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Tempo, applied.Tempo);
			clock.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, applied.Frequency);
			playerRates.Add(player, applied);
		}

		private void screenExited(IScreen previous, IScreen next)
		{
			if (previous is EditorPlayer player && playerRates.TryGetValue(player, out AppliedRate? applied))
			{
				// Release before the editor rebinds the shared track, not at delayed disposal.
				applied.Clock.AdjustmentsFromMods.RemoveAdjustment(AdjustableProperty.Tempo, applied.Tempo);
				applied.Clock.AdjustmentsFromMods.RemoveAdjustment(AdjustableProperty.Frequency, applied.Frequency);
				playerRates.Remove(player);
			}
		}

		private sealed class AppliedRate
		{
			public readonly GameplayClockContainer Clock;
			public readonly BindableDouble Tempo;
			public readonly BindableDouble Frequency;
			public AppliedRate(GameplayClockContainer clock, PlaybackRate rate)
			{
				Clock = clock;
				Tempo = new BindableDouble(rate.Tempo);
				Frequency = new BindableDouble(rate.Frequency);
			}
		}

		private sealed class PlaybackRate
		{
			public readonly double Tempo;
			public readonly double Frequency;
			public PlaybackRate(double tempo, double frequency)
			{
				Tempo = validRate(tempo);
				Frequency = validRate(frequency);
			}
			private static double validRate(double value)
			{
				if (double.IsFinite(value) && value > 0) return value;
				return 1;
			}
		}
	}
}
