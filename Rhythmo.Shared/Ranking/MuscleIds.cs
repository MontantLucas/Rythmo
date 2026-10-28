namespace Rhythmo.Shared.Ranking;

public static class MuscleIds
{
	public const string Chest = "chest";
	public const string Back = "back";
	public const string Shoulders = "shoulders";
	public const string Arms = "arms";
	public const string Abs = "abs";
	public const string Legs = "legs";

	public const string UpperChest = "upper_chest";
	public const string MidChest = "mid_chest";
	public const string Lats = "lats";
	public const string Traps = "traps";
	public const string Rhomboids = "rhomboids";
	public const string Erectors = "erectors";
	public const string FrontDelt = "front_delt";
	public const string SideDelt = "side_delt";
	public const string RearDelt = "rear_delt";
	public const string Biceps = "biceps";
	public const string Triceps = "triceps";
	public const string AbsMuscle = "abs_muscle";
	public const string Quads = "quads";
	public const string Hamstrings = "hamstrings";
	public const string Glutes = "glutes";
	public const string Calves = "calves";
	public const string Adductors = "adductors";

	public static readonly IReadOnlyList<(string Id, string NameFr, int Sort)> Groups =
	[
		(Chest, "Pectoraux", 1),
		(Back, "Dos", 2),
		(Shoulders, "Épaules", 3),
		(Arms, "Bras", 4),
		(Abs, "Abdominaux", 5),
		(Legs, "Jambes", 6)
	];

	public static readonly IReadOnlyList<(string Id, string GroupId, string NameFr, int Sort)> Muscles =
	[
		(UpperChest, Chest, "Pectoraux supérieurs", 1),
		(MidChest, Chest, "Pectoraux sternocostaux", 2),
		(Lats, Back, "Grand dorsal", 1),
		(Traps, Back, "Trapèzes", 2),
		(Rhomboids, Back, "Rhomboïdes", 3),
		(Erectors, Back, "Érecteurs", 4),
		(FrontDelt, Shoulders, "Deltoïde antérieur", 1),
		(SideDelt, Shoulders, "Deltoïde latéral", 2),
		(RearDelt, Shoulders, "Deltoïde postérieur", 3),
		(Biceps, Arms, "Biceps", 1),
		(Triceps, Arms, "Triceps", 2),
		(AbsMuscle, Abs, "Abdominaux", 1),
		(Quads, Legs, "Quadriceps", 1),
		(Hamstrings, Legs, "Ischio-jambiers", 2),
		(Glutes, Legs, "Fessiers", 3),
		(Calves, Legs, "Mollets", 4),
		(Adductors, Legs, "Adducteurs", 5)
	];

	public const string StandardVersionId = "rhythmo-v1";

	public static readonly IReadOnlyDictionary<string, double> V1Coefficients = new Dictionary<string, double>
	{
		[MidChest] = 0.65,
		[UpperChest] = 0.35,
		[Lats] = 0.40,
		[Erectors] = 0.25,
		[Traps] = 0.22,
		[Rhomboids] = 0.13,
		[SideDelt] = 0.38,
		[FrontDelt] = 0.34,
		[RearDelt] = 0.28,
		[Triceps] = 0.60,
		[Biceps] = 0.40,
		[AbsMuscle] = 1.00,
		[Quads] = 0.28,
		[Glutes] = 0.26,
		[Hamstrings] = 0.20,
		[Calves] = 0.14,
		[Adductors] = 0.12
	};
}
