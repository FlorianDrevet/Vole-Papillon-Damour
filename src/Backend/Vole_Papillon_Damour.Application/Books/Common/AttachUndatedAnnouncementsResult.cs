using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AttachUndatedAnnouncementsResult(
    AssoEventsId TargetAssoEventsId,
    int AttachedCount);
