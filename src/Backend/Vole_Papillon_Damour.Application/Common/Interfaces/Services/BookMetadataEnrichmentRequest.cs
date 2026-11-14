using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public sealed record BookMetadataEnrichmentRequest(
    Isbn13 Isbn13,
    IsbndbUsage Usage,
    bool ForceFallback);
