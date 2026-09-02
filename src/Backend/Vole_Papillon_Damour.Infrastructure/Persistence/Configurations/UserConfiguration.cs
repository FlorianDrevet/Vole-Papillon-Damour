using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

public class UserConfiguration: IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ConfigureUsersTable(builder);
    }

    private void ConfigureUsersTable(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value)
            );

        builder.Property(user => user.ExternalId)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.HasIndex(user => user.ExternalId)
            .IsUnique()
            .HasFilter("[ExternalId] IS NOT NULL");

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired(false);

        builder.Property(user => user.CreatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(user => user.LastSeenAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(user => user.AnonymizedAt)
            .HasColumnType("datetime2")
            .IsRequired(false);

        builder.Ignore(user => user.Password);
        builder.Ignore(user => user.Salt);
        builder.Ignore(user => user.Role);

        builder.ComplexProperty(user => user.Name);
    }
}
