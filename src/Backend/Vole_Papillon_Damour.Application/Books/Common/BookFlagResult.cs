using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record BookFlagResult(
    string Isbn13,
    bool IsRare,
    bool IsHiddenFromCatalog,
    bool Changed);
