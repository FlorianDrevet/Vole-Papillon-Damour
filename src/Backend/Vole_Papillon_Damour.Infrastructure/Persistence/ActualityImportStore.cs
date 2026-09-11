using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence;

public sealed class ActualityImportStore(ProjectDbContext context) : IActualityImportStore
{
    public async Task<IReadOnlySet<string>> GetImportedExternalIdsAsync(
        SocialPostSource source,
        CancellationToken cancellationToken)
    {
        var externalIds = await context.SocialPostImports
            .AsNoTracking()
            .Where(import => import.Source == source)
            .Select(import => import.ExternalId)
            .ToListAsync(cancellationToken);

        return externalIds.ToHashSet(StringComparer.Ordinal);
    }

    public async Task PersistAsync(
        Actuality actuality,
        SocialPostImport import,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.Actualities.Add(actuality);
        context.SocialPostImports.Add(import);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
