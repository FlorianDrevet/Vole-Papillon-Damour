using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate;

// Compteur des codes internes (RG-170, RG-171). Une seule ligne existe : chaque code issu du compteur
// est attribué une fois pour toutes, même si la fiche qui le portait disparaît.
public sealed class InternalBookCodeSequence
{
    public const int SingletonId = 1;

    private InternalBookCodeSequence()
    {
    }

    public int Id { get; private set; }

    public long LastIssuedNumber { get; private set; }

    public static InternalBookCodeSequence Create() => new()
    {
        Id = SingletonId,
        LastIssuedNumber = 0,
    };

    // Attribue le code suivant. Lève une exception si le compteur est épuisé (10^11 codes).
    public BookCode IssueNext()
    {
        if (LastIssuedNumber >= BookCode.MaximumInternalSequenceNumber)
        {
            throw new InvalidOperationException("The internal book code sequence is exhausted.");
        }

        LastIssuedNumber++;
        return BookCode.CreateInternalFromSequence(LastIssuedNumber);
    }
}