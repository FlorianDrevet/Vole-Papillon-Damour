using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.BookAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class InternalBookCodeSequenceConfiguration : IEntityTypeConfiguration<InternalBookCodeSequence>
{
    public void Configure(EntityTypeBuilder<InternalBookCodeSequence> builder)
    {
        builder.ToTable("InternalBookCodeSequences");
        builder.HasKey(sequence => sequence.Id);
        builder.Property(sequence => sequence.Id).ValueGeneratedNever();
        // Le jeton de concurrence empêche deux créations de recevoir le même numéro.
        builder.Property(sequence => sequence.LastIssuedNumber)
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasData(InternalBookCodeSequence.Create());
    }
}