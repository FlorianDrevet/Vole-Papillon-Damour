using ErrorOr;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Testcontainers.MsSql;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

// Test d'intégration sur un vrai SQL Server (Testcontainers) : toutes les migrations sont appliquées.
// Il faut Docker, ou Podman via DOCKER_HOST (npipe:////./pipe/docker_engine sous Windows). Pour l'exclure
// dans un environnement sans conteneurs : dotnet test --filter "Category!=SqlServerIntegration".
[Trait("Category", "SqlServerIntegration")]
public sealed class SqlServerMigrationsIntegrationTests : IAsyncLifetime
{
    // Dernière migration avant la séquence : le compteur d'origine existe encore.
    private const string MigrationBeforeSequence = "20261119204819_InternalBookCodeSequence";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000042"));

    private MsSqlContainer _container = null!;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().WithImage(SqlServerImage).Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task AllMigrations_ApplyOnSqlServer_AndTheSequenceStartsAfterTheLastIssuedNumber()
    {
        var connectionString = await CreateDatabaseAsync();
        await using (var context = CreateContext(connectionString))
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeSequence);
            // Le compteur d'origine a déjà émis 42 codes : la séquence doit reprendre à 43 (RG-171).
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE [InternalBookCodeSequences] SET [LastIssuedNumber] = 42 WHERE [Id] = 1");

            await context.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var counterTable = await ScalarAsync(connection, "SELECT OBJECT_ID(N'dbo.InternalBookCodeSequences')");
        counterTable.Should().Be(DBNull.Value, "l'ancien compteur est supprimé par la migration");
        var next = await ScalarAsync(connection, "SELECT NEXT VALUE FOR [dbo].[InternalBookCodeNumbers]");
        Convert.ToInt64(next).Should().Be(43);
    }

    [Fact]
    public async Task ConcurrentCreationsWithoutIsbn_BothSucceedWithDifferentCodes()
    {
        var connectionString = await CreateDatabaseAsync();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.MigrateAsync();
        }

        await SeedVolunteerAsync(connectionString);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var first =CreateBookWithoutIsbnAsync(connectionString, clock, "Premier");
        var second = CreateBookWithoutIsbnAsync(connectionString, clock, "Second");

        var results = await Task.WhenAll(first, second);

        results.Should().OnlyContain(result => !result.IsError);
        results[0].Value.Isbn13.Should().NotBe(results[1].Value.Isbn13);
        results.Select(result => result.Value.Isbn13).Should().OnlyContain(code => BookCodeIsInternal(code));
    }

    [Fact]
    public async Task CheckConstraint_RejectsAnInternalFlagThatContradictsTheKey()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        await SeedVolunteerAsync(connectionString);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var created =await CreateBookWithoutIsbnAsync(connectionString, clock, "Titre");
        created.IsError.Should().BeFalse();

        var contradiction = () => context.Database.ExecuteSqlRawAsync(
            "UPDATE [Books] SET [IsInternalCode] = 0 WHERE [Isbn13] = {0}",
            created.Value.Isbn13);

        await contradiction.Should().ThrowAsync<SqlException>()
            .Where(exception => exception.Number == 547);
    }

    private static async Task<ErrorOr<AdminBookOperationResult>> CreateBookWithoutIsbnAsync(
        string connectionString,
        IDateTimeProvider clock,
        string title)
    {
        await using var context = CreateContext(connectionString);
        var handler = new CreateBookWithoutIsbnCommandHandler(
            context,
            clock,
            new SqlServerInternalBookCodeIssuer(context));
        return await handler.Handle(
            new CreateBookWithoutIsbnCommand(title, 1, Volunteer),
            CancellationToken.None);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"vpd_it_{Guid.NewGuid():N}";
        await using (var master = new SqlConnection(_container.GetConnectionString()))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", master);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = databaseName,
        };
        return builder.ConnectionString;
    }

    // Les mouvements de stock référencent leur bénévole (FK vers Users) : il doit exister en base.
    private static async Task SeedVolunteerAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        context.Users.Add(User.CreateFromExternalIdentity(
            Volunteer,
            "integration-volunteer",
            "volunteer@example.com",
            Now));
        await context.SaveChangesAsync();
    }

    private static ProjectDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private static bool BookCodeIsInternal(string code) =>
        Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.BookCode.IsValidInternalCode(code);
}
