using Rhythmo.Shared.Ranking;

namespace Rhythmo.Shared.History;

public static class ExerciseArt
{
	public const string Generic = "illus_generic";

	private static readonly Dictionary<(string Category, string Name), string> Specific = new()
	{
		[("Pectoraux", "Développé incliné")] = "ex_developpe_incline",
		[("Pectoraux", "Développé incliné haltères")] = "ex_developpe_incline_halteres",
		[("Pectoraux", "Développé couché barre")] = "ex_developpe_couche",
		[("Pectoraux", "Développé couché haltères")] = "ex_developpe_couche_halteres",
		[("Pectoraux", "Développé décliné")] = "ex_developpe_decline",
		[("Pectoraux", "Développé décliné haltères")] = "ex_developpe_decline_halteres",
		[("Pectoraux", "Chest press")] = "ex_chest_press",
		[("Pectoraux", "Pec deck / Butterfly")] = "ex_pec_deck",
		[("Pectoraux", "Écarté haltères")] = "ex_ecarte_halteres",
		[("Pectoraux", "Écarté poulie")] = "ex_ecarte_poulie",
		[("Dos", "Tractions lestées")] = "ex_tractions_lestees",
		[("Dos", "Tirage vertical prise serrée")] = "ex_tirage_vertical_serre",
		[("Dos", "Tirage vertical")] = "ex_tirage_vertical",
		[("Dos", "Rowing barre")] = "ex_rowing_barre",
		[("Dos", "Rowing haltère")] = "ex_rowing_haltere",
		[("Dos", "Tirage horizontal")] = "ex_tirage_horizontal",
		[("Dos", "Rowing poulie unilatéral")] = "ex_rowing_poulie_unilateral",
		[("Dos", "Pullover")] = "ex_pullover",
		[("Dos", "Shrug barre")] = "ex_shrug_barre",
		[("Dos", "Shrug haltères")] = "ex_shrug_halteres",
		[("Dos", "Shrug machine")] = "ex_shrug_machine",
		[("Dos", "Soulevé de terre")] = "ex_souleve_de_terre",
		[("Dos", "Extension lombaire machine")] = "ex_extension_lombaire",
		[("Épaules", "Développé militaire")] = "ex_developpe_militaire",
		[("Épaules", "Développé militaire haltères")] = "ex_developpe_militaire_halteres",
		[("Épaules", "Shoulder press")] = "ex_shoulder_press",
		[("Épaules", "Élévations latérales")] = "ex_elevations_laterales",
		[("Épaules", "Élévations latérales poulie")] = "ex_elevations_laterales_poulie",
		[("Épaules", "Élévations latérales machine")] = "ex_elevations_laterales_machine",
		[("Épaules", "Reverse pec deck")] = "ex_reverse_pec_deck",
		[("Épaules", "Face pull")] = "ex_face_pull",
		[("Biceps", "Curl barre")] = "ex_curl_barre",
		[("Biceps", "Curl haltères")] = "ex_curl_halteres",
		[("Biceps", "Curl pupitre")] = "ex_curl_pupitre",
		[("Triceps", "Extension poulie")] = "ex_extension_poulie",
		[("Triceps", "Pushdown corde")] = "ex_pushdown_corde",
		[("Triceps", "Extension overhead poulie")] = "ex_extension_overhead_poulie",
		[("Abdominaux", "Crunch poulie")] = "ex_crunch_poulie",
		[("Abdominaux", "Crunch machine")] = "ex_crunch_machine",
		[("Abdominaux", "Crunch décliné lesté")] = "ex_crunch_decline_leste",
		[("Abdominaux", "Reverse crunch lesté")] = "ex_reverse_crunch_leste",
		[("Jambes — Quadriceps", "Squat")] = "ex_squat",
		[("Jambes — Quadriceps", "Hack squat")] = "ex_hack_squat",
		[("Jambes — Quadriceps", "Presse à cuisses")] = "ex_presse_cuisses",
		[("Jambes — Quadriceps", "Leg extension")] = "ex_leg_extension",
		[("Jambes — Quadriceps", "Bulgarian split squat")] = "ex_bulgarian_split_squat",
		[("Ischios / Fessiers", "Fentes marchées")] = "ex_fentes_marchees",
		[("Ischios / Fessiers", "Soulevé de terre roumain")] = "ex_souleve_roumain",
		[("Ischios / Fessiers", "Leg curl")] = "ex_leg_curl",
		[("Ischios / Fessiers", "Hip thrust")] = "ex_hip_thrust",
		[("Mollets", "Mollets machine")] = "ex_mollets_machine",
		[("Mollets", "Mollets smith")] = "ex_mollets_smith"
	};

	public static IReadOnlyCollection<string> SpecificFiles => Specific.Values;

	public static string Resolve(string? category, string? nameFr)
	{
		if (string.IsNullOrWhiteSpace(nameFr))
			return Generic;
		if (!string.IsNullOrWhiteSpace(category) &&
		    Specific.TryGetValue((category, nameFr), out var file))
			return file;

		var def = string.IsNullOrWhiteSpace(category)
			? null
			: MuscleRankingCatalog.Find(category, nameFr);
		def ??= MuscleRankingCatalog.All.FirstOrDefault(x => x.NameFr == nameFr);
		return GroupFile(def) ?? Generic;
	}

	public static string GroupFile(string groupId) => $"muscle_{groupId}";

	private static string? GroupFile(ExerciseRankingDef? def)
	{
		if (def is null)
			return null;
		var muscle = def.PrimaryMuscles.FirstOrDefault() ?? def.SecondaryMuscles.FirstOrDefault();
		if (muscle is null)
			return null;
		foreach (var row in MuscleIds.Muscles)
		{
			if (row.Id == muscle)
				return GroupFile(row.GroupId);
		}
		return null;
	}
}
