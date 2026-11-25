using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Rulesets.EditorAddons
{
	/// <summary>Session-only playback speed for the optional extension.</summary>
	public partial class PlaybackRateControl : Container
	{
		public readonly BindableDouble Current = new BindableDouble(1)
		{
			MinValue = 0.01,
			MaxValue = 2,
			Precision = 0.01,
		};

		public static void GetAudioRate(double rate, bool pitched, out double tempo, out double frequency)
		{
			if (!double.IsFinite(rate) || rate <= 0) rate = 1;
			tempo = 1;
			frequency = rate;
			if (pitched) return;
			// TrackBass rejects tempos below 5%. Keep the requested total speed
			// there using frequency for the remaining reduction, rather than crashing.
			// Leave a small margin for the audio backend's floating-point boundary.
			tempo = Math.Max(0.050001, rate);
			frequency = rate / tempo;
		}

		private partial class PlaybackSlider : RoundedSliderBar<double>
		{
			protected override LocalisableString GetTooltipText(double value) => default;
		}

		public PlaybackRateControl()
		{
			RelativeSizeAxes = Axes.X;
			Height = 20;
			OsuSpriteText percentage;
			Children = new Drawable[]
			{
				new Container
				{
					RelativeSizeAxes = Axes.Both,
					Padding = new MarginPadding { Right = 48 },
					Child = new PlaybackSlider
					{
						Current = Current,
						RelativeSizeAxes = Axes.X,
						Anchor = Anchor.CentreLeft,
						Origin = Anchor.CentreLeft,
						KeyboardStep = 0.01f,
						DisplayAsPercentage = true,
					},
				},
				percentage = new OsuSpriteText
				{
					Anchor = Anchor.CentreRight,
					Origin = Anchor.CentreRight,
					Font = OsuFont.GetFont(size: 14),
				},
			};
			Current.BindValueChanged(change => percentage.Text = $"{change.NewValue * 100:0}%", true);
		}
	}
}
