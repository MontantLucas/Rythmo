using Microsoft.Maui.Graphics;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Charting;

public sealed class MuscleRadarDrawable : IDrawable
{
	public IReadOnlyList<int?> Self { get; set; } = Empty();
	public IReadOnlyList<int?> Other { get; set; } = Empty();

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (dirtyRect.Width < 1 || dirtyRect.Height < 1)
			return;
		canvas.Antialias = true;
		canvas.FillColor = RhythmColors.Bg;
		canvas.FillRectangle(dirtyRect);

		var cx = dirtyRect.Center.X;
		var cy = dirtyRect.Center.Y;
		var radius = MathF.Min(dirtyRect.Width, dirtyRect.Height) * 0.34f;
		canvas.StrokeColor = RhythmColors.Surface2;
		canvas.StrokeSize = 1;
		foreach (var ring in new[] { 2, 4, 6, 8, 10 })
		{
			var r = radius * ring / 10f;
			canvas.DrawCircle(cx, cy, r);
			canvas.FontColor = RhythmColors.TextSecondary;
			canvas.FontSize = 10;
			canvas.DrawString($"R{ring}", cx + 4, cy - r - 12, HorizontalAlignment.Left);
		}

		for (var i = 0; i < 6; i++)
		{
			var end = Point(cx, cy, radius, i);
			canvas.StrokeColor = RhythmColors.Surface2;
			canvas.DrawLine(cx, cy, end.X, end.Y);
			var labelAt = Point(cx, cy, radius + 16, i);
			var name = MuscleIds.Groups[i].NameFr;
			canvas.FontColor = RhythmColors.TextPrimary;
			canvas.FontSize = 11;
			canvas.DrawString(name, labelAt.X - 36, labelAt.Y - 8, 72, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
		}

		StrokeRun(canvas, cx, cy, radius, Other, RhythmColors.Violet);
		StrokeRun(canvas, cx, cy, radius, Self, RhythmColors.Accent);
	}

	private static void StrokeRun(ICanvas canvas, float cx, float cy, float radius, IReadOnlyList<int?> ranks, Color color)
	{
		var present = new bool[6];
		for (var i = 0; i < 6; i++)
			present[i] = i < ranks.Count && ranks[i] is >= 1 and <= 10;
		if (present.All(p => p))
		{
			var path = new PathF();
			var first = Point(cx, cy, radius * (ranks[0]!.Value / 10f), 0);
			path.MoveTo(first);
			for (var i = 1; i < 6; i++)
				path.LineTo(Point(cx, cy, radius * (ranks[i]!.Value / 10f), i));
			path.Close();
			canvas.FillColor = color.WithAlpha(0.22f);
			canvas.FillPath(path);
			canvas.StrokeColor = color;
			canvas.StrokeSize = 2;
			canvas.DrawPath(path);
			return;
		}

		canvas.StrokeColor = color;
		canvas.StrokeSize = 2;
		for (var start = 0; start < 6; start++)
		{
			if (!present[start] || present[(start + 5) % 6])
				continue;
			var path = new PathF();
			path.MoveTo(Point(cx, cy, radius * (ranks[start]!.Value / 10f), start));
			var i = start;
			var count = 1;
			while (present[(i + 1) % 6] && (i + 1) % 6 != start)
			{
				i = (i + 1) % 6;
				path.LineTo(Point(cx, cy, radius * (ranks[i]!.Value / 10f), i));
				count++;
			}
			if (count >= 2)
				canvas.DrawPath(path);
			else
			{
				var p = Point(cx, cy, radius * (ranks[start]!.Value / 10f), start);
				canvas.FillColor = color;
				canvas.FillCircle(p.X, p.Y, 3);
			}
		}
	}

	private static PointF Point(float cx, float cy, float radius, int index)
	{
		var angle = -MathF.PI / 2f + index * (MathF.PI * 2f / 6f);
		return new PointF(cx + radius * MathF.Cos(angle), cy + radius * MathF.Sin(angle));
	}

	private static int?[] Empty() => new int?[6];
}
