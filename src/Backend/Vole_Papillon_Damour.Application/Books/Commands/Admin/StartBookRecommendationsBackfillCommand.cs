using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record StartBookRecommendationsBackfillCommand
    : IRequest<ErrorOr<Guid>>;
