using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Events.Responses;

/*
public class BingoCardResponse
{
    public BingoCard BingoCard { get; set; }
    public Guid EventId { get; set; }
}*/

public class BingoCardResponse
{
    public int?[] FirstLine { get; set; }
    public int?[] SecondLine { get; set; }
    public int?[] ThirdLine { get; set; }
}