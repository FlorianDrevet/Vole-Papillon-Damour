namespace Vole_Papillon_Damour.Application.WatchlistFeature.Common;

public enum RecordEmailBounceForRecipientOutcome : byte
{
    Recorded = 0,
    AlreadyRecorded = 1,
    IgnoredUnknownRecipient = 2,
    IgnoredWithoutWatchlist = 3
}

public sealed record RecordEmailBounceForRecipientResult(
    RecordEmailBounceForRecipientOutcome Outcome);
