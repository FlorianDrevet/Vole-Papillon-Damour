using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

// Séquence SQL qui numérote les codes internes des livres sans ISBN (RG-170, RG-171).
// Le modèle ne fixe pas StartsAt : la migration qui crée la séquence la démarre après le dernier numéro
// déjà émis (données existantes). Un numéro n'est jamais réémis ; MAXVALUE borne la séquence (11 chiffres).
public static class InternalBookCodeNumberingConfiguration
{
    public const string SequenceName = "InternalBookCodeNumbers";

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(SequenceName)
            .HasMin(1)
            .HasMax(BookCode.MaximumInternalSequenceNumber)
            .IsCyclic(false);
    }
}
