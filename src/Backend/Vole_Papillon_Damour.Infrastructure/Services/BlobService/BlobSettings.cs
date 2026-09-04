namespace Vole_Papillon_Damour.Infrastructure.Services.BlobService;

public class BlobSettings
{
    public const string SectionName = "BlobSettings";
    public string ContainerName { get; init; } = null!;
    public string ContainerActualityImagesName { get; init; } = null!;
    public string BlobContainerEventImagesClient { get; init; } = null!;
    public string BlobContainerProductsImagesClient { get; init; } = null!;
    public string BlobContainerBookCoversName { get; init; } = "book-covers";
}
