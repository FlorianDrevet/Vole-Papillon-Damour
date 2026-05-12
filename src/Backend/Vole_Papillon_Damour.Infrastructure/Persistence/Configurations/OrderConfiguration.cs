using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ConfigureOrderedProductTable(builder);
        ConfigureOrderTable(builder);
    }
    private void ConfigureOrderTable(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => OrderId.Create(value)
            );
        
        // enum
        builder.Property(order => order.Status)
            .IsRequired()
            .HasConversion(
                status => (int)status.Value,
                value => new StatusEnum((StatusEnum.StatusEnumEnum)value)
            );
    }
    private void ConfigureOrderedProductTable(EntityTypeBuilder<Order> builder)
    {
        builder.OwnsMany(order => order.Products, gb =>
        {
            gb.ToTable("OrderedProducts");
            gb.WithOwner().HasForeignKey("OrderedProductId");
            gb.HasKey("Id", "OrderedProductId");
            gb.Property(order => order.Id)
                .HasColumnName("OrderId")
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => OrderedProductId.Create(value));
        });
        
        builder.Metadata.FindNavigation(nameof(Order.Products))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}