using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Controls.Anatomy;

internal sealed class AnatomyBodyDrawable : IDrawable
{
	private static readonly Color Skin = Color.FromArgb("#252A33");
	private static readonly Color HeadFill = Color.FromArgb("#323844");
	private static readonly Color MuscleStroke = Color.FromArgb("#141820");

	public AnatomyViewKind Kind { get; set; } = AnatomyViewKind.Front;

	public IReadOnlyDictionary<string, BodyGroupVisual> Groups { get; set; } =
		new Dictionary<string, BodyGroupVisual>();

	public string? SelectedGroupId { get; set; }
	public string? HoveredGroupId { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (dirtyRect.Width < 1f || dirtyRect.Height < 1f)
			return;

		canvas.SaveState();
		try
		{
			canvas.Antialias = true;
			canvas.FillColor = Colors.Transparent;
			canvas.FillRectangle(dirtyRect);

			var (scale, ox, oy) = Fit(dirtyRect.Width, dirtyRect.Height);
			canvas.Translate(ox, oy);
			canvas.Scale(scale, scale);

			canvas.FillColor = Skin;
			canvas.FillPath(AnatomyGeometry.Silhouette(Kind));

			foreach (var region in AnatomyGeometry.For(Kind))
				DrawRegion(canvas, region);

			canvas.FillColor = HeadFill;
			canvas.FillPath(AnatomyGeometry.Head);
			canvas.StrokeColor = MuscleStroke;
			canvas.StrokeSize = 0.8f;
			canvas.DrawPath(AnatomyGeometry.Head);
		}
		finally
		{
			canvas.RestoreState();
		}
	}

	private void DrawRegion(ICanvas canvas, AnatomyRegion region)
	{
		Groups.TryGetValue(region.GroupId, out var visual);
		var rank = visual?.ValidatedRank;
		var fill = RankPalette.For(rank);
		if (region.Decorative)
			fill = Lighten(fill, 0.10f);

		var selected = SelectedGroupId == region.GroupId;
		var hovered = HoveredGroupId == region.GroupId && !selected;
		if (selected)
			fill = Lighten(fill, 0.18f);
		else if (hovered)
			fill = Lighten(fill, 0.10f);

		canvas.FillColor = fill;
		canvas.FillPath(region.Path);

		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.StrokeLineCap = LineCap.Round;
		if (selected)
		{
			canvas.StrokeColor = RhythmColors.Accent;
			canvas.StrokeSize = 2.4f;
		}
		else if (hovered)
		{
			canvas.StrokeColor = Colors.White.WithAlpha(0.45f);
			canvas.StrokeSize = 1.8f;
		}
		else
		{
			canvas.StrokeColor = MuscleStroke;
			canvas.StrokeSize = region.Decorative ? 0.6f : 1.05f;
		}

		canvas.DrawPath(region.Path);
	}

	public string? HitTest(float canvasX, float canvasY, float canvasW, float canvasH)
	{
		var (scale, ox, oy) = Fit(canvasW, canvasH);
		if (scale < 0.01f)
			return null;

		var dx = (canvasX - ox) / scale;
		var dy = (canvasY - oy) / scale;
		var regions = AnatomyGeometry.For(Kind);

		foreach (var offset in HitOffsets)
		{
			var x = dx + offset.X;
			var y = dy + offset.Y;
			for (var i = regions.Count - 1; i >= 0; i--)
			{
				if (regions[i].Contains(x, y))
					return regions[i].GroupId;
			}
		}

		return null;
	}

	private static readonly PointF[] HitOffsets =
	[
		new(0, 0),
		new(-3, 0),
		new(3, 0),
		new(0, -3),
		new(0, 3),
		new(-2, -2),
		new(2, 2)
	];

	public static (float Scale, float Ox, float Oy) Fit(float canvasW, float canvasH)
	{
		var scale = Math.Min(canvasW / AnatomyGeometry.W, canvasH / AnatomyGeometry.H);
		var ox = (canvasW - AnatomyGeometry.W * scale) / 2f;
		var oy = (canvasH - AnatomyGeometry.H * scale) / 2f;
		return (scale, ox, oy);
	}

	private static Color Lighten(Color c, float amount)
	{
		amount = Math.Clamp(amount, 0f, 1f);
		return Color.FromRgba(
			c.Red + (1f - c.Red) * amount,
			c.Green + (1f - c.Green) * amount,
			c.Blue + (1f - c.Blue) * amount,
			c.Alpha);
	}
}
