namespace osu.Game.Rulesets.EditorAddons;

public sealed class FreehandStroke
{
	public readonly record struct Point(double Time, float X);
	private readonly SortedDictionary<double, float> points = new();
	private readonly Func<double, double> next;
	private Point previous;
	public double Start { get; }
	public double End { get; private set; }
	public IReadOnlyList<Point> Points => points.Select(p => new Point(p.Key, p.Value)).ToArray();
	public FreehandStroke(double start, float x, Func<double, double> next)
	{
		Start = End = start; previous = new Point(start, x); points[start] = x; this.next = next;
	}
	public void Move(double time, float x)
	{
		time = Math.Max(Start, time);
		if (time < previous.Time)
		{
			foreach (double key in points.Keys.Where(k => k > time).ToArray()) points.Remove(key);
		}
		else
		{
			double tick = next(previous.Time);
			for (int guard = 0; tick <= time + 0.00001 && guard < 20000; guard++)
			{
				double fraction = 1;
				if (time > previous.Time) fraction = (tick - previous.Time) / (time - previous.Time);
				points[tick] = previous.X + (float)fraction * (x - previous.X);
				double following = next(tick);
				if (following <= tick) break;
				tick = following;
			}
		}
		previous = new Point(time, x);
		End = time;
	}
	public IReadOnlyList<Point> Finish(double snappedEnd, float x)
	{
		snappedEnd = Math.Max(Start, snappedEnd);
		foreach (double key in points.Keys.Where(k => k > snappedEnd).ToArray()) points.Remove(key);
		if (snappedEnd > Start) points[snappedEnd] = x;
		End = snappedEnd;
		return Points;
	}
}
