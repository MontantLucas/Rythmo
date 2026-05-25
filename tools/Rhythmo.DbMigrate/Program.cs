using Npgsql;

static string? Env(string name) =>
	Environment.GetEnvironmentVariable(name);

static void LoadEnvFile()
{
	var dir = new DirectoryInfo(AppContext.BaseDirectory);
	for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
	{
		var path = Path.Combine(dir.FullName, ".env");
		if (!File.Exists(path))
			continue;
		foreach (var raw in File.ReadAllLines(path))
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			var eq = line.IndexOf('=');
			if (eq <= 0)
				continue;
			Environment.SetEnvironmentVariable(line[..eq].Trim(), line[(eq + 1)..].Trim().Trim('"'));
		}

		return;
	}
}

LoadEnvFile();

var refId = Env("SUPABASE_PROJECT_REF") ?? "lvmqvylradoimaeckwpj";
var password = Env("SUPABASE_DB_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
{
	Console.Error.WriteLine("SUPABASE_DB_PASSWORD manquant dans .env");
	return 1;
}

var host = Env("SUPABASE_DB_HOST") ?? $"db.{refId}.supabase.co";
var port = Env("SUPABASE_DB_PORT") ?? "5432";
var user = Env("SUPABASE_DB_USER") ?? "postgres";
var database = Env("SUPABASE_DB_NAME") ?? "postgres";

var builder = new NpgsqlConnectionStringBuilder
{
	Host = host,
	Port = int.Parse(port),
	Username = user,
	Password = password,
	Database = database,
	SslMode = SslMode.Require,
	TrustServerCertificate = true
};

var migrationsDir = Env("SUPABASE_MIGRATIONS_DIR");
if (string.IsNullOrWhiteSpace(migrationsDir))
{
	var dir = new DirectoryInfo(AppContext.BaseDirectory);
	for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
	{
		var candidate = Path.Combine(dir.FullName, "supabase", "migrations");
		if (Directory.Exists(candidate))
		{
			migrationsDir = candidate;
			break;
		}
	}
}

if (string.IsNullOrWhiteSpace(migrationsDir) || !Directory.Exists(migrationsDir))
{
	Console.Error.WriteLine("Dossier supabase/migrations introuvable.");
	return 1;
}

var files = Directory.GetFiles(migrationsDir, "*.sql").OrderBy(static f => f).ToList();
if (files.Count == 0)
{
	Console.WriteLine("Aucune migration .sql.");
	return 0;
}

await using var conn = new NpgsqlConnection(builder.ConnectionString);
await conn.OpenAsync();

await using (var ensure = new NpgsqlCommand(
	             """
	             create table if not exists public.schema_migrations (
	               filename text primary key,
	               applied_at timestamptz not null default now()
	             );
	             """, conn))
	await ensure.ExecuteNonQueryAsync();

var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await using (var list = new NpgsqlCommand("select filename from public.schema_migrations", conn))
await using (var reader = await list.ExecuteReaderAsync())
{
	while (await reader.ReadAsync())
		applied.Add(reader.GetString(0));
}

foreach (var file in files)
{
	var name = Path.GetFileName(file);
	if (applied.Contains(name))
	{
		Console.WriteLine($"skip {name}");
		continue;
	}

	Console.WriteLine($"apply {name}...");
	var sql = await File.ReadAllTextAsync(file);
	try
	{
		await using var batch = new NpgsqlCommand(sql, conn);
		await batch.ExecuteNonQueryAsync();
	}
	catch (PostgresException ex) when (ex.SqlState is "42710" or "42P07")
	{
		Console.WriteLine($"  (déjà présent: {ex.MessageText})");
	}

	await using var mark = new NpgsqlCommand(
		"insert into public.schema_migrations (filename) values (@f)", conn);
	mark.Parameters.AddWithValue("f", name);
	await mark.ExecuteNonQueryAsync();
}

Console.WriteLine("Migrations OK.");
return 0;
