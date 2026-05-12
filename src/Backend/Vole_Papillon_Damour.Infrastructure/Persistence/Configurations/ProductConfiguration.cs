using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public class ProductConfiguration: IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        ConfigureProductTable(builder);
        ConfigurePromotionTable(builder);
    }

    private void ConfigureProductTable(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => ProductId.Create(value)
            );
        
        builder.Property(x => x.ProductCategory)
            .HasConversion(
                category => (int)category.Value,
                value => new ProductCategory((ProductCategory.ProductCategoryEnum)value)
            );
        
        builder.Property(x => x.ProductSection)
            .IsRequired()
            .HasConversion(
                category => (int)category.Value,
                value => new ProductSection((ProductSection.ProductSectionEnum)value)
            );
    }
    
    private void ConfigurePromotionTable(EntityTypeBuilder<Product> builder)
    {
        builder.OwnsMany(p => p.Promotions, a =>
        {
            a.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnName("PromotionId");

            a.ToTable("Promotions");

            a.WithOwner().HasForeignKey("ProductId");
        });

        
        builder.Metadata.FindNavigation(nameof(Product.Promotions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

}