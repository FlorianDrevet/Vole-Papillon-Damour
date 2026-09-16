using Vole_Papillon_Damour.Domain.RareBookAggregate;

namespace Vole_Papillon_Damour.Application.RareBooks.Common;

public static class RareBookProjector
{
    public static RareBookResult ToResult(RareBook book) =>
        RareBookResultProjector.ToResult(book);

    public static PublicRareBookResult ToPublicResult(RareBook book) =>
        RareBookResultProjector.ToPublicResult(book);

    public static CashRareBookResult ToCashResult(RareBook book) =>
        RareBookResultProjector.ToCashResult(book);
}
