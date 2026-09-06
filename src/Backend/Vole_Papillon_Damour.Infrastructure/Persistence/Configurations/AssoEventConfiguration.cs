using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public class AssoEventConfiguration : IEntityTypeConfiguration<AssoEvents>
{
    public void Configure(EntityTypeBuilder<AssoEvents> builder)
    {
        ConfigureEventsTable(builder);
        ConfigureParties(builder);
    }

    private void ConfigureEventsTable(EntityTypeBuilder<AssoEvents> builder)
    {
        builder.ToTable("AssoEvents");
        builder.HasKey(events => events.Id);
        builder.Property(events => events.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => AssoEventsId.Create(value)
            );
        
        builder.Property(x => x.BingoNumeros)
            .HasConversion(
                numeros => string.Join(',', numeros),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
            );
        
        // enum
        builder.Property(x => x.EventsType)
            .IsRequired()
            .HasConversion(
                category => (int)category.Value,
                value => new EventsType((EventsType.EventsTypeEnum)value)
            );

        builder.Property(events => events.IsCancelled)
            .HasDefaultValue(false);

        builder.Property(events => events.BookRevenue)
            .HasColumnType("decimal(12,2)")
            .IsRequired(false);

        // Value Object
        builder.ComplexProperty(pdt => pdt.Adresse);
    }
    
    private void ConfigureParties(EntityTypeBuilder<AssoEvents> builder)
    {
        builder.OwnsMany(assoEvent => assoEvent.Parties, gb =>
        {
            gb.ToTable("Parties");
            gb.WithOwner().HasForeignKey("AssoEventId");
            gb.HasKey("Id","AssoEventId");
            gb.Property(assoEventGiver => assoEventGiver.Id)
                .HasColumnName("PartieId")
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => PartieId.Create(value));
        
            // Configuration pour la relation entre Partie et Lot
            gb.OwnsMany(partie => partie.LineParties, lineLots =>
            {
                lineLots.ToTable("LinePartie");
                lineLots.WithOwner().HasForeignKey("LinePartieId", "AssoEventId");
                lineLots.HasKey("Id", "AssoEventId");
                lineLots.Property(assoEventGiver => assoEventGiver.Id)
                    .HasColumnName("LinePartieId")
                    .ValueGeneratedNever()
                    .HasConversion(
                        id => id.Value,
                        value => LinePartieId.Create(value));
                
                lineLots.Property(x => x.NumberLine)
                    .IsRequired()
                    .HasConversion(
                        category => (int)category.Value,
                        value => new NumberLine((NumberLine.NumberLineEnum)value)
                    ); 
                

                lineLots.OwnsMany(lP => lP.Lots, lots =>
                {
                    lots.ToTable("Lots");
                    lots.WithOwner().HasForeignKey("LotsId", "LotId");
                    lots.HasKey("Id");
                    lots.Property(a => a.Id)
                        .HasColumnName("LotId")
                        .ValueGeneratedNever()
                        .HasConversion(
                            id => id.Value,
                            value => LotId.Create(value));
                });

            });
            
            gb.Navigation(s => s.LineParties).AutoInclude();
            
            gb.Property(x => x.PartieType)
                .HasConversion(
                    partieType => partieType.Value.ToString(),
                    value => PartieType.CreateFromString(value)
                );
            
            gb.Property(x => x.LiveNumeros)
                .HasConversion(
                    numeros => string.Join(',', numeros),
                    value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                );
            
            gb.Property(x => x.LastNumeros)
                .HasConversion(
                    numeros => string.Join(',', numeros),
                    value => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                );
        });
    
        builder.Metadata.FindNavigation(nameof(AssoEvents.Parties))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

}
