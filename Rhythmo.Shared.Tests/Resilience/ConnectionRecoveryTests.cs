using System.Net.Http;
using Rhythmo.Shared.Resilience;
using Xunit;

namespace Rhythmo.Shared.Tests.Resilience;

public class FaultClassifierTests
{
	[Fact]
	public void Transport_failures_are_transient()
	{
		Assert.Equal(FaultKind.Transient, FaultClassifier.Classify(new HttpRequestException("network down")));
		Assert.Equal(FaultKind.Transient, FaultClassifier.Classify(new TimeoutException("timed out")));
		Assert.Equal(FaultKind.Transient, FaultClassifier.Classify(new InvalidOperationException("502 Bad Gateway")));
		Assert.Equal(
			FaultKind.Transient,
			FaultClassifier.Classify(new Exception("wrap", new HttpRequestException("socket"))));
	}

	[Fact]
	public void System_cancel_is_transient_until_the_caller_cancels()
	{
		Assert.Equal(FaultKind.Transient, FaultClassifier.Classify(new TaskCanceledException()));

		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Assert.Equal(
			FaultKind.Canceled,
			FaultClassifier.Classify(new OperationCanceledException(cts.Token), callerCanceled: true));
	}

	[Fact]
	public void Dead_session_is_reauth_and_not_a_blip()
	{
		Assert.Equal(FaultKind.Reauth, FaultClassifier.Classify(new InvalidOperationException("jwt expired")));
		Assert.Equal(FaultKind.Reauth, FaultClassifier.Classify(new InvalidOperationException("Invalid Refresh Token")));
		Assert.Equal(FaultKind.Reauth, FaultClassifier.Classify(new InvalidOperationException("Session expirée — reconnecte-toi.")));
		Assert.Equal(FaultKind.Unexpected, FaultClassifier.Classify(new InvalidOperationException("Profil introuvable.")));
	}
}

public class ReconnectPolicyTests
{
	[Fact]
	public void Offline_does_not_schedule_a_poll()
	{
		var now = DateTimeOffset.UnixEpoch;
		Assert.Null(ReconnectPolicy.NextDelay(0, hasInternet: false, wake: false));
		Assert.Null(ReconnectPolicy.NextAttemptAt(now, 3, hasInternet: false, wake: true));
	}

	[Fact]
	public void Online_backoff_is_immediate_then_capped_at_one_second()
	{
		var now = DateTimeOffset.UnixEpoch;
		Assert.Equal(TimeSpan.Zero, ReconnectPolicy.NextDelay(0, hasInternet: true, wake: false));
		Assert.Equal(TimeSpan.FromMilliseconds(250), ReconnectPolicy.NextDelay(1, true, false));
		Assert.Equal(TimeSpan.FromMilliseconds(500), ReconnectPolicy.NextDelay(2, true, false));
		Assert.Equal(TimeSpan.FromSeconds(1), ReconnectPolicy.NextDelay(9, true, false));
		Assert.Equal(now, ReconnectPolicy.NextAttemptAt(now, 4, hasInternet: true, wake: true));
	}
}

public class ConnectionRecoveryTests
{
	[Fact]
	public async Task Load_failure_retries_once_after_a_single_refresh_without_a_visible_signal()
	{
		var calls = 0;
		var refreshes = 0;
		var recovery = Recovery(onRefresh: () => refreshes++);
		var page = new object();

		Task Load(CancellationToken _)
		{
			calls++;
			if (calls == 1)
				throw new HttpRequestException("socket");
			return Task.CompletedTask;
		}

		recovery.Bind(page, Load);
		var kind = await recovery.GuardAsync(Load);

		Assert.Null(kind);
		Assert.Equal(2, calls);
		Assert.Equal(1, refreshes);
		Assert.Equal(0, recovery.UserVisibleSignals);
		Assert.Empty(recovery.SurfacedFaults);
	}

	[Fact]
	public async Task Stays_idle_while_offline_then_reloads_the_current_page_when_radio_returns()
	{
		var refreshes = 0;
		var reloads = 0;
		var recovery = Recovery(onRefresh: () => refreshes++);
		var page = new object();
		recovery.Bind(page, _ =>
		{
			reloads++;
			return Task.CompletedTask;
		});
		recovery.SetRadio(false);

		var parked = recovery.WhenParkedAsync();
		var pending = recovery.GuardAsync(_ => throw new HttpRequestException("network"));
		await parked.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.Equal(0, refreshes);
		Assert.Equal(0, reloads);

		recovery.SetRadio(true);
		Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(1, refreshes);
		Assert.Equal(1, reloads);
		Assert.Equal(0, recovery.UserVisibleSignals);
	}

	[Fact]
	public async Task Phone_lock_waits_for_radio_after_resume_then_reconnects_once()
	{
		var refreshes = 0;
		var reloads = 0;
		var recovery = Recovery(onRefresh: () => refreshes++);
		var page = new object();
		recovery.Bind(page, _ =>
		{
			reloads++;
			return Task.CompletedTask;
		});
		recovery.SetRadio(false);

		var parked = recovery.WhenParkedAsync();
		var pending = recovery.GuardAsync(_ => throw new TaskCanceledException());
		await parked.WaitAsync(TimeSpan.FromSeconds(2));
		Assert.Equal(0, refreshes);

		var parkedAgain = recovery.WhenParkedAsync();
		recovery.NotifyResumed();
		await parkedAgain.WaitAsync(TimeSpan.FromSeconds(2));
		Assert.Equal(0, refreshes);
		Assert.Equal(0, reloads);

		recovery.SetRadio(true);
		Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(1, refreshes);
		Assert.Equal(1, reloads);
		Assert.Equal(0, recovery.UserVisibleSignals);
	}

	[Fact]
	public async Task Leaving_a_page_does_not_reload_it_after_the_network_returns()
	{
		var refreshes = 0;
		var reloads = 0;
		var recovery = Recovery(onRefresh: () => refreshes++);
		var page = new object();
		recovery.Bind(page, _ =>
		{
			reloads++;
			return Task.CompletedTask;
		});
		recovery.SetRadio(false);

		var parked = recovery.WhenParkedAsync();
		var pending = recovery.GuardAsync(_ => throw new HttpRequestException("unreachable"));
		await parked.WaitAsync(TimeSpan.FromSeconds(2));
		recovery.Unbind(page);
		recovery.SetRadio(true);

		Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(1, refreshes);
		Assert.Equal(0, reloads);
	}

	[Fact]
	public async Task Parallel_loads_share_a_single_refresh()
	{
		var refreshes = 0;
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var recovery = new ConnectionRecovery(
			async _ =>
			{
				Interlocked.Increment(ref refreshes);
				await release.Task;
			},
			_ => Task.CompletedTask);
		recovery.Bind(new object(), _ => Task.CompletedTask);

		var first = recovery.GuardAsync(_ => throw new HttpRequestException("network"));
		var second = recovery.GuardAsync(_ => throw new HttpRequestException("network"));
		release.TrySetResult();

		Assert.Null(await first.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Null(await second.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(1, refreshes);
	}

	[Fact]
	public async Task Invalid_refresh_token_stops_the_loop()
	{
		var refreshes = 0;
		var recovery = Recovery(onRefresh: () =>
		{
			refreshes++;
			throw new InvalidOperationException("Invalid Refresh Token");
		});
		recovery.Bind(new object(), _ => Task.CompletedTask);

		var kind = await recovery.GuardAsync(_ => throw new HttpRequestException("network"));

		Assert.Equal(FaultKind.Reauth, kind);
		Assert.Equal(1, refreshes);
		Assert.Empty(recovery.SurfacedFaults);
	}

	[Fact]
	public async Task Caller_cancellation_does_not_refresh()
	{
		var refreshes = 0;
		var recovery = Recovery(onRefresh: () => refreshes++);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var kind = await recovery.GuardAsync(
			_ => throw new OperationCanceledException(cts.Token),
			cts.Token);

		Assert.Equal(FaultKind.Canceled, kind);
		Assert.Equal(0, refreshes);
	}

	[Fact]
	public async Task Pending_finalize_is_resumed_once_when_radio_returns_and_never_marked_failed()
	{
		var resumes = 0;
		var phases = new List<string>();
		var recovery = new ConnectionRecovery(
			_ => Task.CompletedTask,
			_ =>
			{
				resumes++;
				phases.Add("resume");
				return Task.CompletedTask;
			});
		recovery.SetRadio(false);

		var parked = recovery.WhenParkedAsync();
		var pending = recovery.GuardAsync(_ => throw new HttpRequestException("connection"));
		await parked.WaitAsync(TimeSpan.FromSeconds(2));
		recovery.Bind(new object(), _ => Task.CompletedTask);
		recovery.SetRadio(true);

		Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(1, resumes);
		Assert.Equal(["resume"], phases);
		Assert.Equal(0, recovery.UserVisibleSignals);
	}

	[Fact]
	public async Task Online_blips_back_off_before_the_next_refresh()
	{
		var now = DateTimeOffset.UnixEpoch;
		var delays = new List<TimeSpan>();
		var refreshes = 0;
		var recovery = new ConnectionRecovery(
			_ =>
			{
				refreshes++;
				if (refreshes < 3)
					throw new TimeoutException("timeout");
				return Task.CompletedTask;
			},
			_ => Task.CompletedTask,
			clock: () => now,
			delay: (span, _) =>
			{
				delays.Add(span);
				now = now.Add(span);
				return Task.CompletedTask;
			});
		recovery.Bind(new object(), _ => Task.CompletedTask);

		Assert.Null(await recovery.GuardAsync(_ => throw new HttpRequestException("network")));
		Assert.Equal(3, refreshes);
		Assert.Equal(
			[TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)],
			delays);
	}

	private static ConnectionRecovery Recovery(Action onRefresh) =>
		new(
			_ =>
			{
				onRefresh();
				return Task.CompletedTask;
			},
			_ => Task.CompletedTask);
}
