using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicMetadataResolver(
    IBnfSruClient bnfClient,
    IOpenLibraryClient openLibraryClient,
    ILogger<BibliographicMetadataResolver> logger) : IBibliographicMetadataResolver
{
    public async Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var bnfResult = await TryFindAsync(
            bnfClient,
            "BnF",
            isbn13,
            cancellationToken);
        if (bnfResult.Metadata is not null)
        {
            return bnfResult.Metadata;
        }

        var openLibraryResult = await TryFindAsync(
            openLibraryClient,
            "OpenLibrary",
            isbn13,
            cancellationToken);

        if (openLibraryResult.Metadata is not null)
        {
            return openLibraryResult.Metadata;
        }

        if (bnfResult.Failed || openLibraryResult.Failed)
        {
            throw new HttpRequestException(
                $"All bibliographic providers that were contacted failed for ISBN {isbn13.Value}.");
        }

        return null;
    }

    private async Task<ProviderResult> TryFindAsync(
        object client,
        string source,
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ProviderResult(client switch
            {
                IBnfSruClient bnf => await bnf.FindAsync(isbn13, cancellationToken),
                IOpenLibraryClient openLibrary => await openLibrary.FindAsync(isbn13, cancellationToken),
                _ => null
            }, Failed: false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{Source} metadata lookup timed out for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "{Source} metadata lookup failed for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "{Source} returned invalid metadata for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (XmlException exception)
        {
            logger.LogWarning(exception, "{Source} returned invalid metadata for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
    }

    private sealed record ProviderResult(BookMetadataResult? Metadata, bool Failed);
}
