using MediatR;
using Vole_Papillon_Damour.Application.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;

public class GetTodayEventQueryHandler(IEventRepository eventRepository, IDateTimeProvider clock)
    : IRequestHandler<GetTodayEventQuery, TodayEventResult?>
{
    public async Task<TodayEventResult?> Handle(GetTodayEventQuery query, CancellationToken cancellationToken)
    {
        var today = ParisCalendar.Today(clock.UtcNow);
        var lastStartDay = today.AddDays(7);
        // One day of margin: an event stored at Paris midnight starts the previous UTC day.
        var candidates = await eventRepository.GetNotCancelledFromAsync(
            ParisCalendar.StartOfDayUtc(today).AddDays(-1),
            cancellationToken);

        var todayEvent = candidates
            .Where(assoEvent =>
            {
                var startDay = ParisDay(assoEvent.DateStart);
                var endDay = ParisDay(assoEvent.DateEnd ?? assoEvent.DateStart);

                return !assoEvent.IsCancelled && startDay <= lastStartDay && endDay >= today;
            })
            .OrderByDescending(assoEvent => assoEvent.IsHappeningOn(today, ParisCalendar.Zone))
            .ThenBy(assoEvent => Math.Abs(ParisDay(assoEvent.DateStart).DayNumber - today.DayNumber))
            .ThenBy(assoEvent => IsLoto(assoEvent.EventsType) ? 0 : 1)
            .ThenBy(assoEvent => assoEvent.DateStart)
            .FirstOrDefault();

        if (todayEvent is null)
        {
            return null;
        }

        var isLoto = IsLoto(todayEvent.EventsType);
        var isLotoToday = isLoto && todayEvent.IsHappeningOn(today, ParisCalendar.Zone);
        return new TodayEventResult(
            todayEvent.Id,
            todayEvent.Name,
            todayEvent.EventsType,
            todayEvent.DateStart,
            todayEvent.DateEnd,
            todayEvent.HourOpenDoors,
            todayEvent.HourCloseDoors,
            todayEvent.Adresse.City,
            isLotoToday ? todayEvent.GetLiveStatus() : null,
            isLotoToday ? todayEvent.GetLastDrawnNumero() : null,
            isLoto ? todayEvent.LiveTheme : null);
    }

    private static DateOnly ParisDay(DateTimeOffset date) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(date, ParisCalendar.Zone).DateTime);

    private static bool IsLoto(EventsType eventsType) =>
        eventsType.Value == EventsType.EventsTypeEnum.Bingo;
}
