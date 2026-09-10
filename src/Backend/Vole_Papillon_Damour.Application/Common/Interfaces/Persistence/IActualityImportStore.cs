using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;
using SocialPostImportAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.SocialPostImport;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IActualityImportStore
{
    Task<IReadOnlySet<string>> GetImportedExternalIdsAsync(
        SocialPostSource source,
        CancellationToken cancellationToken);

    Task PersistAsync(
        ActualityAggregate actuality,
        SocialPostImportAggregate import,
        CancellationToken cancellationToken);
}
