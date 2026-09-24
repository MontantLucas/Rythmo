using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Maui.Storage;
using Rhythmo.Mobile.Data;
using Rhythmo.Mobile.Diagnostics;
using Rhythmo.Mobile.Infrastructure;
using Rhythmo.Mobile.Social;
using Rhythmo.Mobile.Theme;
using Rhythmo.Shared.Contracts;
using Rhythmo.Shared.Ranking;

namespace Rhythmo.Mobile.Services;

public enum WorkoutFinalizePhase
{
	Saving,
	Saved,
	Failed
}

public sealed class WorkoutFinalizeUpdate
{
	public required WorkoutFinalizePhase Phase { get; init; }
	public required string Message { get; init; }
	public Guid? WorkoutId { get; init; }
	public bool NeedsReauth { get; init; }
	internal IReadOnlyList<QuestUnlockItem> Unlocks { get; init; } = [];
}

/// <summary>Séance prête à être enregistrée, persistée tant que l'envoi n'est pas terminé.</summary>
public sealed class PendingWorkoutFinalize
{
	public Guid WorkoutId { get; set; }
	public Guid ProfileId { get; set; }
	public DateTime CompletedUtc { get; set; }
	public DateOnly PerformanceLocalDate { get; set; }
	public string SessionTitle { get; set; } = "";
	public bool IsAdHoc { get; set; }
	public Guid SessionId { get; set; }
	public double Calories { get; set; }
	public double Minutes { get; set; }
	public int TotalFilledSets { get; set; }
	public bool WorkoutInserted { get; set; }
	public bool PersonalRecordsDone { get; set; }
	public List<PendingExerciseStat> Stats { get; set; } = [];
	public List<CompletedExerciseSetsDto> Exercises { get; set; } = [];
}

public sealed class PendingExerciseStat
{
	public Guid ExerciseId { get; set; }
	public double MaxKg { get; set; }
	public double LastKg { get; set; }
}

/// <summary>
/// Enregistre la séance hors de l'écran en cours, pour que l'utilisateur puisse continuer à naviguer.
/// Le payload est écrit sur disque avant le retour, afin de pouvoir reprendre après un crash.
/// </summary>
public sealed class WorkoutFinalizeService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false
	};

	private static readonly JsonSerializerOptions SnapshotJson = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		WriteIndented = false
	};

	private readonly IRhythmoRepository _repo;
	private readonly PersonalRecordService _personalRecords;
	private readonly MuscleRankingService _ranking;
	private readonly SocialHubService _social;
	private readonly SupabaseAuthService _auth;
	private readonly Lock _fileLock = new();
	private readonly Lock _queueLock = new();
	private readonly HashSet<Guid> _queued = [];
	private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
	private int _workerStarted;

	public WorkoutFinalizeService(
		IRhythmoRepository repo,
		PersonalRecordService personalRecords,
		MuscleRankingService ranking,
		SocialHubService social,
		SupabaseAuthService auth)
	{
		_repo = repo;
		_personalRecords = personalRecords;
		_ranking = ranking;
		_social = social;
		_auth = auth;
	}

	public event EventHandler<WorkoutFinalizeUpdate>? Updated;

	public bool IsBusy
	{
		get
		{
			lock (_queueLock)
				return _queued.Count > 0;
		}
	}

	public void Enqueue(PendingWorkoutFinalize job)
	{
		Save(job);
		Queue(job.WorkoutId, announce: true);
	}

	public void ResumePending()
	{
		foreach (var id in ListIds())
			Queue(id, announce: true);
	}

	public void Retry(Guid workoutId)
	{
		if (!File.Exists(GetPath(workoutId)))
			return;
		Queue(workoutId, announce: true);
	}

	private void Queue(Guid workoutId, bool announce)
	{
		lock (_queueLock)
		{
			if (!_queued.Add(workoutId))
				return;
		}

		if (announce)
		{
			Publish(new WorkoutFinalizeUpdate
			{
				Phase = WorkoutFinalizePhase.Saving,
				Message = "Enregistrement de la séance…",
				WorkoutId = workoutId
			});
		}

		EnsureWorker();
		_channel.Writer.TryWrite(workoutId);
	}

	private void EnsureWorker()
	{
		if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) != 0)
			return;

		_ = Task.Run(WorkerLoop);
	}

	private async Task WorkerLoop()
	{
		await foreach (var id in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
		{
			var job = TryLoad(id);
			if (job is null)
			{
				Forget(id);
				continue;
			}

			Publish(new WorkoutFinalizeUpdate
			{
				Phase = WorkoutFinalizePhase.Saving,
				Message = "Enregistrement de la séance…",
				WorkoutId = id
			});

			var saved = false;
			Exception? last = null;
			var needsReauth = false;
			for (var attempt = 1; attempt <= 3 && !saved; attempt++)
			{
				try
				{
					var update = await ProcessAsync(job).ConfigureAwait(false);
					Delete(id);
					Forget(id);
					Publish(update);
					saved = true;
				}
				catch (Exception ex) when (SupabaseAuthService.RequiresReauthentication(ex))
				{
					last = ex;
					needsReauth = true;
					break;
				}
				catch (Exception ex)
				{
					last = ex;
					if (attempt >= 3 || !IsTransient(ex))
						break;

					await Task.Delay(TimeSpan.FromSeconds(2 * attempt)).ConfigureAwait(false);
					job = TryLoad(id) ?? job;
				}
			}

			if (saved)
				continue;

			if (last is not null)
				CrashLogWriter.TryAppend(nameof(WorkoutFinalizeService), last);

			Forget(id);
			Publish(new WorkoutFinalizeUpdate
			{
				Phase = WorkoutFinalizePhase.Failed,
				WorkoutId = id,
				NeedsReauth = needsReauth,
				Message = needsReauth
					? "Session expirée. Touche pour te reconnecter : la séance est conservée."
					: "L'enregistrement n'a pas abouti. Touche pour réessayer."
			});
		}
	}

	private async Task<WorkoutFinalizeUpdate> ProcessAsync(PendingWorkoutFinalize job)
	{
		await _auth.EnsureSessionFreshAsync().ConfigureAwait(false);
		if (!_auth.IsSignedIn)
			throw new InvalidOperationException("Session expirée — reconnecte-toi.");

		foreach (var stat in job.Stats)
		{
			await _repo.UpsertDailyMaxKgAsync(
				job.ProfileId,
				stat.ExerciseId,
				job.PerformanceLocalDate,
				stat.MaxKg).ConfigureAwait(false);

			await _repo.UpsertLastWeightAsync(new ExerciseLastWeightRow
			{
				ProfileId = job.ProfileId,
				ExerciseId = stat.ExerciseId,
				WeightKg = stat.LastKg,
				UpdatedUtc = DateTime.UtcNow
			}).ConfigureAwait(false);
		}

		var savedWorkoutId = job.WorkoutId;
		if (!job.WorkoutInserted)
		{
			CompletedWorkoutRow? existing = null;
			try
			{
				existing = await _repo.GetCompletedWorkoutAsync(job.WorkoutId, job.ProfileId).ConfigureAwait(false);
			}
			catch (Exception ex) when (!SupabaseAuthService.RequiresReauthentication(ex))
			{
				if (IsTransient(ex))
					throw;
				existing = null;
			}

			if (existing is null)
			{
				var remote = new WorkoutCompletedRequest(
					job.CompletedUtc,
					job.Calories,
					job.Minutes,
					null,
					job.Exercises);
				try
				{
					savedWorkoutId = await _repo.AddCompletedWorkoutAsync(new CompletedWorkoutRow
					{
						Id = job.WorkoutId,
						ProfileId = job.ProfileId,
						CompletedUtc = job.CompletedUtc,
						CaloriesRounded = job.Calories,
						SessionTitle = job.SessionTitle,
						SourceSessionTemplateId = job.IsAdHoc ? null : job.SessionId,
						PayloadJson = CompletedWorkoutSnapshot.SerializeRequest(remote)
					}).ConfigureAwait(false);
				}
				catch (Exception ex) when (!SupabaseAuthService.RequiresReauthentication(ex) && IsDuplicate(ex))
				{
					savedWorkoutId = job.WorkoutId;
				}
			}

			job.WorkoutInserted = true;
			Save(job);
		}

		if (!job.PersonalRecordsDone)
		{
			try
			{
				await _personalRecords.ProcessCompletedWorkoutAsync(
					_repo,
					job.ProfileId,
					savedWorkoutId,
					job.CompletedUtc,
					job.Exercises,
					job.TotalFilledSets).ConfigureAwait(false);
				job.PersonalRecordsDone = true;
				Save(job);
			}
			catch (Exception prEx) when (prEx is not OperationCanceledException
			                             && !SupabaseAuthService.RequiresReauthentication(prEx))
			{
				CrashLogWriter.TryAppend(nameof(WorkoutFinalizeService) + ".Pr", prEx);
				if (IsTransient(prEx))
					throw;

				job.PersonalRecordsDone = true;
				Save(job);
			}
		}

		if (!job.IsAdHoc && job.SessionId != Guid.Empty)
		{
			var json = JsonSerializer.Serialize(new LastRunEnvelope(job.Exercises), SnapshotJson);
			await _repo.UpsertSessionSnapshotAsync(new SessionLastSnapshotRow
			{
				SessionId = job.SessionId,
				Json = json,
				SavedUtc = job.CompletedUtc
			}).ConfigureAwait(false);
		}

		_social.InvalidateCache();

		IReadOnlyList<QuestUnlockItem> unlocks = [];
		try
		{
			var rankingResult = await _ranking.RefreshAsync(job.ProfileId).ConfigureAwait(false);
			unlocks = await BuildUnlockItemsAsync(job.ProfileId, rankingResult.Unlocked).ConfigureAwait(false);
		}
		catch (Exception rankEx) when (rankEx is not OperationCanceledException
		                                && !SupabaseAuthService.RequiresReauthentication(rankEx))
		{
			CrashLogWriter.TryAppend(nameof(WorkoutFinalizeService) + ".Rank", rankEx);
			if (IsTransient(rankEx))
				throw;
		}

		var kcal = Math.Round(job.Calories);
		var message = unlocks.Count switch
		{
			0 => $"Séance enregistrée · {kcal} kcal (indicatif).",
			1 => $"Séance enregistrée · {kcal} kcal. 1 quête débloquée — toucher pour voir.",
			_ => $"Séance enregistrée · {kcal} kcal. {unlocks.Count} quêtes débloquées — toucher pour voir."
		};

		return new WorkoutFinalizeUpdate
		{
			Phase = WorkoutFinalizePhase.Saved,
			Message = message,
			WorkoutId = savedWorkoutId,
			Unlocks = unlocks
		};
	}

	private async Task<IReadOnlyList<QuestUnlockItem>> BuildUnlockItemsAsync(
		Guid profileId,
		IReadOnlyList<NewlyUnlockedQuest> unlocked)
	{
		if (unlocked.Count == 0)
			return [];

		var exercises = (await _repo.ListExercisesAsync().ConfigureAwait(false)).ToDictionary(e => e.Id);
		var defs = RankingExerciseIndex.Map(exercises.Values);
		var ranks = (await _repo.ListProfileExerciseRanksAsync(profileId).ConfigureAwait(false))
			.ToDictionary(r => r.ExerciseId);

		var items = new List<QuestUnlockItem>(unlocked.Count);
		foreach (var u in unlocked)
		{
			exercises.TryGetValue(u.ExerciseId, out var ex);
			defs.TryGetValue(u.ExerciseId, out var def);
			ranks.TryGetValue(u.ExerciseId, out var rank);
			var objective = "—";
			if (rank?.R10Used is > 0 && def is not null)
			{
				var option = QuestObjectivePlanner.ForRank(
					rank.R10Used.Value,
					u.TargetRank,
					def.MeasurementType ?? MeasurementType.FiveRm,
					def.LoadMode);
				objective = QuestObjectiveUi.FormatSet(option.Primary, def.LoadMode);
			}

			items.Add(new QuestUnlockItem(
				u.ExerciseId,
				ex?.NameFr ?? "Exercice",
				u.TargetRank,
				u.ValidatedRank,
				objective));
		}

		return items;
	}

	private static bool IsTransient(Exception ex) => NetworkFault.IsTransient(ex);

	private static bool IsDuplicate(Exception ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			var msg = current.Message;
			if (msg.Contains("23505", StringComparison.Ordinal)
			    || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private void Publish(WorkoutFinalizeUpdate update) =>
		Updated?.Invoke(this, update);

	private void Forget(Guid workoutId)
	{
		lock (_queueLock)
			_queued.Remove(workoutId);
	}

	private void Save(PendingWorkoutFinalize job)
	{
		var path = GetPath(job.WorkoutId);
		var dir = Path.GetDirectoryName(path)!;
		Directory.CreateDirectory(dir);
		var json = JsonSerializer.Serialize(job, JsonOptions);
		var tempPath = path + ".tmp";
		lock (_fileLock)
		{
			File.WriteAllText(tempPath, json);
			if (File.Exists(path))
				File.Delete(path);
			File.Move(tempPath, path);
		}
	}

	private PendingWorkoutFinalize? TryLoad(Guid workoutId)
	{
		var path = GetPath(workoutId);
		if (!File.Exists(path))
			return null;

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<PendingWorkoutFinalize>(json, JsonOptions);
		}
		catch (Exception ex)
		{
			CrashLogWriter.TryAppend(nameof(WorkoutFinalizeService) + ".Load", ex);
			return null;
		}
	}

	private void Delete(Guid workoutId)
	{
		var path = GetPath(workoutId);
		lock (_fileLock)
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}

	private List<Guid> ListIds()
	{
		var dir = GetDirectory();
		if (!Directory.Exists(dir))
			return [];

		var ids = new List<Guid>();
		foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
		{
			var name = Path.GetFileNameWithoutExtension(path);
			if (Guid.TryParseExact(name, "N", out var id))
				ids.Add(id);
		}

		return ids;
	}

	private static string GetPath(Guid workoutId) =>
		Path.Combine(GetDirectory(), $"{workoutId:N}.json");

	private static string GetDirectory()
	{
		try
		{
			return Path.Combine(FileSystem.AppDataDirectory, "pending-finalize");
		}
		catch
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"com.rhythmo.mobile",
				"pending-finalize");
		}
	}
}
