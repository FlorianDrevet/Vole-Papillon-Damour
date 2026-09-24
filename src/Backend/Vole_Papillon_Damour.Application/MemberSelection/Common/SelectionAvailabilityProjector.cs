using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberSelection.Common;

public static class SelectionAvailabilityProjector
{
    public static SelectionAvailability ForEdition(Book? book, bool announced)
    {
        if (book is null)
        {
            return announced ? SelectionAvailability.Announced : SelectionAvailability.Unavailable;
        }

        if (book.IsHiddenFromCatalog || book.RedirectedToIsbn13 is not null)
        {
            return SelectionAvailability.Unavailable;
        }

        if (book.QuantityAvailable > 0)
        {
            return SelectionAvailability.Available;
        }

        return announced ? SelectionAvailability.Announced : SelectionAvailability.OutOfStock;
    }

    public static SelectionAvailability ForRareBook(RareBook? rareBook)
    {
        if (rareBook is null || rareBook.Status != RareBookStatus.Published)
        {
            return SelectionAvailability.Unavailable;
        }

        return rareBook.IsSold ? SelectionAvailability.RareSold : SelectionAvailability.Available;
    }
}

