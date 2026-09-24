using Microsoft.Maui.Graphics;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Controls.Anatomy;

internal sealed class AnatomyBodyDrawable : IDrawable
{
	public AnatomyViewKind Kind { get; set; } = AnatomyViewKind.Front;

	public IReadOnlyDictionary<string, BodyGroupVisual> Groups { get; set; } =
		new Dictionary<string, BodyGroupVisual>();

	public string? SelectedGroupId { get; set; }
	public string? HoveredGroupId { get; set; }

	/// <summary>Robot dessiné dans le canvas (obligatoire sur Windows : le GraphicsView y est opaque).</summary>
	public Microsoft.Maui.Graphics.IImage? RobotImage { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (dirtyRect.Width < 1f || dirtyRect.Height < 1f)
			return;

		canvas.SaveState();
		try
		{
			canvas.Antialias = true;
			canvas.FillColor = RhythmColors.Bg;
			canvas.FillRectangle(dirtyRect);

			var (scale, ox, oy) = Fit(dirtyRect.Width, dirtyRect.Height);
			canvas.Translate(ox, oy);
			canvas.Scale(scale, scale);

			if (RobotImage is not null)
				canvas.DrawImage(RobotImage, 0, 0, AnatomyGeometry.W, AnatomyGeometry.H);

			foreach (var region in AnatomyGeometry.For(Kind))
				DrawRegion(canvas, region);
		}
		finally
		{
			canvas.RestoreState();
		}
	}

	private void DrawRegion(ICanvas canvas, AnatomyRegion region)
	{
		var selected = SelectedGroupId == region.GroupId;
		var hovered = HoveredGroupId == region.GroupId && !selected;
		if (!selected && !hovered)
			return;

		Groups.TryGetValue(region.GroupId, out var visual);
		var rankColor = RankPalette.For(visual?.ValidatedRank);

		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.StrokeLineCap = LineCap.Round;
		if (selected)
		{
			canvas.FillColor = rankColor.WithAlpha(0.28f);
			canvas.FillPath(region.Path);
			canvas.StrokeColor = RhythmColors.Accent;
			canvas.StrokeSize = 2.6f;
		}
		else
		{
			canvas.FillColor = Colors.White.WithAlpha(0.14f);
			canvas.FillPath(region.Path);
			canvas.StrokeColor = Colors.White.WithAlpha(0.45f);
			canvas.StrokeSize = 1.8f;
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
		new(-4, 0),
		new(4, 0),
		new(0, -4),
		new(0, 4),
		new(-3, -3),
		new(3, 3)
	];

	public static (float Scale, float Ox, float Oy) Fit(float canvasW, float canvasH)
	{
		var scale = Math.Min(canvasW / AnatomyGeometry.W, canvasH / AnatomyGeometry.H);
		var ox = (canvasW - AnatomyGeometry.W * scale) / 2f;
		var oy = (canvasH - AnatomyGeometry.H * scale) / 2f;
		return (scale, ox, oy);
	}
}
