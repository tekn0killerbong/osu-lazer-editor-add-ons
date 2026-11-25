namespace osu.Game.Rulesets.EditorAddons;

// Pure gesture state, independent of frame rate, input hardware, and UI.
public sealed class RecorderModel
{
	public sealed class Entry
	{
		public int Column { get; init; }
		public double Start { get; init; }
		public double End { get; set; }
		public bool Extended { get; set; }
	}
	private readonly Dictionary<int, Entry> held = new();
	private readonly Func<double, int, double> next;
	private readonly Action<Entry> add;
	private readonly Action<Entry> change;
	private readonly Action<double> seek;
	public double Position { get; private set; }
	public int HeldCount => held.Count;
	public RecorderModel(double position, Func<double, int, double> next, Action<Entry> add, Action<Entry> change, Action<double> seek)
	{
		Position = position; this.next = next; this.add = add; this.change = change; this.seek = seek;
	}
	public void Press(int column)
	{
		if (held.ContainsKey(column)) return;
		Entry entry = new() { Column = column, Start = Position, End = Position };
		held.Add(column, entry);
		add(entry);
	}
	public void Release(int column)
	{
		if (!held.Remove(column, out Entry? released)) return;
		if (!released.Extended && !held.Values.Any(e => !e.Extended)) Move(1);
	}
	public void Move(int direction)
	{
		Position = next(Position, direction);
		foreach (Entry entry in held.Values)
		{
			entry.End = Math.Max(entry.Start, Position);
			entry.Extended = true;
			change(entry);
		}
		seek(Position);
	}
	public void Clear() => held.Clear();
}
