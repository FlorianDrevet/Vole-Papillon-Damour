using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

public sealed class AdminPerformanceIndexesTests
{
    [Fact]
    public void Configuration_DeclaresTheAdminReadIndexes()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer("Server=localhost;Database=model-only;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new ProjectDbContext(options);

        IReadOnlyCollection<string> IndexesOf(Type entityType) => context.Model
            .FindEntityType(entityType)!
            .GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToArray();

        IndexesOf(typeof(ScanSession)).Should().Contain("StartedAt").And.Contain("Status");
        IndexesOf(typeof(OutboxMessage)).Should().Contain("Kind,DueAt").And.Contain("MemberId");
        IndexesOf(typeof(BookNotFoundReport)).Should().Contain("Status,ClosedAt");
    }
}
