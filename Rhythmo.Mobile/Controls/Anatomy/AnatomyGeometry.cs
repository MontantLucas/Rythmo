using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Controls.Anatomy;

/// <summary>Zones cliquables du mascotte, alignées sur le SVG 160×200.</summary>
internal static class AnatomyGeometry
{
	public const float W = 160f;
	public const float H = 200f;
	public const float Cx = 80f;

	public static IReadOnlyList<AnatomyRegion> Front { get; } = BuildFront();
	public static IReadOnlyList<AnatomyRegion> Back { get; } = BuildBack();

	public static IReadOnlyList<AnatomyRegion> For(AnatomyViewKind kind) =>
		kind == AnatomyViewKind.Back ? Back : Front;

	private static IReadOnlyList<AnatomyRegion> BuildFront()
	{
		var list = new List<AnatomyRegion>();
		AddMirror(list, MuscleIds.Legs, RoundRect(54, 136, 24, 40, 11));
		list.Add(new AnatomyRegion(MuscleIds.Abs, RoundRect(54, 112, 52, 28, 12)));
		list.Add(new AnatomyRegion(MuscleIds.Chest, RoundRect(54, 84, 52, 32, 14)));
		AddMirror(list, MuscleIds.Arms, RoundRect(26, 96, 22, 46, 11));
		AddMirror(list, MuscleIds.Shoulders, RoundRect(18, 74, 38, 28, 13));
		return list;
	}

	private static IReadOnlyList<AnatomyRegion> BuildBack()
	{
		var list = new List<AnatomyRegion>();
		AddMirror(list, MuscleIds.Legs, RoundRect(52, 128, 26, 48, 12));
		list.Add(new AnatomyRegion(MuscleIds.Back, RoundRect(50, 80, 60, 56, 16)));
		AddMirror(list, MuscleIds.Arms, RoundRect(24, 94, 22, 48, 11));
		AddMirror(list, MuscleIds.Shoulders, RoundRect(18, 74, 38, 28, 13));
		return list;
	}

	private static void AddMirror(List<AnatomyRegion> list, string groupId, PathF left)
	{
		list.Add(new AnatomyRegion(groupId, left));
		list.Add(new AnatomyRegion(groupId, MirrorX(left)));
	}

	private static PathF RoundRect(float x, float y, float w, float h, float r)
	{
		r = Math.Min(r, Math.Min(w, h) * 0.5f);
		var k = 0.55228475f * r;
		var p = new PathF();
		p.MoveTo(x + r, y);
		p.LineTo(x + w - r, y);
		p.CurveTo(x + w - r + k, y, x + w, y + r - k, x + w, y + r);
		p.LineTo(x + w, y + h - r);
		p.CurveTo(x + w, y + h - r + k, x + w - r + k, y + h, x + w - r, y + h);
		p.LineTo(x + r, y + h);
		p.CurveTo(x + r - k, y + h, x, y + h - r + k, x, y + h - r);
		p.LineTo(x, y + r);
		p.CurveTo(x, y + r - k, x + r - k, y, x + r, y);
		p.Close();
		return p;
	}

	private static PathF MirrorX(PathF src)
	{
		var dst = new PathF();
		var pointIndex = 0;
		for (var s = 0; s < src.OperationCount; s++)
		{
			var op = src.GetSegmentType(s);
			switch (op)
			{
				case PathOperation.Move:
				{
					var m = src[pointIndex++];
					dst.MoveTo(Flip(m.X), m.Y);
					break;
				}
				case PathOperation.Line:
				{
					var l = src[pointIndex++];
					dst.LineTo(Flip(l.X), l.Y);
					break;
				}
				case PathOperation.Quad:
				{
					var c = src[pointIndex++];
					var e = src[pointIndex++];
					dst.QuadTo(Flip(c.X), c.Y, Flip(e.X), e.Y);
					break;
				}
				case PathOperation.Cubic:
				{
					var c1 = src[pointIndex++];
					var c2 = src[pointIndex++];
					var e = src[pointIndex++];
					dst.CurveTo(Flip(c1.X), c1.Y, Flip(c2.X), c2.Y, Flip(e.X), e.Y);
					break;
				}
				case PathOperation.Close:
					dst.Close();
					break;
				default:
					if (pointIndex < src.Count)
						pointIndex++;
					break;
			}
		}

		return dst;
	}

	private static float Flip(float x) => Cx * 2f - x;
}
