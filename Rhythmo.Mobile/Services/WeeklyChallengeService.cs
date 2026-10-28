using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Challenges;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Services;

public sealed record ChallengeCardModel(
	Guid AssignmentId,
	int Slot,
	ChallengeFamily Family,
	string Title,
	string Detail,
	ChallengeStatus Status,
	bool CanStart,
	bool CanAbandon,
	int Points,
	string? DistanceLabel);

public sealed record ChallengeBoard(int Score, IReadOnlyList<ChallengeCardModel> Cards);

public sealed class WeeklyChallengeService
{
	public async Task<ChallengeBoard> LoadAsync(IRhythmoRepository repo, Guid profileId, CancellationToken ct = default)
	{
		var zone = TimeZoneInfo.Local;
		var today = DateOnly.FromDateTime(DateTime.Now);
		var week = IsoWeek.Of(today);
		var expires = week.EndUtc(zone);
		var existing = await repo.ListChallengeAssignmentsAsync(profileId, week.Year, week.Week, ct).ConfigureAwait(false);
		foreach (var slot in WeeklyChallengeCatalog.Slots)
		{
			if (existing.Any(a => a.Slot == slot.Slot))
				continue;
			await repo.InsertChallengeAssignmentAsync(new WeeklyChallengeAssignmentRow
			{
				ProfileId = profileId,
				IsoYear = week.Year,
				IsoWeek = week.Week,
				Slot = slot.Slot,
				TemplateId = WeeklyChallengeCatalog.TemplateId(slot.Slot),
				Status = "offered",
				ExpiresUtc = expires
			}, ct).ConfigureAwait(false);
		}

		var assignments = await repo.ListChallengeAssignmentsAsync(profileId, week.Year, week.Week, ct).ConfigureAwait(false);
		var ctx = await Context(repo, profileId, ct).ConfigureAwait(false);
		var awards = await repo.ListChallengeAwardsAsync(profileId, ct).ConfigureAwait(false);
		var awardById = awards.ToDictionary(a => a.AssignmentId);
		var names = (await repo.ListExercisesAsync(ct).ConfigureAwait(false)).ToDictionary(e => e.Id, e => e.NameFr);

		var cards = new List<ChallengeCardModel>();
		foreach (var assignment in assignments.OrderBy(a => a.Slot))
		{
			var family = WeeklyChallengeCatalog.FamilyOf(assignment.TemplateId);
			var status = WeeklyChallengeCatalog.ParseStatus(assignment.Status);
			FrozenTarget? target = string.IsNullOrWhiteSpace(assignment.TargetJson)
				? null
				: FrozenTargetJson.Read(assignment.TargetJson);
			awardById.TryGetValue(assignment.Id, out var award);
			var plan = ChallengeEvaluator.Decide(
				family, target, status, award?.Points, assignment.StartedUtc, assignment.ExpiresUtc, ctx);
			if (plan.WriteAward)
			{
				if (award is null)
				{
					var inserted = await repo.TryInsertChallengeAwardAsync(new ChallengePointAwardRow
					{
						AssignmentId = assignment.Id,
						ProfileId = profileId,
						Points = plan.Points,
						AwardedUtc = DateTime.UtcNow
					}, ct).ConfigureAwait(false);
					if (!inserted)
						plan = plan with { AlreadyAwarded = true, WriteAward = false };
				}
				else
					await repo.UpdateChallengeAwardAsync(assignment.Id, plan.Points, ct).ConfigureAwait(false);
			}
			if (plan.NextStatus != status)
			{
				await repo.UpdateChallengeProgressAsync(
					assignment.Id, WeeklyChallengeCatalog.StatusName(plan.NextStatus), null, null, ct).ConfigureAwait(false);
				status = plan.NextStatus;
			}

			var preview = target ?? ChallengeTargets.TryBuild(
				family, WeeklyChallengeCatalog.StrengthIndex(assignment.Slot), ctx.NowUtc, ctx);
			var canStart = status == ChallengeStatus.Offered && preview is not null;
			var canAbandon = status == ChallengeStatus.Active && (award is null || family == ChallengeFamily.RunningDistance);
			string? distance = null;
			if (family == ChallengeFamily.RunningDistance && assignment.StartedUtc is { } started)
			{
				var measured = ChallengeEvaluator.Measure(family, target ?? preview!, started, assignment.ExpiresUtc, ctx);
				distance = $"{measured.DistanceM / 1000d:0.#} km";
			}
			cards.Add(new ChallengeCardModel(
				assignment.Id,
				assignment.Slot,
				family,
				ChallengeText.Title(family),
				Describe(target ?? (canStart ? preview : null), names),
				status,
				canStart,
				canAbandon,
				plan.Points,
				distance));
		}

		var score = (await repo.ListChallengeAwardsAsync(profileId, ct).ConfigureAwait(false)).Sum(a => a.Points);
		return new ChallengeBoard(score, cards);
	}

	public async Task StartAsync(IRhythmoRepository repo, Guid profileId, Guid assignmentId, CancellationToken ct = default)
	{
		var board = await Locate(repo, profileId, assignmentId, ct).ConfigureAwait(false);
		if (board is null)
			return;
		var (assignment, ctx) = board.Value;
		var family = WeeklyChallengeCatalog.FamilyOf(assignment.TemplateId);
		var status = WeeklyChallengeCatalog.ParseStatus(assignment.Status);
		if (status != ChallengeStatus.Offered)
			return;
		var targetJson = assignment.TargetJson;
		DateTime? started = assignment.StartedUtc;
		if (string.IsNullOrWhiteSpace(targetJson))
		{
			var built = ChallengeTargets.TryBuild(
				family, WeeklyChallengeCatalog.StrengthIndex(assignment.Slot), DateTime.UtcNow, ctx);
			if (built is null)
				return;
			targetJson = FrozenTargetJson.Write(built);
			started = DateTime.UtcNow;
		}
		await repo.UpdateChallengeProgressAsync(assignment.Id, "active", targetJson, started, ct).ConfigureAwait(false);
	}

	public async Task AbandonAsync(IRhythmoRepository repo, Guid profileId, Guid assignmentId, CancellationToken ct = default)
	{
		var found = await Locate(repo, profileId, assignmentId, ct).ConfigureAwait(false);
		if (found is null)
			return;
		var assignment = found.Value.Assignment;
		if (WeeklyChallengeCatalog.ParseStatus(assignment.Status) != ChallengeStatus.Active)
			return;
		await repo.UpdateChallengeProgressAsync(assignment.Id, "offered", null, null, ct).ConfigureAwait(false);
	}

	private static async Task<(WeeklyChallengeAssignmentRow Assignment, ChallengeContext Context)?> Locate(
		IRhythmoRepository repo, Guid profileId, Guid assignmentId, CancellationToken ct)
	{
		var today = DateOnly.FromDateTime(DateTime.Now);
		var week = IsoWeek.Of(today);
		var rows = await repo.ListChallengeAssignmentsAsync(profileId, week.Year, week.Week, ct).ConfigureAwait(false);
		var row = rows.FirstOrDefault(a => a.Id == assignmentId);
		if (row is null)
			return null;
		return (row, await Context(repo, profileId, ct).ConfigureAwait(false));
	}

	private static async Task<ChallengeContext> Context(IRhythmoRepository repo, Guid profileId, CancellationToken ct)
	{
		var since = DateTime.UtcNow.AddDays(-120);
		var workouts = await repo.ListCompletedWorkoutsSinceAsync(profileId, since, ct).ConfigureAwait(false);
		var cardio = await repo.ListCardioActivitiesSinceAsync(profileId, since, ct).ConfigureAwait(false);
		var groups = await repo.ListGroupRankSnapshotsAsync(profileId, MuscleIds.StandardVersionId, ct).ConfigureAwait(false);
		var bests = await repo.ListExercisePersonalBestsAsync(profileId, ct).ConfigureAwait(false);
		var exercises = await repo.ListExercisesAsync(ct).ConfigureAwait(false);
		var byId = exercises.ToDictionary(e => e.Id);
		return new ChallengeContext
		{
			NowUtc = DateTime.UtcNow,
			Zone = TimeZoneInfo.Local,
			Workouts = workouts.Select(HistoryMapping.ToLogged).ToList(),
			Cardio = cardio.Select(c => new LoggedCardio(c.PerformedUtc, c.DistanceM, c.DurationS)).ToList(),
			Groups = groups.Select(g => (g.GroupId, g.ValidatedRank)).ToList(),
			Bests = bests.Select(b => (b.ExerciseId, b.MaxKg, b.UpdatedUtc)).ToList(),
			PrimaryGroup = id => GroupOf(id, byId),
			IsPrimaryFor = (id, group) => GroupOf(id, byId) == group
		};
	}

	private static string? GroupOf(Guid id, IReadOnlyDictionary<Guid, CachedExerciseRow> byId)
	{
		if (!byId.TryGetValue(id, out var row))
			return null;
		var def = MuscleRankingCatalog.Find(row.Category ?? "", row.NameFr);
		var muscle = def?.PrimaryMuscles.FirstOrDefault();
		if (muscle is null)
			return null;
		foreach (var item in MuscleIds.Muscles)
		{
			if (item.Id == muscle)
				return item.GroupId;
		}
		return null;
	}

	private static string Describe(FrozenTarget? target, IReadOnlyDictionary<Guid, string> names)
	{
		if (target is null)
			return ChallengeText.LockedReason(ChallengeFamily.Strength);
		return target.Family switch
		{
			ChallengeFamily.Strength =>
				$"{Name(target.ExerciseId, names)} · 3 × 6–12 à {target.Number:0.#} kg",
			ChallengeFamily.SessionVolume => $"Une séance ≥ {target.Number:0} kg",
			ChallengeFamily.WeekVolume => $"Semaine ≥ {target.Number:0} kg",
			ChallengeFamily.ExerciseVariety => $"{target.Count} exercices distincts",
			ChallengeFamily.GroupVariety => $"{target.Count} groupes distincts",
			ChallengeFamily.TargetedMuscle => $"{target.Count} séries · {GroupName(target.GroupId)}",
			ChallengeFamily.Progression => $"Dépasser {Name(target.ExerciseId, names)}",
			ChallengeFamily.RunningDistance => "5 km = 10 points, au prorata",
			ChallengeFamily.CardioPace => "Au moins 1 km à l’allure figée ou plus vite",
			_ => ""
		};
	}

	private static string Name(Guid? id, IReadOnlyDictionary<Guid, string> names) =>
		id is { } value && names.TryGetValue(value, out var name) ? name : "Exercice";

	private static string GroupName(string? groupId)
	{
		foreach (var group in MuscleIds.Groups)
		{
			if (group.Id == groupId)
				return group.NameFr;
		}
		return "Groupe";
	}
}
