using Rhythmo.Shared.Resilience;

namespace Rhythmo.Mobile.Infrastructure;

/// <summary>Classifie les pannes réseau / timeout pour retry silencieux (pas d'UI).</summary>
public static class NetworkFault
{
	public static bool IsTransient(Exception ex, CancellationToken cancellationToken = default) =>
		FaultClassifier.Classify(ex, cancellationToken.IsCancellationRequested) == FaultKind.Transient;
}
