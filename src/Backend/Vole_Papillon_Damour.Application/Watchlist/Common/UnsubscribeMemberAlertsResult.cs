namespace Vole_Papillon_Damour.Application.WatchlistFeature.Common;

public enum UnsubscribeMemberAlertsOutcome
{
    Unsubscribed,
    AlreadyUnsubscribed,
    IgnoredUnknownMember
}

public sealed record UnsubscribeMemberAlertsResult(UnsubscribeMemberAlertsOutcome Outcome);
