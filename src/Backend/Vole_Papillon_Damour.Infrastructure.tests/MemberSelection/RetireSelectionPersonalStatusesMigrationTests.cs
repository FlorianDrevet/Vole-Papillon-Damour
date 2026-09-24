using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.MemberSelection;

public sealed class RetireSelectionPersonalStatusesMigrationTests
{
    [Fact]
    public void Migration_ConvertsRecentNotFoundOnAvailableEditionToOpenReport()
    {
        var sql = GetUpSql();

        sql.Should().Contain("INSERT INTO BookNotFoundReports");
        sql.Should().Contain("SELECT NEWID(), s.UserId, s.Isbn13, NULL, NULL, NULL, 0, s.StatusChangedAt");
        sql.Should().Contain("s.Status = 2 AND s.Isbn13 IS NOT NULL");
        sql.Should().Contain("s.StatusChangedAt >= DATEADD(day, -30, SYSUTCDATETIME())");
        sql.Should().Contain("b.QuantityAvailable > 0 AND b.IsHiddenFromCatalog = 0 AND b.RedirectedToIsbn13 IS NULL");
    }

    [Fact]
    public void Migration_ResetsToRevisitAndOldNotFoundToToTake()
    {
        var sql = GetUpSql();

        sql.Should().Contain("UPDATE MemberSelectionItems SET Status = 0 WHERE Status IN (2, 3)");
    }

    private static string GetUpSql()
    {
        var migrationType = typeof(ProjectDbContext).Assembly.GetTypes()
            .SingleOrDefault(type => type.Name == "RetireSelectionPersonalStatuses");
        migrationType.Should().NotBeNull();

        var migration = (Migration)Activator.CreateInstance(migrationType!, nonPublic: true)!;
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = migrationType!.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        up.Should().NotBeNull();
        up!.Invoke(migration, [builder]);

        return string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }
}
