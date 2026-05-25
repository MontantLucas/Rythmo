using Rhythmo.Mobile.Data;
using SupabaseClient = Supabase.Client;

namespace Rhythmo.Mobile.Services;

public sealed class SupabaseRhythmoRepository(SupabaseClient client, RhythmoMemoryCache cache) : IRhythmoRepository
{
	private SupabaseClient Client => client;
	private RhythmoMemoryCache Cache => cache;

	public async Task EnsureBuiltinExercisesAsync(CancellationToken ct = default)
	{
		var existing = await Client.From<ExerciseRecord>().Select("id").Get(ct).ConfigureAwait(false);
		var have = existing.Models?.Select(e => e.Id).ToHashSet() ?? [];
		var toInsert = LocalExerciseSeed.CreateRows()
			.Where(r => !have.Contains(r.Id))
			.Select(r => new ExerciseRecord
			{
				Id = r.Id,
				NameFr = r.NameFr,
				Category = r.Category,
				MetApprox = r.MetApprox,
				IsBuiltin = true
			})
			.ToList();
		if (toInsert.Count == 0)
			return;
		await Client.From<ExerciseRecord>().Insert(toInsert, cancellationToken: ct).ConfigureAwait(false);
		Cache.InvalidateExercises();
	}

	public Task<ProfileRow?> GetProfileAsync(Guid userId, CancellationToken ct = default) =>
		Cache.GetOrLoadAsync(
			RhythmoMemoryCache.ProfileKey(userId),
			TimeSpan.FromMinutes(5),
			async c =>
			{
				var res = await Client.From<ProfileRecord>()
					.Where(p => p.Id == userId)
					.Get(c)
					.ConfigureAwait(false);
				var model = res.Models?.FirstOrDefault();
				return model is null ? null : ToProfile(model);
			},
			ct);

	public async Task<IReadOnlyList<ProfileRow>> ListCommunityProfilesAsync(CancellationToken ct = default)
	{
		var res = await Client.From<ProfileRecord>().Get(ct).ConfigureAwait(false);
		return res.Models?.Select(ToProfile).ToList() ?? [];
	}

	public async Task SaveProfileAsync(ProfileRow profile, CancellationToken ct = default)
	{
		if (Client.Auth.CurrentSession is null)
			throw new InvalidOperationException("Session expirée — reconnecte-toi.");

		if (Client.Auth.CurrentUser?.Id is { } uidStr &&
		    Guid.TryParse(uidStr, out var authId) &&
		    authId != profile.Id)
			profile.Id = authId;

		var record = new ProfileRecord
		{
			Id = profile.Id,
			DisplayName = profile.DisplayName,
			BiologicalSex = (int)profile.BiologicalSex,
			WeightKg = profile.WeightKg,
			HeightCm = profile.HeightCm,
			AgeYears = profile.AgeYears
		};

		var upserted = await Client.From<ProfileRecord>()
			.Upsert(record, cancellationToken: ct)
			.ConfigureAwait(false);

		Cache.InvalidateProfile(profile.Id);

		if (upserted.Models is { Count: > 0 })
			return;

		var saved = await GetProfileAsync(profile.Id, ct).ConfigureAwait(false);
		if (saved is null ||
		    !string.Equals(saved.DisplayName, profile.DisplayName, StringComparison.Ordinal) ||
		    Math.Abs(saved.WeightKg - profile.WeightKg) > 0.01)
		{
			throw new InvalidOperationException(
				"Le profil n'a pas été enregistré (vérifie ta connexion ou réessaie après t'être reconnecté).");
		}
	}

	public Task<IReadOnlyList<CachedExerciseRow>> ListExercisesAsync(CancellationToken ct = default) =>
		Cache.GetOrLoadAsync(
			"exercises",
			TimeSpan.FromMinutes(30),
			async c =>
			{
				var res = await Client.From<ExerciseRecord>()
					.Order(e => e.Category, Supabase.Postgrest.Constants.Ordering.Ascending)
					.Order(e => e.NameFr, Supabase.Postgrest.Constants.Ordering.Ascending)
					.Get(c)
					.ConfigureAwait(false);
				return (IReadOnlyList<CachedExerciseRow>)(res.Models?.Select(ToCached).ToList() ?? []);
			},
			ct);

	public async Task<IReadOnlyList<SessionTemplateRow>> ListSessionTemplatesAsync(Guid ownerId, CancellationToken ct = default)
	{
		var res = await Client.From<SessionTemplateRecord>()
			.Where(t => t.OwnerId == ownerId)
			.Order(t => t.UpdatedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToTemplate).ToList() ?? [];
	}

	public async Task<SessionTemplateRow?> GetSessionTemplateAsync(Guid sessionId, CancellationToken ct = default)
	{
		var res = await Client.From<SessionTemplateRecord>()
			.Where(t => t.Id == sessionId)
			.Single(ct)
			.ConfigureAwait(false);
		return res is null ? null : ToTemplate(res);
	}

	public async Task<int> CountSessionExercisesAsync(Guid sessionId, CancellationToken ct = default)
	{
		var lines = await ListSessionExercisesAsync(sessionId, ct).ConfigureAwait(false);
		return lines.Count;
	}

	public async Task<IReadOnlyList<SessionExerciseRow>> ListSessionExercisesAsync(Guid sessionId, CancellationToken ct = default)
	{
		var res = await Client.From<SessionExerciseRecord>()
			.Where(e => e.SessionId == sessionId)
			.Order(e => e.SortOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToSessionExercise).ToList() ?? [];
	}

	public async Task SaveSessionAsync(
		SessionTemplateRow template,
		IReadOnlyList<SessionExerciseRow> lines,
		bool clearSnapshot,
		CancellationToken ct = default)
	{
		await Client.From<SessionTemplateRecord>().Upsert(new SessionTemplateRecord
		{
			Id = template.Id,
			OwnerId = template.OwnerProfileId,
			Title = template.Title,
			CreatedUtc = template.CreatedUtc,
			UpdatedUtc = template.UpdatedUtc
		}, cancellationToken: ct).ConfigureAwait(false);

		if (clearSnapshot)
			await DeleteSessionSnapshotAsync(template.Id, ct).ConfigureAwait(false);

		await Client.From<SessionExerciseRecord>()
			.Where(e => e.SessionId == template.Id)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);

		if (lines.Count > 0)
		{
			await Client.From<SessionExerciseRecord>().Insert(
				lines.Select(l => new SessionExerciseRecord
				{
					Id = l.Id,
					SessionId = l.SessionId,
					ExerciseId = l.ExerciseId,
					SortOrder = l.SortOrder,
					TargetSets = l.TargetSets,
					TargetReps = l.TargetReps
				}).ToList(),
				cancellationToken: ct).ConfigureAwait(false);
		}
	}

	public async Task DeleteSessionTemplateAsync(Guid sessionId, Guid ownerId, CancellationToken ct = default)
	{
		await Client.From<SessionTemplateRecord>()
			.Where(t => t.Id == sessionId && t.OwnerId == ownerId)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	public async Task DuplicateSessionTemplateAsync(Guid sourceId, Guid ownerId, CancellationToken ct = default)
	{
		var src = await GetSessionTemplateAsync(sourceId, ct).ConfigureAwait(false);
		if (src is null || src.OwnerProfileId != ownerId)
			return;
		var lines = await ListSessionExercisesAsync(sourceId, ct).ConfigureAwait(false);
		var utc = DateTime.UtcNow;
		var newId = Guid.NewGuid();
		await SaveSessionAsync(new SessionTemplateRow
		{
			Id = newId,
			OwnerProfileId = ownerId,
			Title = src.Title.Trim() + " (copie)",
			CreatedUtc = utc,
			UpdatedUtc = utc
		}, lines.Select(l => new SessionExerciseRow
		{
			Id = Guid.NewGuid(),
			SessionId = newId,
			ExerciseId = l.ExerciseId,
			SortOrder = l.SortOrder,
			TargetSets = l.TargetSets,
			TargetReps = l.TargetReps
		}).ToList(), clearSnapshot: false, ct).ConfigureAwait(false);
	}

	public async Task<SessionLastSnapshotRow?> GetSessionSnapshotAsync(Guid sessionId, CancellationToken ct = default)
	{
		var res = await Client.From<SessionLastSnapshotRecord>()
			.Where(s => s.SessionId == sessionId)
			.Single(ct)
			.ConfigureAwait(false);
		return res is null
			? null
			: new SessionLastSnapshotRow { SessionId = res.SessionId, Json = res.Json, SavedUtc = res.SavedUtc };
	}

	public async Task UpsertSessionSnapshotAsync(SessionLastSnapshotRow snapshot, CancellationToken ct = default)
	{
		await Client.From<SessionLastSnapshotRecord>().Upsert(new SessionLastSnapshotRecord
		{
			SessionId = snapshot.SessionId,
			Json = snapshot.Json,
			SavedUtc = snapshot.SavedUtc
		}, cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task DeleteSessionSnapshotAsync(Guid sessionId, CancellationToken ct = default)
	{
		await Client.From<SessionLastSnapshotRecord>()
			.Where(s => s.SessionId == sessionId)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<CompletedWorkoutRow>> ListCompletedWorkoutsAsync(Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<CompletedWorkoutRecord>()
			.Where(c => c.ProfileId == profileId)
			.Order(c => c.CompletedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToCompleted).ToList() ?? [];
	}

	public async Task<IReadOnlyList<CompletedWorkoutRow>> ListCompletedWorkoutsSinceAsync(
		Guid profileId, DateTime sinceUtc, CancellationToken ct = default)
	{
		var res = await Client.From<CompletedWorkoutRecord>()
			.Where(c => c.ProfileId == profileId && c.CompletedUtc >= sinceUtc)
			.Order(c => c.CompletedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToCompleted).ToList() ?? [];
	}

	public async Task<CompletedWorkoutRow?> GetCompletedWorkoutAsync(Guid id, Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<CompletedWorkoutRecord>()
			.Where(c => c.Id == id && c.ProfileId == profileId)
			.Single(ct)
			.ConfigureAwait(false);
		return res is null ? null : ToCompleted(res);
	}

	public async Task<Guid> AddCompletedWorkoutAsync(CompletedWorkoutRow row, CancellationToken ct = default)
	{
		var record = new CompletedWorkoutRecord
		{
			Id = row.Id,
			ProfileId = row.ProfileId,
			CompletedUtc = row.CompletedUtc,
			CaloriesRounded = row.CaloriesRounded,
			SessionTitle = row.SessionTitle,
			SourceSessionTemplateId = row.SourceSessionTemplateId,
			PayloadJson = row.PayloadJson
		};

		var response = await Client.From<CompletedWorkoutRecord>()
			.Insert(record, cancellationToken: ct)
			.ConfigureAwait(false);

		var insertedId = response.Models?.FirstOrDefault()?.Id;
		if (insertedId is { } fromDb && fromDb != Guid.Empty)
			return fromDb;

		var verify = await GetCompletedWorkoutAsync(row.Id, row.ProfileId, ct).ConfigureAwait(false);
		if (verify is not null)
			return verify.Id;

		throw new InvalidOperationException(
			"La séance n'a pas pu être enregistrée (vérifie ta connexion ou réessaie).");
	}

	public async Task DeleteCompletedWorkoutAsync(Guid id, Guid profileId, CancellationToken ct = default)
	{
		await Client.From<CompletedWorkoutRecord>()
			.Where(c => c.Id == id && c.ProfileId == profileId)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<CompletedWorkoutRow>> GetOrphanedCompletedWorkoutsAsync(Guid profileId, CancellationToken ct = default)
	{
		var templates = await ListSessionTemplatesAsync(profileId, ct).ConfigureAwait(false);
		var idSet = templates.Select(t => t.Id).ToHashSet();
		var titles = templates
			.Select(t => (t.Title ?? "").Trim())
			.Where(x => x.Length > 0)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var completed = await ListCompletedWorkoutsAsync(profileId, ct).ConfigureAwait(false);
		var orphaned = new List<CompletedWorkoutRow>();
		foreach (var c in completed)
		{
			if (c.SourceSessionTemplateId is { } sid)
			{
				if (!idSet.Contains(sid))
					orphaned.Add(c);
			}
			else
			{
				var ttl = (c.SessionTitle ?? "").Trim();
				if (ttl.Length == 0 || !titles.Contains(ttl))
					orphaned.Add(c);
			}
		}

		return orphaned;
	}

	public async Task DeleteAllPerformanceDailyAsync(Guid profileId, CancellationToken ct = default)
	{
		await Client.From<ExercisePerformanceDailyRecord>()
			.Where(p => p.ProfileId == profileId)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	public async Task DeleteCompletedWorkoutsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
	{
		foreach (var id in ids)
		{
			await Client.From<CompletedWorkoutRecord>()
				.Where(c => c.Id == id)
				.Delete(cancellationToken: ct)
				.ConfigureAwait(false);
		}
	}

	public async Task<ExerciseLastWeightRow?> GetLastWeightAsync(Guid profileId, Guid exerciseId, CancellationToken ct = default)
	{
		var res = await Client.From<ExerciseLastWeightRecord>()
			.Where(w => w.ProfileId == profileId && w.ExerciseId == exerciseId)
			.Single(ct)
			.ConfigureAwait(false);
		return res is null
			? null
			: new ExerciseLastWeightRow
			{
				ProfileId = res.ProfileId,
				ExerciseId = res.ExerciseId,
				WeightKg = res.WeightKg,
				UpdatedUtc = res.UpdatedUtc
			};
	}

	public async Task UpsertLastWeightAsync(ExerciseLastWeightRow row, CancellationToken ct = default)
	{
		await Client.From<ExerciseLastWeightRecord>().Upsert(new ExerciseLastWeightRecord
		{
			ProfileId = row.ProfileId,
			ExerciseId = row.ExerciseId,
			WeightKg = row.WeightKg,
			UpdatedUtc = row.UpdatedUtc
		}, cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task UpsertDailyMaxKgAsync(Guid profileId, Guid exerciseId, DateOnly date, double maxKg, CancellationToken ct = default)
	{
		if (maxKg <= double.Epsilon)
			return;

		var existing = await Client.From<ExercisePerformanceDailyRecord>()
			.Where(x => x.ProfileId == profileId)
			.Where(x => x.ExerciseId == exerciseId)
			.Get(ct)
			.ConfigureAwait(false);
		var row = existing.Models?.FirstOrDefault(x => x.PerformanceDate == date);

		if (row is null)
		{
			await Client.From<ExercisePerformanceDailyRecord>().Insert(new ExercisePerformanceDailyRecord
			{
				ProfileId = profileId,
				ExerciseId = exerciseId,
				PerformanceDate = date,
				MaxWeightKg = maxKg
			}, cancellationToken: ct).ConfigureAwait(false);
			return;
		}

		if (maxKg > row.MaxWeightKg)
		{
			row.MaxWeightKg = maxKg;
			await Client.From<ExercisePerformanceDailyRecord>().Upsert(row, cancellationToken: ct).ConfigureAwait(false);
		}
	}

	public async Task<IReadOnlyList<ExercisePerformanceDailyRow>> ListPerformanceDailyAsync(
		Guid profileId, Guid exerciseId, CancellationToken ct = default)
	{
		var res = await Client.From<ExercisePerformanceDailyRecord>()
			.Where(p => p.ProfileId == profileId && p.ExerciseId == exerciseId)
			.Order(p => p.PerformanceDate, Supabase.Postgrest.Constants.Ordering.Ascending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(p => new ExercisePerformanceDailyRow
		{
			ProfileId = p.ProfileId,
			ExerciseId = p.ExerciseId,
			PerformanceDate = p.PerformanceDate,
			MaxWeightKg = p.MaxWeightKg
		}).ToList() ?? [];
	}

	public async Task<IReadOnlyList<Guid>> ListExercisesWithPerformanceAsync(Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<ExercisePerformanceDailyRecord>()
			.Where(p => p.ProfileId == profileId)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(p => p.ExerciseId).Distinct().ToList() ?? [];
	}

	public async Task<ExercisePersonalBestRow?> GetExercisePersonalBestAsync(
		Guid profileId, Guid exerciseId, CancellationToken ct = default)
	{
		var res = await Client.From<ExercisePersonalBestRecord>()
			.Where(b => b.ProfileId == profileId && b.ExerciseId == exerciseId)
			.Single(ct)
			.ConfigureAwait(false);
		return res is null ? null : ToPersonalBest(res);
	}

	public async Task UpsertExercisePersonalBestAsync(ExercisePersonalBestRow row, CancellationToken ct = default)
	{
		await Client.From<ExercisePersonalBestRecord>().Upsert(new ExercisePersonalBestRecord
		{
			ProfileId = row.ProfileId,
			ExerciseId = row.ExerciseId,
			MaxKg = row.MaxKg,
			MaxReps = row.MaxReps,
			MaxSessionVolume = row.MaxSessionVolume,
			UpdatedUtc = row.UpdatedUtc
		}, cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task InsertPrEventAsync(PrEventRow row, CancellationToken ct = default)
	{
		await Client.From<PrEventRecord>().Insert(new PrEventRecord
		{
			Id = row.Id,
			ProfileId = row.ProfileId,
			ExerciseId = row.ExerciseId,
			Kind = row.Kind,
			WeightKg = row.WeightKg,
			Reps = row.Reps,
			PerformanceLine = row.PerformanceLine,
			CompletedWorkoutId = row.CompletedWorkoutId,
			AchievedUtc = row.AchievedUtc
		}, cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<PrEventRow>> ListRecentPrEventsAsync(int limit = 24, CancellationToken ct = default)
	{
		var res = await Client.From<PrEventRecord>()
			.Order(e => e.AchievedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Limit(limit)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToPrEvent).ToList() ?? [];
	}

	public async Task<IReadOnlyList<PrEventRow>> ListPrEventsAsync(CancellationToken ct = default)
	{
		var res = await Client.From<PrEventRecord>()
			.Order(e => e.AchievedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToPrEvent).ToList() ?? [];
	}

	public async Task<IReadOnlyList<ImportableUserRow>> ListImportableUsersAsync(CancellationToken ct = default)
	{
		var session = Client.Auth.CurrentSession
			?? throw new InvalidOperationException("Non connecté.");
		var me = Guid.Parse(session.User!.Id!);
		var profiles = await ListCommunityProfilesAsync(ct).ConfigureAwait(false);
		return profiles
			.Where(p => p.Id != me)
			.Select(p => new ImportableUserRow
			{
				UserId = p.Id,
				DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? "Utilisateur" : p.DisplayName
			})
			.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public async Task<IReadOnlyList<SessionTemplateRow>> ListSessionTemplatesByOwnerAsync(
		Guid ownerId, CancellationToken ct = default)
	{
		var res = await Client.From<SessionTemplateRecord>()
			.Where(t => t.OwnerId == ownerId)
			.Order(t => t.UpdatedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToTemplate).ToList() ?? [];
	}

	public async Task ImportSessionTemplateAsync(
		Guid sourceSessionId, Guid targetOwnerId, CancellationToken ct = default)
	{
		var src = await GetSessionTemplateAsync(sourceSessionId, ct).ConfigureAwait(false);
		if (src is null)
			throw new InvalidOperationException("Séance introuvable ou non accessible.");

		var lines = await ListSessionExercisesAsync(sourceSessionId, ct).ConfigureAwait(false);
		if (lines.Count == 0)
			throw new InvalidOperationException("Cette séance ne contient aucun exercice.");

		var utc = DateTime.UtcNow;
		var newId = Guid.NewGuid();
		var title = src.Title.Trim();
		if (!title.Contains("(importé)", StringComparison.OrdinalIgnoreCase))
			title += " (importé)";

		await SaveSessionAsync(new SessionTemplateRow
		{
			Id = newId,
			OwnerProfileId = targetOwnerId,
			Title = title,
			CreatedUtc = utc,
			UpdatedUtc = utc
		}, lines.Select(l => new SessionExerciseRow
		{
			Id = Guid.NewGuid(),
			SessionId = newId,
			ExerciseId = l.ExerciseId,
			SortOrder = l.SortOrder,
			TargetSets = l.TargetSets,
			TargetReps = l.TargetReps
		}).ToList(), clearSnapshot: false, ct).ConfigureAwait(false);
	}

	private static ProfileRow ToProfile(ProfileRecord p) => new()
	{
		Id = p.Id,
		DisplayName = p.DisplayName,
		BiologicalSex = (BiologicalSex)p.BiologicalSex,
		WeightKg = p.WeightKg,
		HeightCm = p.HeightCm,
		AgeYears = p.AgeYears
	};

	private static CachedExerciseRow ToCached(ExerciseRecord e) => new()
	{
		Id = e.Id,
		NameFr = e.NameFr,
		Category = e.Category,
		MetApprox = e.MetApprox
	};

	private static SessionTemplateRow ToTemplate(SessionTemplateRecord t) => new()
	{
		Id = t.Id,
		OwnerProfileId = t.OwnerId,
		Title = t.Title,
		CreatedUtc = t.CreatedUtc,
		UpdatedUtc = t.UpdatedUtc
	};

	private static SessionExerciseRow ToSessionExercise(SessionExerciseRecord e) => new()
	{
		Id = e.Id,
		SessionId = e.SessionId,
		ExerciseId = e.ExerciseId,
		SortOrder = e.SortOrder,
		TargetSets = e.TargetSets,
		TargetReps = e.TargetReps
	};

	private static CompletedWorkoutRow ToCompleted(CompletedWorkoutRecord c) => new()
	{
		Id = c.Id,
		ProfileId = c.ProfileId,
		CompletedUtc = c.CompletedUtc,
		CaloriesRounded = c.CaloriesRounded,
		SessionTitle = c.SessionTitle,
		SourceSessionTemplateId = c.SourceSessionTemplateId,
		PayloadJson = c.PayloadJson
	};

	private static ExercisePersonalBestRow ToPersonalBest(ExercisePersonalBestRecord b) => new()
	{
		ProfileId = b.ProfileId,
		ExerciseId = b.ExerciseId,
		MaxKg = b.MaxKg,
		MaxReps = b.MaxReps,
		MaxSessionVolume = b.MaxSessionVolume,
		UpdatedUtc = b.UpdatedUtc
	};

	private static PrEventRow ToPrEvent(PrEventRecord e) => new()
	{
		Id = e.Id,
		ProfileId = e.ProfileId,
		ExerciseId = e.ExerciseId,
		Kind = e.Kind,
		WeightKg = e.WeightKg,
		Reps = e.Reps,
		PerformanceLine = e.PerformanceLine,
		CompletedWorkoutId = e.CompletedWorkoutId,
		AchievedUtc = e.AchievedUtc
	};
}
