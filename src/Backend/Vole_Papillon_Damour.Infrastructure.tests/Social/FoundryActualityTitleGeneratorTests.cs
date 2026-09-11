using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Infrastructure.Services.Ai;

namespace Vole_Papillon_Damour.Infrastructure.tests.Social;

public sealed class FoundryActualityTitleGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsValidatedTitleAndSendsOnlyTheArticleAsUserContent()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "La fête du livre revient bientôt")));

        var generator = CreateGenerator(chatClient);

        var result = await generator.GenerateAsync(
            "Le programme de la fête du livre est maintenant disponible.",
            CancellationToken.None);

        result.Should().Be("La fête du livre revient bientôt");
        await chatClient.Received(1).GetResponseAsync(
                Arg.Is<IEnumerable<ChatMessage>>(messages =>
                messages.Select(message => message.Text).SequenceEqual(
                new[]
                {
                    "Tu proposes un titre court en français pour une actualité associative. Réponds uniquement par le titre, sans guillemets ni préfixe.",
                    "Le programme de la fête du livre est maintenant disponible.",
                })),
            Arg.Is<ChatOptions>(options =>
                options.Temperature == 0.2f && options.MaxOutputTokens == 40),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNullWhenTheModelBreaksTheEditorialContract()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Titre trop court")));

        var generator = CreateGenerator(chatClient);

        var result = await generator.GenerateAsync("Un article associatif.", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_TruncatesTheArticleSentToTheModel()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "La fête du livre revient bientôt")));
        var article = new string('a', 2_100);
        var generator = CreateGenerator(chatClient);

        await generator.GenerateAsync(article, CancellationToken.None);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => messages.Last().Text.Length == 2_000),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_PropagatesExternalCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<ChatResponse>(cancellation.Token));

        var generator = CreateGenerator(chatClient);

        var action = () => generator.GenerateAsync("Un article associatif.", cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static FoundryActualityTitleGenerator CreateGenerator(IChatClient chatClient)
    {
        return new FoundryActualityTitleGenerator(
            chatClient,
            Options.Create(new TitleGenerationOptions()),
            NullLogger<FoundryActualityTitleGenerator>.Instance);
    }
}
