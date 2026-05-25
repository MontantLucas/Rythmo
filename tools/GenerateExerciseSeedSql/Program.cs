using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rhythmo.Mobile.Data; // linked sources

var rows = LocalExerciseSeed.CreateRows();
var sb = new StringBuilder();
sb.AppendLine("-- Généré par tools/GenerateExerciseSeedSql — ne pas éditer à la main.");
sb.AppendLine("insert into public.exercises (id, name_fr, category, met_approx, is_builtin, created_by) values");
for (var i = 0; i < rows.Length; i++)
{
	var r = rows[i];
	var cat = r.Category?.Replace("'", "''") ?? "";
	var name = r.NameFr.Replace("'", "''");
	var comma = i == 0 ? "  " : " ,";
	sb.Append(comma).Append('(')
		.Append('\'').Append(r.Id).Append("', ")
		.Append('\'').Append(name).Append("', ")
		.Append('\'').Append(cat).Append("', ")
		.Append(r.MetApprox.ToString(CultureInfo.InvariantCulture))
		.Append(", true, null)\n");
}

sb.AppendLine("on conflict (id) do nothing;");
var outPath = Path.GetFullPath(Path.Combine(args[0], "supabase", "migrations", "004_profiles_age_and_exercise_seed.sql"));
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var header = """
-- Profil : policy INSERT + âge
alter table public.profiles add column if not exists age_years smallint;

drop policy if exists profiles_insert_own on public.profiles;
create policy profiles_insert_own on public.profiles
  for insert to authenticated
  with check (auth.uid() = id);

""";
File.WriteAllText(outPath, header + sb, Encoding.UTF8);
Console.WriteLine($"Wrote {outPath} ({rows.Length} exercises)");
