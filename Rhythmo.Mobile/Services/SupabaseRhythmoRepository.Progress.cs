using Rhythmo.Mobile.Data;

namespace Rhythmo.Mobile.Services;

public sealed partial class SupabaseRhythmoRepository
{
	public async Task<IReadOnlyList<PrEventRow>> ListPrEventsForProfileAsync(
		Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<PrEventRecord>()
			.Where(e => e.ProfileId == profileId)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToPrEvent).ToList() ?? [];
	}

	public async Task<IReadOnlyList<ExercisePersonalBestRow>> ListExercisePersonalBestsAsync(
		Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<ExercisePersonalBestRecord>()
			.Where(b => b.ProfileId == profileId)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToPersonalBest).ToList() ?? [];
	}

	public async Task<IReadOnlyList<CardioActivityRow>> ListCardioActivitiesSinceAsync(
		Guid profileId, DateTime sinceUtc, CancellationToken ct = default)
	{
		var res = await Client.From<CardioActivityRecord>()
			.Where(c => c.ProfileId == profileId && c.PerformedUtc >= sinceUtc)
			.Order(c => c.PerformedUtc, Supabase.Postgrest.Constants.Ordering.Descending)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(c => new CardioActivityRow
		{
			Id = c.Id,
			ProfileId = c.ProfileId,
			PerformedUtc = c.PerformedUtc,
			DistanceM = c.DistanceM,
			DurationS = c.DurationS
		}).ToList() ?? [];
	}

	public async Task AddCardioActivityAsync(CardioActivityRow row, CancellationToken ct = default)
	{
		await Client.From<CardioActivityRecord>().Insert(new CardioActivityRecord
		{
			Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id,
			ProfileId = row.ProfileId,
			PerformedUtc = row.PerformedUtc,
			DistanceM = row.DistanceM,
			DurationS = row.DurationS
		}, cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task DeleteCardioActivityAsync(Guid id, Guid profileId, CancellationToken ct = default)
	{
		await Client.From<CardioActivityRecord>()
			.Where(c => c.Id == id && c.ProfileId == profileId)
			.Delete(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<WeeklyChallengeAssignmentRow>> ListChallengeAssignmentsAsync(
		Guid profileId, int isoYear, int isoWeek, CancellationToken ct = default)
	{
		var res = await Client.From<WeeklyChallengeAssignmentRecord>()
			.Where(a => a.ProfileId == profileId && a.IsoYear == isoYear && a.IsoWeek == isoWeek)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(ToAssignment).ToList() ?? [];
	}

	public async Task InsertChallengeAssignmentAsync(WeeklyChallengeAssignmentRow row, CancellationToken ct = default)
	{
		try
		{
			await Client.From<WeeklyChallengeAssignmentRecord>()
				.Insert(FromAssignment(row), cancellationToken: ct)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (IsUniqueViolation(ex))
		{
		}
	}

	public async Task UpdateChallengeProgressAsync(
		Guid assignmentId,
		string status,
		string? targetJson,
		DateTime? startedUtc,
		CancellationToken ct = default)
	{
		var table = Client.From<WeeklyChallengeAssignmentRecord>()
			.Where(a => a.Id == assignmentId)
			.Set(a => a.Status, status);
		if (targetJson is not null)
			table = table.Set(a => a.TargetJson!, targetJson);
		if (startedUtc is not null)
			table = table.Set(a => a.StartedUtc!, startedUtc.Value);
		await table.Update(cancellationToken: ct).ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<ChallengePointAwardRow>> ListChallengeAwardsAsync(
		Guid profileId, CancellationToken ct = default)
	{
		var res = await Client.From<ChallengePointAwardRecord>()
			.Where(a => a.ProfileId == profileId)
			.Get(ct)
			.ConfigureAwait(false);
		return res.Models?.Select(a => new ChallengePointAwardRow
		{
			AssignmentId = a.AssignmentId,
			ProfileId = a.ProfileId,
			Points = a.Points,
			AwardedUtc = a.AwardedUtc
		}).ToList() ?? [];
	}

	public async Task<bool> TryInsertChallengeAwardAsync(ChallengePointAwardRow row, CancellationToken ct = default)
	{
		try
		{
			await Client.From<ChallengePointAwardRecord>().Insert(new ChallengePointAwardRecord
			{
				AssignmentId = row.AssignmentId,
				ProfileId = row.ProfileId,
				Points = row.Points,
				AwardedUtc = row.AwardedUtc
			}, cancellationToken: ct).ConfigureAwait(false);
			return true;
		}
		catch (Exception ex) when (IsUniqueViolation(ex))
		{
			return false;
		}
	}

	public async Task UpdateChallengeAwardAsync(Guid assignmentId, int points, CancellationToken ct = default)
	{
		await Client.From<ChallengePointAwardRecord>()
			.Where(a => a.AssignmentId == assignmentId)
			.Set(a => a.Points, points)
			.Set(a => a.AwardedUtc, DateTime.UtcNow)
			.Update(cancellationToken: ct)
			.ConfigureAwait(false);
	}

	private static WeeklyChallengeAssignmentRow ToAssignment(WeeklyChallengeAssignmentRecord a) => new()
	{
		Id = a.Id,
		ProfileId = a.ProfileId,
		IsoYear = a.IsoYear,
		IsoWeek = a.IsoWeek,
		Slot = a.Slot,
		TemplateId = a.TemplateId,
		TargetJson = a.TargetJson,
		Status = a.Status,
		StartedUtc = a.StartedUtc,
		ExpiresUtc = a.ExpiresUtc
	};

	private static WeeklyChallengeAssignmentRecord FromAssignment(WeeklyChallengeAssignmentRow a) => new()
	{
		Id = a.Id,
		ProfileId = a.ProfileId,
		IsoYear = a.IsoYear,
		IsoWeek = a.IsoWeek,
		Slot = a.Slot,
		TemplateId = a.TemplateId,
		TargetJson = a.TargetJson,
		Status = a.Status,
		StartedUtc = a.StartedUtc,
		ExpiresUtc = a.ExpiresUtc
	};

	private static bool IsUniqueViolation(Exception ex)
	{
		var text = ex.ToString();
		return text.Contains("23505", StringComparison.Ordinal) ||
		       text.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
	}
}
