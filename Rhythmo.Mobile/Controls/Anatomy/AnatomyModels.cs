namespace Rhythmo.Mobile.Controls.Anatomy;

/// <summary>État visuel d’un groupe — aucun calcul de ranking ici.</summary>
public sealed record BodyGroupVisual(
	string GroupId,
	string NameFr,
	int? ValidatedRank,
	int EvaluatedCount,
	int TotalCount);

public enum AnatomyViewKind
{
	Front,
	Back
}

internal sealed class AnatomyRegion
{
	public AnatomyRegion(string groupId, PathF path, bool decorative = false)
	{
		GroupId = groupId;
		Path = path;
		Decorative = decorative;
		var flat = path.GetFlattenedPath(0.8f, true);
		Hull = new PointF[flat.Count];
		for (var i = 0; i < flat.Count; i++)
			Hull[i] = flat[i];
		Bounds = path.Bounds;
	}

	public string GroupId { get; }
	public PathF Path { get; }
	public bool Decorative { get; }
	public PointF[] Hull { get; }
	public RectF Bounds { get; }

	public bool Contains(float x, float y)
	{
		if (x < Bounds.Left - 2f || x > Bounds.Right + 2f ||
		    y < Bounds.Top - 2f || y > Bounds.Bottom + 2f)
			return false;
		return PointInPolygon(x, y, Hull);
	}

	private static bool PointInPolygon(float x, float y, PointF[] pts)
	{
		if (pts.Length < 3)
			return false;

		var inside = false;
		for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
		{
			var pi = pts[i];
			var pj = pts[j];
			if ((pi.Y > y) == (pj.Y > y))
				continue;
			var denom = pj.Y - pi.Y;
			if (Math.Abs(denom) < 0.0001f)
				continue;
			if (x < (pj.X - pi.X) * (y - pi.Y) / denom + pi.X)
				inside = !inside;
		}

		return inside;
	}
}
