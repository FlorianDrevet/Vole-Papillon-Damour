using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookCoverStorage
{
    Task<Uri?> TryStoreAsync(
        Isbn13 isbn13,
        Uri sourceUri,
        CancellationToken cancellationToken);
}
