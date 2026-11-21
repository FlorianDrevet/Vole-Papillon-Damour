using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Common;

// Émetteur de test : la séquence SQL n'existe pas sous SQLite. Chaque appel renvoie le numéro suivant,
// sans jamais le réémettre (RG-171). LastIssuedNumber permet de simuler un compteur en retard.
internal sealed class FakeInternalBookCodeIssuer(long lastIssuedNumber = 0) : IInternalBookCodeIssuer
{
    public long LastIssuedNumber { get; private set; } = lastIssuedNumber;

    public int IssueCount { get; private set; }

    public Task<BookCode> IssueNextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastIssuedNumber++;
        IssueCount++;
        return Task.FromResult(BookCode.CreateInternalFromSequence(LastIssuedNumber));
    }
}
