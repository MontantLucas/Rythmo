namespace Rhythmo.Mobile.Services;

public sealed class SupabaseBootstrap(IRhythmoRepository repo, SupabaseAuthService auth)
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _completed;

	public async Task EnsureAsync(CancellationToken ct = default)
	{
		if (_completed)
			return;

		await _gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			if (_completed || !auth.IsSignedIn || auth.CurrentUserId is not { } uid)
				return;

			if (await repo.GetProfileAsync(uid, ct).ConfigureAwait(false) is null)
			{
				await repo.SaveProfileAsync(new Data.ProfileRow
				{
					Id = uid,
					DisplayName = "Athlète",
					BiologicalSex = Data.BiologicalSex.Male,
					WeightKg = 75
				}, ct).ConfigureAwait(false);
			}

			await repo.EnsureBuiltinExercisesAsync(ct).ConfigureAwait(false);
			_completed = true;
		}
		catch (Exception ex)
		{
			Diagnostics.CrashLogWriter.TryAppend(nameof(SupabaseBootstrap), ex);
		}
		finally
		{
			_gate.Release();
		}
	}
}
