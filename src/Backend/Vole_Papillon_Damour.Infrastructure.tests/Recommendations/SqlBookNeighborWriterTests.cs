using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class SqlBookNeighborWriterTests
{
    [Fact]
    public void DeleteReplacedRowsSql_UsesReservedWordSafeSql()
    {
        var command = SqlBookNeighborWriter.BuildDeleteReplacedRowsSql(Guid.Empty);

        command.Format.Should().Contain("DELETE FROM [dbo].[BookNeighbors]");
        command.Format.Should().Contain("[GenerationId] = {0}");
        command.Format.Should().Contain("[Isbn13] IN (SELECT [Isbn13] FROM #ReplacedIsbns)");
        command.Format.Should().NotContain(" AS current");
    }
}
