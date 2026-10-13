using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Common;

/// <summary>
/// The event shown in the site's "today" banner. <see cref="LiveStatus"/> and
/// <see cref="LastNumero"/> are only set for a loto.
/// </summary>
public record TodayEventResult(
    AssoEventsId Id,
    string Name,
    EventsType EventsType,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset? HourOpenDoors,
    DateTimeOffset? HourCloseDoors,
    string City,
    LiveTableauStatus? LiveStatus,
    int? LastNumero);
