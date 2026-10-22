using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;

public sealed record RecomputeBookNeighborsCommand(IReadOnlyCollection<string>? Isbn13s = null)
    : IRequest<RecomputeBookNeighborsResult>;

public sealed record RecomputeBookNeighborsResult(int Books, int Embedded, int Neighbors, Guid? GenerationId);
