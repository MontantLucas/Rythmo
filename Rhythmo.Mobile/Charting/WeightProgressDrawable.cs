using System.Globalization;
using System.Linq;
using Microsoft.Maui.Graphics;
using Rhythmo.Mobile.Theme;

namespace Rhythmo.Mobile.Charting;

/// <summary>Évolution du poids max quotidien pour un exercice.</summary>
public sealed class WeightProgressDrawable : IDrawable
{
	private IReadOnlyList<(DateOnly Day, double Kg)> _series = [];

	public void SetSeries(IReadOnlyList<(DateOnly Day, double Kg)> points) =>
		_series = points.Count == 0 ? [] : points.OrderBy(p => p.Day).ToList();

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		canvas.SaveState();
		try
		{
			DrawCore(canvas, dirtyRect);
		}
		finally
		{
			canvas.ResetState();
		}
	}

	private void DrawCore(ICanvas canvas, RectF dirtyRect)
	{
		// WinUI / layout non finalisé : évite plantages dans le pipeline graphique.
		if (dirtyRect.Width < 1f || dirtyRect.Height < 1f)
			return;

		canvas.Antialias = true;

		var accent = RhythmColors.Accent;
		var muted = RhythmColors.TextSecondary;
		// Toujours définir une couleur de remplissage avant FillRectangle (sinon crash possible sur WinUI).
		canvas.FillColor = RhythmColors.Bg;
		canvas.FillRectangle(dirtyRect);

		if (_series.Count == 0)
		{
			canvas.FontSize = 13f;
			canvas.FontColor = muted;
			canvas.DrawString(
				"Choisis un exercice avec au moins une valeur enregistrée.",
				new RectF(dirtyRect.X + 16f, dirtyRect.Y + 16f,
					Math.Max(0f, dirtyRect.Width - 32f),
					Math.Max(0f, dirtyRect.Height - 32f)),
				HorizontalAlignment.Center,
				VerticalAlignment.Center,
				TextFlow.OverflowBounds);
			return;
		}

		const float padL = 50f;
		const float padR = 14f;
		const float padT = 40f;
		const float padB = 54f;

		var plot = new RectF(
			dirtyRect.X + padL,
			dirtyRect.Y + padT,
			Math.Max(24f, dirtyRect.Width - padL - padR),
			Math.Max(72f, dirtyRect.Height - padT - padB));

		Stroke(canvas, muted, 1f);
		canvas.DrawLine(plot.Left - 8f, plot.Bottom, plot.Right + 8f, plot.Bottom);

		const double minKg = 0;
		double maxKg = _series.Max(p => p.Kg);
		if (maxKg < 1e-6)
			maxKg = 10;
		else
			maxKg += Math.Max(maxKg * 0.08, 2.5);

		double kgSpan = Math.Max(maxKg - minKg, 1e-9);
		var day0 = _series[0].Day;
		var day1 = _series[^1].Day;
		double daySpread = Math.Max(day1.DayNumber - day0.DayNumber, 1);

		canvas.FontSize = 11f;
		canvas.FontColor = muted;
		canvas.DrawString(
			$"{maxKg:0.#}",
			new RectF(plot.Left - 50f, plot.Top - 30f, 46f, 16f),
			HorizontalAlignment.Right,
			VerticalAlignment.Center);
		canvas.DrawString(
			"0",
			new RectF(plot.Left - 50f, plot.Bottom + 4f, 46f, 16f),
			HorizontalAlignment.Right,
			VerticalAlignment.Center);

		PointF at(int ix)
		{
			var day = _series[ix].Day;
			var kg = _series[ix].Kg;
			var tDays = Math.Max(day.DayNumber - day0.DayNumber, 0) / daySpread;
			float px = plot.Left + (float)(tDays * plot.Width);
			float py = plot.Bottom - (float)((kg - minKg) / kgSpan * plot.Height);
			return new PointF(px, py);
		}

		if (_series.Count >= 2)
		{
			var lp = new PathF();
			lp.MoveTo(at(0));
			for (var i = 1; i < _series.Count; i++)
				lp.LineTo(at(i));

			Stroke(canvas, accent, 3f);
			canvas.DrawPath(lp);
		}

		for (var i = 0; i < _series.Count; i++)
		{
			var pt = at(i);
			canvas.FillColor = accent.WithAlpha(.35f);
			canvas.FillCircle(pt.X, pt.Y, 6f);

			Stroke(canvas, accent, 2f);
			canvas.DrawCircle(pt.X, pt.Y, 5f);
		}

		var la = FormatDay(day0);
		var lb = FormatDay(day1);

		canvas.FontSize = 11f;
		canvas.FontColor = muted;

		var ty = plot.Bottom + 42f;
		if (string.Equals(la, lb, StringComparison.Ordinal))
			canvas.DrawString(
				la,
				new RectF(plot.Left - 24f, ty - 18f, plot.Width + 48f, 20f),
				HorizontalAlignment.Center,
				VerticalAlignment.Top,
				TextFlow.OverflowBounds);
		else
			canvas.DrawString(
				$"{la}  →  {lb}",
				new RectF(plot.Left - 22f, ty - 18f, plot.Width + 44f, 20f),
				HorizontalAlignment.Center,
				VerticalAlignment.Top,
				TextFlow.OverflowBounds);

		string FormatDay(DateOnly d) =>
			d.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
	}

	private static void Stroke(ICanvas canvas, Color c, float w)
	{
		canvas.StrokeColor = c;
		canvas.StrokeSize = w;
		canvas.StrokeLineCap = LineCap.Round;
		canvas.StrokeLineJoin = LineJoin.Round;
	}
}
