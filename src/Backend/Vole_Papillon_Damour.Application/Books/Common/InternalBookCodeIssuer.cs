using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

// Attribue le prochain code interne (RG-170, RG-171). Le compteur est verrouillé par son jeton de concurrence :
// deux attributions simultanées ne peuvent pas recevoir le même numéro.
internal static class InternalBookCodeIssuer
{
    public static async Task<BookCode> IssueAsync(IProjectDbContext dbContext, CancellationToken cancellationToken)
    {
        var sequence = await dbContext.InternalBookCodeSequences
            .SingleOrDefaultAsync(cancellationToken);
        if (sequence is null)
        {
            sequence = InternalBookCodeSequence.Create();
            dbContext.InternalBookCodeSequences.Add(sequence);
        }

        return sequence.IssueNext();
    }
}