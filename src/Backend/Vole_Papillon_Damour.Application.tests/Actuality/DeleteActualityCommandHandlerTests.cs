using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;

namespace Vole_Papillon_Damour.Application.tests.Actuality;

public sealed class DeleteActualityCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeletesTheActualityImagesAfterDeletingTheAggregate()
    {
        var principalImage = new Uri("https://storage.example.test/actuality-images/principal.jpg");
        var galleryImage = new Uri("https://storage.example.test/actuality-images/gallery.jpg");
        var actuality = ActualityAggregate.Create(
            "Une actualité",
            "Un article",
            principalImage,
            null,
            null,
            [galleryImage],
            DateTimeOffset.UtcNow);
        var repository = Substitute.For<IActualityRepository>();
        repository.GetByIdAsync(actuality.Id).Returns(actuality);
        repository.DeleteAsync(actuality.Id).Returns(true);
        var blobService = Substitute.For<IBlobService>();
        blobService.DeleteFileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(string.Empty));
        var handler = new DeleteActualityCommandHandler(
            repository,
            blobService,
            NullLogger<DeleteActualityCommandHandler>.Instance);

        var result = await handler.Handle(
            new DeleteActualityCommand(actuality.Id),
            CancellationToken.None);

        result.Value.Should().BeTrue();
        await blobService.Received(1).DeleteFileAsync(principalImage.ToString());
        await blobService.Received(1).DeleteFileAsync(galleryImage.ToString());
    }
}
