using FluentAssertions;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.tests.Actuality;

public sealed class CaptionCleanerTests
{
    [Fact]
    public void Clean_RemovesTerminalHashtagsAndKeepsParagraphsEmojiAndMentions()
    {
        var caption = "Bienvenue à la fête 🎉\n\nMerci @volepapillon\n#association #bénévoles";

        CaptionCleaner.Clean(caption)
            .Should()
            .Be("Bienvenue à la fête 🎉\n\nMerci @volepapillon");
    }

    [Fact]
    public void Clean_RemovesHashtagOnlyLinesAndCollapsesBlankLines()
    {
        var caption = "Premier paragraphe\n\n\n#livres\n\nDeuxième paragraphe\n#lecture";

        CaptionCleaner.Clean(caption)
            .Should()
            .Be("Premier paragraphe\n\nDeuxième paragraphe");
    }

    [Fact]
    public void Clean_ReturnsAnEmptyArticleForAnEmptyCaption()
    {
        CaptionCleaner.Clean("  \n #association ").Should().BeEmpty();
    }
}
