using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;

public sealed record LookupCheckoutPassageQuery(string Reference)
    : IRequest<ErrorOr<CheckoutPassageLookupResult>>;

public sealed record CheckoutPassageLookupResult(
    Guid Id,
    DateTime OccurredAt,
    int LineCount,
    string? DisplayLabel);
