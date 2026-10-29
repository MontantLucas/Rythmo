using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Rhythmo.Mobile.Services;

/// <summary>
/// Garde la session sur le disque. Sans ça, tuer l'appli déconnecte, alors que le refresh token est encore valable.
/// </summary>
public sealed class AuthSessionStore : IGotrueSessionPersistence<Session>
{
	private static string FilePath =>
		Path.Combine(FileSystem.AppDataDirectory, "auth-session.json");

	public void SaveSession(Session session)
	{
		try
		{
			var json = JsonConvert.SerializeObject(session);
			File.WriteAllText(FilePath, json);
		}
		catch
		{
			// Une écriture ratée ne doit pas casser la session en mémoire.
		}
	}

	public Session? LoadSession()
	{
		try
		{
			if (!File.Exists(FilePath))
				return null;

			var session = JsonConvert.DeserializeObject<Session>(File.ReadAllText(FilePath));
			if (session is null)
				return null;

			if (session.CreatedAt.Kind == DateTimeKind.Unspecified)
				session.CreatedAt = DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc);
			return session;
		}
		catch
		{
			return null;
		}
	}

	public void DestroySession()
	{
		try
		{
			if (File.Exists(FilePath))
				File.Delete(FilePath);
		}
		catch
		{
			// Le fichier sera réécrit au prochain login.
		}
	}
}
