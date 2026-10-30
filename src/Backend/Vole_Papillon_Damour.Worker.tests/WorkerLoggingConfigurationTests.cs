using FluentAssertions;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class WorkerLoggingConfigurationTests
{
    [Theory]
    [InlineData("Azure.Core.1", LogLevel.Information, false)]
    [InlineData("DurableTask-AzureStorage", LogLevel.Information, false)]
    [InlineData("Vole_Papillon_Damour.Worker.BookSweepFunction", LogLevel.Information, true)]
    [InlineData("Vole_Papillon_Damour.Application.Actuality.ImportSocialActualitiesCommandHandler", LogLevel.Information, true)]
    [InlineData("Vole_Papillon_Damour.Infrastructure.Services.Bibliographic.BibliographicMetadataResolver", LogLevel.Information, true)]
    [InlineData("Microsoft.Hosting.Lifetime", LogLevel.Information, true)]
    [InlineData("Azure.Core.1", LogLevel.Warning, true)]
    [InlineData("Vole_Papillon_Damour.Worker.BookSweepFunction", LogLevel.Debug, false)]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information, false)]
    [InlineData("System.Net.Http.HttpClient", LogLevel.Information, false)]
    [InlineData("Azure.Core.1", LogLevel.None, false)]
    public void Production_console_filter_preserves_diagnostic_summaries_and_warnings(
        string category,
        LogLevel level,
        bool expected)
    {
        WorkerLoggingConfiguration.ShouldLogToConsole(category, level)
            .Should()
            .Be(expected);
    }
}
