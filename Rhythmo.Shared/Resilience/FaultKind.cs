namespace Rhythmo.Shared.Resilience;

public enum FaultKind
{
	Transient,
	Reauth,
	Canceled,
	Unexpected
}
