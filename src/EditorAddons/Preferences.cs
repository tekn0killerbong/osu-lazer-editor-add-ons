using System.Runtime.CompilerServices;
using System.Text.Json;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.EditorAddons;

internal static class Preferences
{
	private sealed class Values
	{
		public bool Enabled { get; set; }
		public bool ReturnOnPause { get; set; }
	}
	private sealed class Store
	{
		public Dictionary<string, Values> Modes = new();
	}
	private static readonly ConditionalWeakTable<Storage, Store> stores = new();
	public static void Bind(Storage storage, string mode, BindableBool enabled, BindableBool returnOnPause)
	{
		Store store = stores.GetValue(storage, s =>
		{
			Store result = new();
			try
			{
				if (s.Exists("editor-tools.json"))
				{
					using Stream input = s.GetStream("editor-tools.json");
					result.Modes = JsonSerializer.Deserialize<Dictionary<string, Values>>(input) ?? new();
				}
			}
			catch (Exception error) { Logger.Error(error, "EditorAddons preferences could not be read; using defaults."); }
			return result;
		});
		if (!store.Modes.TryGetValue(mode, out Values? values)) store.Modes[mode] = values = new();
		enabled.Value = values.Enabled; returnOnPause.Value = values.ReturnOnPause;
		void Save()
		{
			values.Enabled = enabled.Value; values.ReturnOnPause = returnOnPause.Value;
			try { using Stream output = storage.GetStream("editor-tools.json", FileAccess.Write, FileMode.Create); JsonSerializer.Serialize(output, store.Modes); }
			catch (Exception error) { Logger.Error(error, "EditorAddons preferences could not be saved."); }
		}
		enabled.BindValueChanged(_ => Save()); returnOnPause.BindValueChanged(_ => Save());
	}
}
