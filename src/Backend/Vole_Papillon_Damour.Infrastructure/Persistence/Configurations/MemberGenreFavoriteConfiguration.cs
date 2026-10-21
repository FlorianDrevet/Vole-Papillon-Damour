using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class MemberGenreFavoriteConfiguration : IEntityTypeConfiguration<MemberGenreFavorite>
{
    public void Configure(EntityTypeBuilder<MemberGenreFavorite> builder)
    {
        builder.ToTable("MemberGenreFavorites");
        builder.HasKey(favorite => new {favorite.UserId, favorite.Genre});
        builder.Property(favorite => favorite.UserId)
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(favorite => favorite.Genre)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AI")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
