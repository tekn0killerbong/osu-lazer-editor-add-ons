using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Rulesets.EditorAddons;

public partial class Footprints : Container
{
	public Footprints()
	{
		Size = new Vector2(26);
		for (int side = 0; side < 2; side++)
		{
			float rotation = -15;
			if (side == 1) rotation = 15;
			Container foot = new() { Position = new Vector2(4 + side * 11, 3 + side * 5), Size = new Vector2(6, 16), Rotation = rotation };
			foot.Add(new Circle { Size = new Vector2(6, 10) });
			foot.Add(new Circle { Position = new Vector2(1, 12), Size = new Vector2(4) });
			Add(foot);
		}
	}
}

internal partial class RecorderControls : FillFlowContainer
{
	public RecorderControls(BindableBool enabled, BindableBool returnOnPause)
	{
		RelativeSizeAxes = Axes.X;
		AutoSizeAxes = Axes.Y;
		Direction = FillDirection.Vertical;
		Children = new Drawable[]
		{
			new Container
			{
				RelativeSizeAxes = Axes.X, Height = 44, Padding = new MarginPadding { Left = 8, Right = 8 },
				Children = new Drawable[]
				{
					new OsuCheckbox { RelativeSizeAxes = Axes.X, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, LabelText = "", Current = enabled },
					new SpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 34, Text = "Step recorder", Font = FontUsage.Default.With(size: 18) },
					new Footprints { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft }
				}
			},
			new Container
			{
				RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Padding = new MarginPadding { Left = 42, Right = 8, Bottom = 8 },
				Child = new OsuCheckbox { RelativeSizeAxes = Axes.X, LabelText = "Return on pause", Current = returnOnPause }
			}
		};
	}
}
