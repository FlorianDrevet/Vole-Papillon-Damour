using System.Text.Json.Serialization;

namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminBooksImportResponse(
    int RequestedCount,
    int CreatedCount,
    int AlreadyPresentCount,
    IReadOnlyList<AdminBooksImportResponse.Item> Results)
{
    public sealed record Item(string Isbn13, Status Status);

    [JsonConverter(typeof(JsonStringEnumConverter<Status>))]
    public enum Status
    {
        [JsonStringEnumMemberName("created")]
        Created,

        [JsonStringEnumMemberName("alreadyPresent")]
        AlreadyPresent
    }
}
