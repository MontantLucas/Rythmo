using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Controls.Anatomy;

/// <summary>Silhouettes et régions musculaires en coordonnées design 120×250.</summary>
internal static class AnatomyGeometry
{
	public const float W = 120f;
	public const float H = 250f;
	public const float Cx = 60f;

	public static PathF FrontSilhouette { get; } = BuildFrontSilhouette();
	public static PathF BackSilhouette { get; } = BuildBackSilhouette();
	public static PathF Head { get; } = BuildHead();

	public static IReadOnlyList<AnatomyRegion> Front { get; } = BuildFront();
	public static IReadOnlyList<AnatomyRegion> Back { get; } = BuildBack();

	public static IReadOnlyList<AnatomyRegion> For(AnatomyViewKind kind) =>
		kind == AnatomyViewKind.Back ? Back : Front;

	public static PathF Silhouette(AnatomyViewKind kind) =>
		kind == AnatomyViewKind.Back ? BackSilhouette : FrontSilhouette;

	private static IReadOnlyList<AnatomyRegion> BuildFront()
	{
		var list = new List<AnatomyRegion>();

		// Jambes (dessous)
		AddMirror(list, MuscleIds.Legs, QuadLeft());
		AddMirror(list, MuscleIds.Legs, InnerThighLeft());
		AddMirror(list, MuscleIds.Legs, CalfLeft());

		// Abdos
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPlate()));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(46.5f, 86, 11.5f, 9.5f), decorative: true));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(62f, 86, 11.5f, 9.5f), decorative: true));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(46.5f, 97.5f, 11.5f, 9.5f), decorative: true));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(62f, 97.5f, 11.5f, 9.5f), decorative: true));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(47.2f, 109, 10.8f, 9.2f), decorative: true));
		list.Add(new AnatomyRegion(MuscleIds.Abs, AbsPack(62f, 109, 10.8f, 9.2f), decorative: true));
		AddMirror(list, MuscleIds.Abs, ObliqueLeft());

		// Pectoraux
		AddMirror(list, MuscleIds.Chest, PecLeft());

		// Bras
		AddMirror(list, MuscleIds.Arms, BicepsLeft());
		AddMirror(list, MuscleIds.Arms, ForearmLeft());

		// Épaules (dessus pour le hit-test)
		AddMirror(list, MuscleIds.Shoulders, FrontDeltLeft());

		return list;
	}

	private static IReadOnlyList<AnatomyRegion> BuildBack()
	{
		var list = new List<AnatomyRegion>();

		AddMirror(list, MuscleIds.Legs, HamLeft());
		AddMirror(list, MuscleIds.Legs, BackCalfLeft());
		AddMirror(list, MuscleIds.Legs, GluteLeft());

		list.Add(new AnatomyRegion(MuscleIds.Back, Traps()));
		AddMirror(list, MuscleIds.Back, LatLeft());
		list.Add(new AnatomyRegion(MuscleIds.Back, Erectors()));

		AddMirror(list, MuscleIds.Arms, TricepsLeft());
		AddMirror(list, MuscleIds.Arms, BackForearmLeft());

		AddMirror(list, MuscleIds.Shoulders, RearDeltLeft());

		return list;
	}

	private static void AddMirror(List<AnatomyRegion> list, string groupId, PathF left)
	{
		list.Add(new AnatomyRegion(groupId, left));
		list.Add(new AnatomyRegion(groupId, MirrorX(left)));
	}

	private static PathF BuildHead()
	{
		var p = new PathF();
		p.AppendEllipse(47.5f, 9f, 25f, 30f);
		return p;
	}

	private static PathF BuildFrontSilhouette()
	{
		var p = new PathF();
		p.MoveTo(60, 8);
		p.CurveTo(73, 8, 76, 18, 76, 28);
		p.CurveTo(76, 36, 71, 41, 67, 43);
		p.LineTo(67, 47);
		p.CurveTo(86, 48, 100, 56, 98, 70);
		p.CurveTo(97, 92, 94, 118, 90, 136);
		p.CurveTo(89, 142, 83, 144, 81, 138);
		p.CurveTo(83, 118, 86, 90, 82, 74);
		p.CurveTo(80, 70, 76, 69, 74, 72);
		p.CurveTo(76, 92, 77, 112, 74, 124);
		p.CurveTo(72, 132, 70, 136, 68, 140);
		p.CurveTo(71, 168, 73, 198, 71, 222);
		p.CurveTo(70, 232, 66, 238, 60, 238);
		p.CurveTo(54, 238, 50, 232, 49, 222);
		p.CurveTo(47, 198, 49, 168, 52, 140);
		p.CurveTo(50, 136, 48, 132, 46, 124);
		p.CurveTo(43, 112, 44, 92, 46, 72);
		p.CurveTo(44, 69, 40, 70, 38, 74);
		p.CurveTo(34, 90, 37, 118, 39, 138);
		p.CurveTo(37, 144, 31, 142, 30, 136);
		p.CurveTo(26, 118, 23, 92, 22, 70);
		p.CurveTo(20, 56, 34, 48, 53, 47);
		p.LineTo(53, 43);
		p.CurveTo(49, 41, 44, 36, 44, 28);
		p.CurveTo(44, 18, 47, 8, 60, 8);
		p.Close();
		return p;
	}

	private static PathF BuildBackSilhouette()
	{
		var p = new PathF();
		p.MoveTo(60, 8);
		p.CurveTo(73, 8, 76, 18, 76, 28);
		p.CurveTo(76, 36, 71, 41, 67, 43);
		p.LineTo(67, 47);
		p.CurveTo(88, 49, 101, 58, 99, 72);
		p.CurveTo(98, 94, 95, 120, 91, 138);
		p.CurveTo(90, 144, 84, 145, 82, 139);
		p.CurveTo(84, 118, 87, 90, 83, 74);
		p.CurveTo(81, 70, 77, 70, 75, 74);
		p.CurveTo(77, 94, 78, 114, 76, 126);
		p.CurveTo(80, 132, 82, 140, 80, 146);
		p.CurveTo(78, 172, 76, 200, 73, 222);
		p.CurveTo(72, 232, 66, 238, 60, 238);
		p.CurveTo(54, 238, 48, 232, 47, 222);
		p.CurveTo(44, 200, 42, 172, 40, 146);
		p.CurveTo(38, 140, 40, 132, 44, 126);
		p.CurveTo(42, 114, 43, 94, 45, 74);
		p.CurveTo(43, 70, 39, 70, 37, 74);
		p.CurveTo(33, 90, 36, 118, 38, 139);
		p.CurveTo(36, 145, 30, 144, 29, 138);
		p.CurveTo(25, 120, 22, 94, 21, 72);
		p.CurveTo(19, 58, 32, 49, 53, 47);
		p.LineTo(53, 43);
		p.CurveTo(49, 41, 44, 36, 44, 28);
		p.CurveTo(44, 18, 47, 8, 60, 8);
		p.Close();
		return p;
	}

	private static PathF FrontDeltLeft()
	{
		var p = new PathF();
		p.MoveTo(44, 48);
		p.CurveTo(36, 46, 26, 48, 22, 54);
		p.CurveTo(19, 59, 20, 65, 26, 67);
		p.CurveTo(32, 69, 40, 62, 46, 55);
		p.CurveTo(47, 52, 46, 49, 44, 48);
		p.Close();
		return p;
	}

	private static PathF PecLeft()
	{
		var p = new PathF();
		p.MoveTo(59, 53);
		p.CurveTo(50, 50, 40, 53, 36, 62);
		p.CurveTo(34, 70, 36, 79, 46, 84);
		p.CurveTo(52, 86, 58, 81, 59, 72);
		p.CurveTo(60, 64, 60, 56, 59, 53);
		p.Close();
		return p;
	}

	private static PathF BicepsLeft()
	{
		var p = new PathF();
		p.MoveTo(26, 66);
		p.CurveTo(19, 68, 16, 74, 15.5f, 86);
		p.CurveTo(15.5f, 94, 19, 100, 26, 99);
		p.CurveTo(32, 98, 35, 88, 35, 76);
		p.CurveTo(35, 70, 31, 66, 26, 66);
		p.Close();
		return p;
	}

	private static PathF ForearmLeft()
	{
		var p = new PathF();
		p.MoveTo(16, 97);
		p.CurveTo(13, 110, 11, 122, 12, 134);
		p.CurveTo(13, 138, 18, 140, 21, 137);
		p.CurveTo(26, 124, 28, 108, 26, 99);
		p.CurveTo(22, 96, 18, 96, 16, 97);
		p.Close();
		return p;
	}

	private static PathF AbsPlate()
	{
		var p = new PathF();
		p.MoveTo(47, 83);
		p.CurveTo(44, 90, 44, 102, 46, 118);
		p.CurveTo(50, 122, 70, 122, 74, 118);
		p.CurveTo(76, 102, 76, 90, 73, 83);
		p.CurveTo(68, 85, 52, 85, 47, 83);
		p.Close();
		return p;
	}

	private static PathF AbsPack(float x, float y, float w, float h)
	{
		var p = new PathF();
		p.AppendRoundedRectangle(x, y, w, h, 2.4f);
		return p;
	}

	private static PathF ObliqueLeft()
	{
		var p = new PathF();
		p.MoveTo(46, 84);
		p.CurveTo(40, 86, 36, 94, 36, 106);
		p.CurveTo(36, 114, 40, 120, 46, 118);
		p.CurveTo(47, 108, 47, 94, 46, 84);
		p.Close();
		return p;
	}

	private static PathF QuadLeft()
	{
		var p = new PathF();
		p.MoveTo(48, 118);
		p.CurveTo(39, 120, 34, 130, 33, 152);
		p.CurveTo(32, 168, 34, 178, 42, 180);
		p.CurveTo(49, 181, 53, 166, 54, 144);
		p.CurveTo(54, 128, 52, 118, 48, 118);
		p.Close();
		return p;
	}

	private static PathF InnerThighLeft()
	{
		var p = new PathF();
		p.MoveTo(52, 122);
		p.CurveTo(50, 136, 50, 152, 51, 168);
		p.CurveTo(53, 174, 57, 174, 58, 166);
		p.CurveTo(59, 148, 58, 130, 56, 122);
		p.CurveTo(54, 120, 53, 120, 52, 122);
		p.Close();
		return p;
	}

	private static PathF CalfLeft()
	{
		var p = new PathF();
		p.MoveTo(34, 178);
		p.CurveTo(31, 194, 32, 210, 38, 224);
		p.CurveTo(41, 228, 48, 228, 50, 220);
		p.CurveTo(52, 204, 50, 188, 42, 178);
		p.CurveTo(39, 176, 36, 176, 34, 178);
		p.Close();
		return p;
	}

	private static PathF Traps()
	{
		var p = new PathF();
		p.MoveTo(60, 45);
		p.CurveTo(48, 46, 34, 50, 30, 56);
		p.CurveTo(38, 59, 50, 66, 60, 68);
		p.CurveTo(70, 66, 82, 59, 90, 56);
		p.CurveTo(86, 50, 72, 46, 60, 45);
		p.Close();
		return p;
	}

	private static PathF LatLeft()
	{
		var p = new PathF();
		p.MoveTo(56, 66);
		p.CurveTo(40, 68, 28, 78, 25, 96);
		p.CurveTo(24, 108, 30, 116, 46, 118);
		p.CurveTo(52, 116, 56, 102, 56, 82);
		p.CurveTo(56, 72, 56, 66, 56, 66);
		p.Close();
		return p;
	}

	private static PathF Erectors()
	{
		var p = new PathF();
		p.MoveTo(53, 68);
		p.CurveTo(52, 90, 52, 108, 54, 118);
		p.LineTo(66, 118);
		p.CurveTo(68, 108, 68, 90, 67, 68);
		p.Close();
		return p;
	}

	private static PathF RearDeltLeft()
	{
		var p = new PathF();
		p.MoveTo(44, 50);
		p.CurveTo(34, 50, 24, 54, 21, 60);
		p.CurveTo(20, 66, 26, 70, 34, 68);
		p.CurveTo(42, 65, 46, 58, 44, 50);
		p.Close();
		return p;
	}

	private static PathF TricepsLeft()
	{
		var p = new PathF();
		p.MoveTo(25, 66);
		p.CurveTo(17, 70, 14, 80, 14, 94);
		p.CurveTo(14, 104, 18, 108, 25, 106);
		p.CurveTo(31, 104, 33, 90, 33, 76);
		p.CurveTo(33, 70, 30, 66, 25, 66);
		p.Close();
		return p;
	}

	private static PathF BackForearmLeft()
	{
		var p = new PathF();
		p.MoveTo(15, 104);
		p.CurveTo(12, 116, 11, 128, 12, 138);
		p.CurveTo(13, 142, 18, 143, 21, 140);
		p.CurveTo(26, 126, 28, 112, 25, 106);
		p.CurveTo(21, 103, 17, 103, 15, 104);
		p.Close();
		return p;
	}

	private static PathF GluteLeft()
	{
		var p = new PathF();
		p.MoveTo(47, 114);
		p.CurveTo(38, 116, 33, 124, 35, 134);
		p.CurveTo(38, 142, 50, 143, 57, 136);
		p.CurveTo(59, 128, 56, 116, 47, 114);
		p.Close();
		return p;
	}

	private static PathF HamLeft()
	{
		var p = new PathF();
		p.MoveTo(38, 136);
		p.CurveTo(33, 148, 31, 164, 33, 178);
		p.CurveTo(36, 184, 45, 184, 49, 176);
		p.CurveTo(53, 160, 53, 146, 50, 138);
		p.CurveTo(46, 134, 42, 134, 38, 136);
		p.Close();
		return p;
	}

	private static PathF BackCalfLeft()
	{
		var p = new PathF();
		p.MoveTo(33, 178);
		p.CurveTo(30, 194, 31, 212, 37, 226);
		p.CurveTo(40, 230, 48, 230, 50, 222);
		p.CurveTo(52, 206, 50, 190, 42, 178);
		p.CurveTo(39, 176, 35, 176, 33, 178);
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
