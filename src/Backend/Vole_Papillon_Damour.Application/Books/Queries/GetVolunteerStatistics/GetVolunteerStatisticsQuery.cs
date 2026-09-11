using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetVolunteerStatistics;

public sealed record GetVolunteerStatisticsQuery(
    UserId VolunteerId) : IRequest<ErrorOr<VolunteerStatisticsResult>>;
