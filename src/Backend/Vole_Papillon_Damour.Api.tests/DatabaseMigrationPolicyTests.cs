using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Vole_Papillon_Damour.Api.Common;

namespace Vole_Papillon_Damour.Api.tests;

public sealed class DatabaseMigrationPolicyTests
{
    [Fact]
    public void Development_runs_migrations_at_startup_for_local_feedback()
    {
        DatabaseMigrationPolicy.ShouldRunOnStartup(Environments.Development)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public void Non_development_environments_require_an_explicit_migration_step(
        string environmentName)
    {
        DatabaseMigrationPolicy.ShouldRunOnStartup(environmentName)
            .Should()
            .BeFalse();
    }
}
