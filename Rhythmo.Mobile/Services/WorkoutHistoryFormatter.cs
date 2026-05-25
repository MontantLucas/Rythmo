using System.Globalization;
using System.Text;
using Rhythmo.Mobile.Data;
using Rhythmo.Shared.Contracts;

namespace Rhythmo.Mobile.Services;

public static class WorkoutHistoryFormatter
{
	public static string BuildListSubtitle(CompletedWorkoutRow row)
	{
		var snap = CompletedWorkoutSnapshot.DeserializeRequestSnapshot(row.PayloadJson);
		var local = row.CompletedUtc.ToLocalTime();
		if (snap?.Exercises is not { Count: > 0 })
			return $"{Math.Round(row.CaloriesRounded)} kcal · {local:g} · (résumé sans séries)";

		var setCount = snap.Exercises.Sum(e => e.Sets?.Count ?? 0);
		var vol = WorkoutAnalytics.ComputeVolumeKg(snap);
		var volStr = vol >= 1000 ? $"{vol / 1000d:0.#} t" : $"{vol:0} kg";
		var dur = snap.EstimatedDurationMinutes is { } m and > 0 ? $" · ~{m:0} min" : "";
		return $"{setCount} série(s) · {volStr} · {Math.Round(row.CaloriesRounded)} kcal{dur} · {local:g}";
	}

	public static string BuildDetailText(
		WorkoutCompletedRequest? snap,
		IReadOnlyDictionary<Guid, string> exerciseNames)
	{
		if (snap?.Exercises is not { Count: > 0 })
			return "(Aucun détail de séries enregistré pour cette entrée.)";

		var sb = new StringBuilder();
		foreach (var ex in snap.Exercises)
		{
			exerciseNames.TryGetValue(ex.ExerciseId, out var nm);
			var title = string.IsNullOrWhiteSpace(nm)
				? ex.ExerciseId.ToString()[..8]
				: nm;
			sb.AppendLine(title);

			if (ex.Sets is not { Count: > 0 })
			{
				sb.AppendLine("  (aucune série)");
				sb.AppendLine();
				continue;
			}

			for (var i = 0; i < ex.Sets.Count; i++)
			{
				var s = ex.Sets[i];
				var num = s.SetNumber > 0 ? s.SetNumber : i + 1;
				sb.Append("  Série ").Append(num.ToString(CultureInfo.InvariantCulture))
					.Append(" : ")
					.AppendFormat(CultureInfo.InvariantCulture, "{0} × {1:G5} kg", s.Reps, s.WeightKg)
					.AppendLine();
			}

			sb.AppendLine();
		}

		return sb.ToString().TrimEnd();
	}
}
