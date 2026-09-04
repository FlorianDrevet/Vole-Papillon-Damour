using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicNextBookFair;

public sealed class GetPublicNextBookFairQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetPublicNextBookFairQuery, ErrorOr<PublicBookFairResult>>
{
    public async Task<ErrorOr<PublicBookFairResult>> Handle(
        GetPublicNextBookFairQuery query,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var now = new DateTimeOffset(nowUtc, TimeSpan.Zero);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .Where(assoEvent => !assoEvent.IsCancelled)
            .ToListAsync(cancellationToken);

        var fair = fairs
            .Where(assoEvent =>
                assoEvent.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                (assoEvent.DateEnd ?? assoEvent.DateStart) > now)
            .OrderBy(assoEvent => assoEvent.HourOpenDoors ?? assoEvent.DateStart)
            .ThenBy(assoEvent => assoEvent.Id.Value)
            .FirstOrDefault();

        if (fair is null)
        {
            return Errors.AssoEvent.AssoEventNextBooksNotFound();
        }

        return new PublicBookFairResult(
            fair.Id.Value,
            fair.Name,
            fair.DateStart,
            fair.DateEnd,
            fair.HourOpenDoors ?? fair.DateStart,
            fair.HourCloseDoors ?? fair.DateEnd,
            fair.Adresse?.RoadNumber,
            fair.Adresse?.City ?? string.Empty,
            fair.Adresse?.CityCode ?? 0,
            fair.Adresse?.Road ?? string.Empty);
    }
}
