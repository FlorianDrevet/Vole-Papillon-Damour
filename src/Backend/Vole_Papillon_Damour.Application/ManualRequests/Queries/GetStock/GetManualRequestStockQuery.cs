using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetStock;

public sealed record GetManualRequestStockQuery(
    ManualBookRequestScope Scope,
    string? WorkId,
    string? Isbn13) : IRequest<ErrorOr<ManualRequestStockSnapshot>>;
