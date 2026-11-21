using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed class AddBookPhotoRequest
{
    public IFormFile? File { get; set; }
}

public sealed record BookPhotoResponse(Guid Id, string Url, int Position, string ContentType);

public sealed record CreatedBookWithoutIsbnResponse(string Code, int QuantityAvailable);