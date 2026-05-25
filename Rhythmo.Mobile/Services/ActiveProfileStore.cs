namespace Rhythmo.Mobile.Services;

/// <summary>Utilisateur connecté (identifiant Supabase = auth.users.id).</summary>
public sealed class ActiveProfileStore
{
	private Guid? _userId;

	public bool IsAuthenticated => _userId.HasValue;

	public void Set(Guid userId) => _userId = userId;

	public void Clear() => _userId = null;

	public Guid Get() =>
		_userId ?? throw new InvalidOperationException("Aucun utilisateur connecté.");
}
