namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record ImportAdminBooksRequest(IReadOnlyList<string>? Isbns);
