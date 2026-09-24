using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public sealed class MemberRecommendationPreferenceConfiguration : IEntityTypeConfiguration<MemberRecommendationPreference>
{
    public void Configure(EntityTypeBuilder<MemberRecommendationPreference> builder)
    {
        builder.ToTable("MemberRecommendationPreferences");
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.UserId)
            .IsRequired()
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value));
        builder.Property(preference => preference.Enabled).IsRequired();
        builder.Property(preference => preference.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(BookPersistenceConversions.UtcDateTimeConverter)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
