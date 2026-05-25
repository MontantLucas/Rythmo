using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rhythmo.Mobile.Data;

/// <summary>Cataloque intégré (pas d’API) : zone + nom + MET indicatif pour kcal grossières.</summary>
public static class LocalExerciseSeed
{
	/// <summary>Ordre d’affichage des filtres par « type ».</summary>
	public static IReadOnlyList<string> CategoryOrder { get; } =
	[
		"Pectoraux",
		"Dos",
		"Épaules",
		"Biceps",
		"Triceps",
		"Jambes — Quadriceps",
		"Ischios / Fessiers",
		"Mollets",
		"Abdominaux",
		"Poids du corps / Street Workout",
	];

	private static readonly IReadOnlyList<(string Cat, string Name)> Pairs =
	[
		// Pectoraux
		("Pectoraux", "Développé couché barre"),
		("Pectoraux", "Développé couché haltères"),
		("Pectoraux", "Développé incliné"),
		("Pectoraux", "Développé décliné"),
		("Pectoraux", "Dips"),
		("Pectoraux", "Écarté haltères"),
		("Pectoraux", "Pec deck / Butterfly"),
		("Pectoraux", "Pompes"),
		// Dos
		("Dos", "Tractions"),
		("Dos", "Tractions lestées"),
		("Dos", "Muscle-up"),
		("Dos", "Tirage vertical"),
		("Dos", "Rowing barre"),
		("Dos", "Rowing haltère"),
		("Dos", "Tirage horizontal"),
		("Dos", "Soulevé de terre"),
		("Dos", "Pullover"),
		("Dos", "Rowing T-Bar"),
		// Épaules
		("Épaules", "Développé militaire"),
		("Épaules", "Shoulder press"),
		("Épaules", "Élévations latérales"),
		("Épaules", "Élévations frontales"),
		("Épaules", "Oiseau"),
		("Épaules", "Face pull"),
		("Épaules", "Tirage menton"),
		("Épaules", "Arnold press"),
		// Biceps
		("Biceps", "Curl barre"),
		("Biceps", "Curl haltères"),
		("Biceps", "Curl incliné"),
		("Biceps", "Curl marteau"),
		("Biceps", "Curl pupitre"),
		("Biceps", "Curl poulie"),
		// Triceps
		("Triceps", "Barre au front"),
		("Triceps", "Extension poulie"),
		("Triceps", "Dips"),
		("Triceps", "Extension haltère derrière la tête"),
		("Triceps", "Pushdown corde"),
		("Triceps", "Développé serré"),
		// Jambes — Quadriceps
		("Jambes — Quadriceps", "Squat"),
		("Jambes — Quadriceps", "Front squat"),
		("Jambes — Quadriceps", "Presse à cuisses"),
		("Jambes — Quadriceps", "Hack squat"),
		("Jambes — Quadriceps", "Fentes"),
		("Jambes — Quadriceps", "Bulgarian split squat"),
		("Jambes — Quadriceps", "Leg extension"),
		("Jambes — Quadriceps", "Sissy squat"),
		// Ischios / Fessiers
		("Ischios / Fessiers", "Soulevé de terre roumain"),
		("Ischios / Fessiers", "Leg curl"),
		("Ischios / Fessiers", "Hip thrust"),
		("Ischios / Fessiers", "Good morning"),
		("Ischios / Fessiers", "Fentes marchées"),
		("Ischios / Fessiers", "Glute bridge"),
		("Ischios / Fessiers", "Kickback poulie"),
		// Mollets
		("Mollets", "Mollets machine"),
		("Mollets", "Mollets smith"),
		("Mollets", "Mollets à la presse"),
		// Abdominaux
		("Abdominaux", "Crunch"),
		("Abdominaux", "Crunch poulie"),
		("Abdominaux", "Relevés de jambes"),
		("Abdominaux", "Relevés de jambes suspendu"),
		("Abdominaux", "Gainage"),
		("Abdominaux", "Russian twist"),
		("Abdominaux", "Mountain climbers"),
		("Abdominaux", "Planche latérale"),
		("Abdominaux", "Ab wheel"),
		("Abdominaux", "Hollow body hold"),
		// Poids du corps / Street Workout
		("Poids du corps / Street Workout", "Tractions"),
		("Poids du corps / Street Workout", "Muscle-up"),
		("Poids du corps / Street Workout", "Dips"),
		("Poids du corps / Street Workout", "Pompes"),
		("Poids du corps / Street Workout", "Pompes diamant"),
		("Poids du corps / Street Workout", "Tractions australiennes"),
		("Poids du corps / Street Workout", "L-sit"),
		("Poids du corps / Street Workout", "Front lever"),
		("Poids du corps / Street Workout", "Back lever"),
		("Poids du corps / Street Workout", "Handstand push-up"),
	];

	public static CachedExerciseRow[] CreateRows() =>
		Pairs.Select(pair => new CachedExerciseRow
		{
			Id = StableGuid(pair.Cat, pair.Name),
			NameFr = pair.Name,
			Category = pair.Cat,
			MetApprox = RoughMet(pair.Cat, pair.Name),
		}).ToArray();

	public static Guid StableGuid(string category, string nameFr)
	{
		var blob = $"{category.Trim()}|{nameFr.Trim()}";
		var md5 = MD5.HashData(Encoding.UTF8.GetBytes(blob));
		return new Guid(md5);
	}

	private static double RoughMet(string cat, string nm)
	{
		var lc = nm.ToLowerInvariant();

		switch (cat)
		{
			case "Mollets":
				return 5.2;
			case "Abdominaux":
				return 4.2;
			case "Biceps":
			case "Triceps":
				return 5.5;
			case "Épaules":
				return 7.0;
			case "Poids du corps / Street Workout":
				return lc.Contains("lever") || lc.Contains("stand") ? 9.0 :
					lc.Contains("planche") || lc.Contains("l-sit") ? 8.0 : 7.5;
			case "Dos":
				if (lc.Contains("soulev"))
					return 8.0;
				if (lc.Contains("muscle"))
					return 8.8;
				if (lc.Contains("rowing"))
					return 7.5;
				return 7.2;
			case "Pectoraux":
				if (lc.Contains("deck") || lc.Contains("papillon") || lc.Contains("écart"))
					return 4.8;
				if (lc.Contains("dip"))
					return 7.0;
				if (lc.Contains("pompes"))
					return 7.0;
				if (lc.Contains("couche"))
					return 6.8;
				return 6.2;
			case "Jambes — Quadriceps":
				if (lc.Contains("extension") || lc.Contains("split") || lc.StartsWith("fentes "))
					return 6.8;
				if (lc.Contains("sissy"))
					return 5.9;
				return 8.4;
			case "Ischios / Fessiers":
				if (lc.Contains("curl") || lc.Contains("kickback"))
					return 6.0;
				return 8.0;
			default:
				return 6.8;
		}
	}}
