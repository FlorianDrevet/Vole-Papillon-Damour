namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

public sealed record ManualRequesterDeletionResult(int CancelledRequestCount, int NotifiedMemberCount);

public sealed record ManualRequestReleaseResult(int NotifiedMemberCount);
