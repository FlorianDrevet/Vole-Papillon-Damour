using FluentAssertions;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.tests.Actuality;

public sealed class FallbackTitleTests
{
    [Fact]
    public void Create_UsesThePublicationDateInFrench()
    {
        var date = new DateTimeOffset(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);

        FallbackTitle.Create(date).Should().Be("Actualité du 10 septembre 2026");
    }
}
