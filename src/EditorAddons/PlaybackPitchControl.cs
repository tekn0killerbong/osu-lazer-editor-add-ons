using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Rulesets.EditorAddons
{
	/// <summary>Playback header shared by all editors.</summary>
	public partial class PlaybackPitchControl : FillFlowContainer, IHasTooltip
	{
		public const float PlaybackColumnWidth = 330;
		public readonly BindableBool Pitched = new BindableBool(true);
		private static readonly ConditionalWeakTable<Storage, PitchConfig> preferences = new ConditionalWeakTable<Storage, PitchConfig>();
		public LocalisableString TooltipText => "Change audio pitch with playback speed in the editor and Test mode. Below 5%, pitch also decreases when this is off.";

		public PlaybackPitchControl()
		{
			AutoSizeAxes = Axes.Both;
			Direction = FillDirection.Horizontal;
			Spacing = new Vector2(5, 0);
			Children = new Drawable[]
			{
				new OsuSpriteText { Text = EditorStrings.PlaybackSpeed, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
				new OsuSpriteText { Text = "|", Alpha = 0.5f, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
				new OsuSpriteText { Text = "Pitched", Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
				new SwitchButton
				{
					Current = Pitched,
					Size = new Vector2(32, 14),
					Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft
				}
			};
		}

		[BackgroundDependencyLoader]
		private void load(Storage storage)
		{
			// One preference per client storage, shared by all beatmaps/rulesets.
			// Keep it alive beyond an editor's disposal, including difficulty switches.
			Pitched.BindTo(preferences.GetValue(storage, s => new PitchConfig(s)).Value);
		}

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing) Pitched.UnbindAll();
			base.Dispose(isDisposing);
		}

		private enum PitchSetting { Pitched }

		private sealed class PitchConfig : IniConfigManager<PitchSetting>
		{
			protected override string Filename => "editor-tools-playback.ini";
			public readonly Bindable<bool> Value;

			public PitchConfig(Storage storage) : base(storage)
			{
				Value = GetBindable<bool>(PitchSetting.Pitched);
				if (!storage.Exists(Filename) && storage.Exists("shred-editor-playback.ini"))
				{
					using System.IO.StreamReader previous = new(storage.GetStream("shred-editor-playback.ini"));
					foreach (string line in previous.ReadToEnd().Split('\n'))
					{
						string[] pair = line.Split('=', 2);
						if (pair.Length == 2 && pair[0].Trim() == "Pitched" && bool.TryParse(pair[1].Trim(), out bool pitched)) Value.Value = pitched;
					}
					Save();
				}
				// A rare toggle writes immediately, even if the client closes next.
				Value.BindValueChanged(_ => Save());
			}

			protected override void InitialiseDefaults() => SetDefault(PitchSetting.Pitched, true);
		}
	}
}
