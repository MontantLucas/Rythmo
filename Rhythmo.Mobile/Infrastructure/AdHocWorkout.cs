using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Rhythmo.Mobile.Services;

namespace Rhythmo.Mobile.Infrastructure;

internal static class AdHocWorkout
{
	public const string DefaultTitle = "Séance à la volée";

	public static string TitleForNow() =>
		$"{DefaultTitle} · {DateTime.Now.ToString("d", CultureInfo.CurrentCulture)}";

	public static string RunnerRoute(Guid? sessionId = null, Guid? firstExerciseId = null)
	{
		var parts = new List<string> { "AdHoc=1" };
		if (sessionId is { } sid && sid != Guid.Empty)
			parts.Add("SessionId=" + Uri.EscapeDataString(sid.ToString()));
		if (firstExerciseId is { } ex)
			parts.Add("ExerciseId=" + Uri.EscapeDataString(ex.ToString()));
		return $"{nameof(WorkoutRunnerPage)}?{string.Join("&", parts)}";
	}

	public static async Task StartOrResumeAsync(Page host, Guid? firstExerciseId = null)
	{
		var profileId = ServiceHelper.Services.GetRequiredService<ActiveProfileStore>().Get();
		var store = ServiceHelper.Services.GetRequiredService<WorkoutDraftStore>();
		var draft = store.TryLoad(profileId);

		if (draft is not null && draft.ProfileId == profileId)
		{
			var resume = await host.DisplayAlertAsync(
				"Séance en cours",
				$"Tu as déjà « {draft.SessionTitle} » non terminée.\nReprendre, ou commencer une nouvelle séance à la volée ?",
				"Reprendre",
				"Nouvelle").ConfigureAwait(true);

			if (resume)
			{
				var route = draft.IsAdHoc
					? RunnerRoute(draft.SessionId)
					: $"{nameof(WorkoutRunnerPage)}?SessionId={Uri.EscapeDataString(draft.SessionId.ToString())}";
				await UiShellNavigate.GoAsync(route).ConfigureAwait(false);
				return;
			}

			store.Clear(profileId);
		}

		await UiShellNavigate.GoAsync(RunnerRoute(firstExerciseId: firstExerciseId)).ConfigureAwait(false);
	}
}
