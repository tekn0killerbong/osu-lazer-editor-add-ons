using HarmonyLib;
using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Catch.Edit.Blueprints;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.EditorAddons;

internal static class CatchMenus
{
	internal static void Install(Harmony harmony)
	{
		harmony.Patch(AccessTools.PropertyGetter(typeof(JuiceStreamSelectionBlueprint), nameof(JuiceStreamSelectionBlueprint.ContextMenuItems)), postfix: new HarmonyMethod(typeof(CatchMenus), nameof(StreamMenu)));
	}
	private static void StreamMenu(JuiceStreamSelectionBlueprint __instance, ref MenuItem[] __result)
	{
		EditorBeatmap? map = (EditorBeatmap?)AccessTools.Property(typeof(JuiceStreamSelectionBlueprint), "editorBeatmap").GetValue(__instance);
		if (map == null) return;
		JuiceStream stream = (JuiceStream)__instance.Item;
		CurveTracking? book = CurveTracking.For(map);
		__result = __result.Append(new OsuMenuItem("Stick to curve (automatic)", MenuItemType.Standard, () => Notify(Bake(map, stream)))).ToArray();
		if (book?.IsTracked(stream) == true)
			__result = __result.Append(new OsuMenuItem("Restore original curve", MenuItemType.Standard, () => Notify(book.RestoreOriginal(stream)))).ToArray();
		else if (book != null)
			__result = __result.Append(new OsuMenuItem("Track current droplet positions", MenuItemType.Standard, () => Notify(book.TrackCurrentPositions(stream)))).ToArray();
		void Notify(string message) => __instance.Dependencies.Get<INotificationOverlay>()?.Post(new SimpleNotification { Text = message });
	}
	internal static string Bake(EditorBeatmap map, JuiceStream stream)
	{
		CurveTracking? book = CurveTracking.For(map);
		if (book == null) return "Curve tracking is not ready. Reopen the editor before aligning this stream.";
		return book.Bake(stream);
	}
}
