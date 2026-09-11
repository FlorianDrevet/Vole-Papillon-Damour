using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public class ActualityConfiguration : IEntityTypeConfiguration<Actuality>
{
    public void Configure(EntityTypeBuilder<Actuality> builder)
    {
        ConfigureActualityTable(builder);
    }
    private void ConfigureActualityTable(EntityTypeBuilder<Actuality> builder)
    {
        builder.ToTable("Actualities");
        builder.HasKey(actuality => actuality.Id);
        builder.Property(actuality => actuality.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => ActualityId.Create(value)
            );
                
        builder.Property(x => x.Images)
            .HasConversion(
                images => string.Join(',', images),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => new Uri(i)).ToList()
            );

        builder.Property(actuality => actuality.Status)
            .HasConversion<byte>()
            .HasDefaultValue(ActualityStatus.Published)
            .IsRequired();
        builder.Property(actuality => actuality.TitleNeedsReview)
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(actuality => actuality.ImportedAt)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(actuality => new { actuality.Status, actuality.Date });
    }
}
