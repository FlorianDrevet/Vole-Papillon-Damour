using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.MemberCards;

public sealed class MemberCardConfigurationTests
{
    [Fact]
    public void Model_MapsMemberCardAndItsUniquenessRules()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        typeof(IProjectDbContext).GetProperty(nameof(IProjectDbContext.MemberCards)).Should().NotBeNull();
        typeof(ProjectDbContext).GetProperty(nameof(ProjectDbContext.MemberCards)).Should().NotBeNull();

        var cards = model.FindEntityType(typeof(MemberCard))!;
        cards.GetTableName().Should().Be("MemberCards");
        cards.FindProperty(nameof(MemberCard.RecoveryCode))!.GetMaxLength().Should().Be(11);
        cards.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(MemberCard.UserId) }));
        cards.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.GetFilter() == "[RevokedAt] IS NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(MemberCard.RecoveryCode) }));
        cards.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Vole_Papillon_Damour.Domain.UserAggregate.User) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    private static ProjectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=VpdMemberCardModelTests;Trusted_Connection=True;")
            .Options;
        return new ProjectDbContext(options);
    }
}
