namespace Rhythmo.Shared.Resilience;

/// <summary>
/// Un seul vol de reconnexion. Au succès : refresh, reprise des envois en attente, rejeu de la page encore visible.
/// Aucune coupure n'est signalée à l'utilisateur.
/// </summary>
public sealed class ConnectionRecovery
{
	private readonly object _sync = new();
	private readonly Func<CancellationToken, Task> _refreshSession;
	private readonly Func<CancellationToken, Task> _resumePending;
	private readonly Func<DateTimeOffset> _clock;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;
	private readonly Action<Exception, FaultKind>? _onFault;
	private readonly Func<bool>? _shouldRestore;

	private bool _radio = true;
	private bool _wake;
	private int _failures;
	private DateTimeOffset? _notBefore;
	private object? _page;
	private Func<CancellationToken, Task>? _reload;
	private Task? _recovery;
	private bool _again;
	private FaultKind? _passResult;
	private TaskCompletionSource<bool> _pulse = NewSignal();
	private TaskCompletionSource<bool> _parked = NewSignal();
	private readonly List<FaultKind> _surfaced = [];

	public ConnectionRecovery(
		Func<CancellationToken, Task> refreshSession,
		Func<CancellationToken, Task> resumePending,
		Func<DateTimeOffset>? clock = null,
		Func<TimeSpan, CancellationToken, Task>? delay = null,
		Action<Exception, FaultKind>? onFault = null,
		Func<bool>? shouldRestore = null)
	{
		_refreshSession = refreshSession;
		_resumePending = resumePending;
		_clock = clock ?? (() => DateTimeOffset.UtcNow);
		_delay = delay ?? ((span, token) => Task.Delay(span, token));
		_onFault = onFault;
		_shouldRestore = shouldRestore;
	}

	/// <summary>Les coupures ne remontent jamais ici. Reste à 0.</summary>
	public int UserVisibleSignals => _surfaced.Count;

	public IReadOnlyList<FaultKind> SurfacedFaults => _surfaced;

	public void SetRadio(bool online)
	{
		TaskCompletionSource<bool>? pulse = null;
		lock (_sync)
		{
			var becameOnline = online && !_radio;
			_radio = online;
			if (becameOnline)
				_wake = true;

			var running = _recovery is { IsCompleted: false };
			if (running)
				pulse = ExchangePulse();
			else if (becameOnline)
				_recovery = RunPassAsync();
		}

		pulse?.TrySetResult(true);
	}

	public void NotifyResumed()
	{
		TaskCompletionSource<bool>? pulse;
		lock (_sync)
		{
			_wake = true;
			pulse = ExchangePulse();
			if (_recovery is null || _recovery.IsCompleted)
				_recovery = RunPassAsync();
		}

		pulse.TrySetResult(true);
	}

	/// <summary>
	/// Reprend les envois en attente. Si un vol est déjà en cours, il refait un tour à la fin.
	/// </summary>
	public void RequestFollowUp()
	{
		TaskCompletionSource<bool>? pulse = null;
		lock (_sync)
		{
			if (_recovery is { IsCompleted: false })
			{
				_again = true;
				_wake = true;
				pulse = ExchangePulse();
			}
			else
			{
				_wake = true;
				_recovery = RunPassAsync();
			}
		}

		pulse?.TrySetResult(true);
	}

	public void Bind(object page, Func<CancellationToken, Task> reload)
	{
		lock (_sync)
		{
			_page = page;
			_reload = reload;
		}
	}

	public void Unbind(object page)
	{
		lock (_sync)
		{
			if (!ReferenceEquals(_page, page))
				return;

			_page = null;
			_reload = null;
		}
	}

	public Task WhenParkedAsync()
	{
		lock (_sync)
			return _parked.Task;
	}

	public async Task<FaultKind?> GuardAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await action(cancellationToken).ConfigureAwait(false);
			return null;
		}
		catch (Exception ex)
		{
			var kind = FaultClassifier.Classify(ex, cancellationToken.IsCancellationRequested);
			_onFault?.Invoke(ex, kind);
			if (kind != FaultKind.Transient)
				return kind;

			await EnsurePassAsync().ConfigureAwait(false);
			return _passResult;
		}
	}

	private Task EnsurePassAsync()
	{
		lock (_sync)
		{
			if (_recovery is { IsCompleted: false })
				return _recovery;

			_recovery = RunPassAsync();
			return _recovery;
		}
	}

	private async Task RunPassAsync()
	{
		await Task.Yield();
		_passResult = null;

		while (true)
		{
			await WaitUntilDueAsync().ConfigureAwait(false);

			try
			{
				await _refreshSession(CancellationToken.None).ConfigureAwait(false);
				lock (_sync)
					_failures = 0;

				if (_shouldRestore is null || _shouldRestore())
					await _resumePending(CancellationToken.None).ConfigureAwait(false);

				Func<CancellationToken, Task>? reload;
				lock (_sync)
					reload = _reload;

				if (reload is not null)
					await reload(CancellationToken.None).ConfigureAwait(false);

				bool again;
				lock (_sync)
				{
					again = _again;
					_again = false;
				}

				if (again)
					continue;

				_passResult = null;
				return;
			}
			catch (Exception ex)
			{
				var kind = FaultClassifier.Classify(ex, callerCanceled: false);
				_onFault?.Invoke(ex, kind);
				if (kind != FaultKind.Transient)
				{
					_passResult = kind;
					lock (_sync)
						_again = false;
					return;
				}

				lock (_sync)
				{
					_failures++;
					var wait = ReconnectPolicy.NextDelay(_failures, _radio, wake: false);
					_notBefore = wait is null ? null : _clock() + wait.Value;
				}
			}
		}
	}

	private async Task WaitUntilDueAsync()
	{
		while (true)
		{
			Task? pulse = null;
			TimeSpan? delay = null;
			lock (_sync)
			{
				var now = _clock();
				if (!_radio)
				{
					_wake = false;
					pulse = _pulse.Task;
					MarkParkedLocked();
				}
				else if (_wake || _notBefore is null || now >= _notBefore.Value)
				{
					_wake = false;
					_notBefore = null;
					return;
				}
				else
				{
					delay = _notBefore.Value - now;
					pulse = _pulse.Task;
				}
			}

			if (delay is null)
			{
				await pulse!.ConfigureAwait(false);
				continue;
			}

			var delayTask = _delay(delay.Value, CancellationToken.None);
			var finished = await Task.WhenAny(delayTask, pulse!).ConfigureAwait(false);
			if (finished == delayTask)
			{
				await delayTask.ConfigureAwait(false);
				lock (_sync)
					_notBefore = _clock();
			}
		}
	}

	private void MarkParkedLocked()
	{
		var current = _parked;
		_parked = NewSignal();
		current.TrySetResult(true);
	}

	private TaskCompletionSource<bool> ExchangePulse()
	{
		var current = _pulse;
		_pulse = NewSignal();
		return current;
	}

	private static TaskCompletionSource<bool> NewSignal() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}
