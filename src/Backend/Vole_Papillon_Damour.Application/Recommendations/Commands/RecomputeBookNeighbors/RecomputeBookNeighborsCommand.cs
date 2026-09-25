using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;

public sealed record RecomputeBookNeighborsCommand : IRequest<RecomputeBookNeighborsResult>;

public sealed record RecomputeBookNeighborsResult(int Books, int Embedded, int Neighbors, Guid? GenerationId);
