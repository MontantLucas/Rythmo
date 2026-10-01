using System.Globalization;
using System.Text;
using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Ranking;

var rows = LocalExerciseSeed.CreateRows().ToDictionary(r => (r.Category ?? "", r.NameFr));
var sb = new StringBuilder();
var inv = CultureInfo.InvariantCulture;

sb.AppendLine("-- Exercices ranking V1 (insert idempotent).");
sb.AppendLine("insert into public.exercises (id, name_fr, category, met_approx, is_builtin, created_by) values");
var i = 0;
foreach (var row in LocalExerciseSeed.CreateRows())
{
	var comma = i++ == 0 ? "  " : " ,";
	sb.Append(comma).Append("('").Append(row.Id).Append("', '")
		.Append(Esc(row.NameFr)).Append("', '")
		.Append(Esc(row.Category ?? "")).Append("', ")
		.Append(row.MetApprox.ToString(inv))
		.AppendLine(", true, null)");
}
sb.AppendLine("on conflict (id) do nothing;");
sb.AppendLine();

sb.AppendLine("insert into public.exercise_muscle_roles (exercise_id, muscle_id, role) values");
i = 0;
foreach (var def in MuscleRankingCatalog.All)
{
	if (!rows.TryGetValue((def.Category, def.NameFr), out var ex))
		throw new InvalidOperationException($"Missing seed exercise {def.Category}|{def.NameFr}");
	foreach (var m in def.PrimaryMuscles)
		AppendRole(ref i, ex.Id, m, "primary");
	foreach (var m in def.SecondaryMuscles)
		AppendRole(ref i, ex.Id, m, "secondary");
}
sb.AppendLine("on conflict (exercise_id, muscle_id) do nothing;");
sb.AppendLine();

sb.AppendLine("insert into public.exercise_rank_standards (");
sb.AppendLine("  version_id, exercise_id, measurement_type, load_mode, comparability, source, confidence,");
sb.AppendLine("  r10_male_70, r10_male_100, r10_female_60, r10_female_90) values");
i = 0;
foreach (var def in MuscleRankingCatalog.All.Where(x => x.IsClassifying))
{
	var ex = rows[(def.Category, def.NameFr)];
	var comma = i++ == 0 ? "  " : " ,";
	sb.Append(comma).Append("('rhythmo-v1', '").Append(ex.Id).Append("', '")
		.Append(Measure(def.MeasurementType!.Value)).Append("', '")
		.Append(Load(def.LoadMode)).Append("', '")
		.Append(def.Comparability.ToString().ToLowerInvariant()).Append("', '")
		.Append(Source(def.Source!.Value)).Append("', '")
		.Append(def.Confidence).Append("', ")
		.Append(def.R10Male70!.Value.ToString(inv)).Append(", ")
		.Append(def.R10Male100!.Value.ToString(inv)).Append(", ")
		.Append(def.R10Female60!.Value.ToString(inv)).Append(", ")
		.Append(def.R10Female90!.Value.ToString(inv)).AppendLine(")");
}
sb.AppendLine("on conflict (version_id, exercise_id) do nothing;");

var outPath = args.Length > 0 ? args[0] : "ranking_seed.sql";
File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Wrote {outPath}");

void AppendRole(ref int n, Guid id, string muscle, string role)
{
	var comma = n++ == 0 ? "  " : " ,";
	sb.Append(comma).Append("('").Append(id).Append("', '").Append(muscle).Append("', '")
		.Append(role).AppendLine("')");
}

static string Esc(string s) => s.Replace("'", "''");
static string Measure(MeasurementType t) => t switch
{
	MeasurementType.OneRm => "1rm",
	MeasurementType.FiveRm => "5rm",
	_ => "10rm"
};
static string Load(LoadMode m) => m switch
{
	LoadMode.PerDumbbell => "per_dumbbell",
	LoadMode.AddedLoad => "added_load",
	_ => "total"
};
static string Source(StandardSource s) => s switch
{
	StandardSource.RhythmoV1Manual => "rhythmo_v1_manual",
	StandardSource.RhythmoEstimated => "rhythmo_estimated",
	_ => "strength_level"
};
