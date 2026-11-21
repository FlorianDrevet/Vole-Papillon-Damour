using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

// Attribue le prochain code interne d'un livre sans ISBN (RG-170, RG-171). L'implémentation lit la séquence
// SQL de la plateforme : deux appels simultanés reçoivent deux numéros différents, et un numéro n'est jamais
// réémis, même si la transaction appelante est annulée (le numéro est alors sauté).
public interface IInternalBookCodeIssuer
{
    Task<BookCode> IssueNextAsync(CancellationToken cancellationToken);
}
