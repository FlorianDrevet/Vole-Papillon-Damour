namespace Vole_Papillon_Damour.Application.Common.Models;

public class BingoCard
{
    public int?[] FirstLine { get; }
    public int?[] SecondLine { get; }
    public int?[] ThirdLine { get; }
    
    public BingoCard
    (
        int?[] firstLine,
        int?[] secondLine,
        int?[] thirdLine
    )
    {
        if (firstLine.Length != 5)
            throw new ArgumentException("FirstLine must contain exactly 5 numbers.", nameof(firstLine));
        
        if (secondLine.Length != 5)
            throw new ArgumentException("SecondLine must contain exactly 5 numbers.", nameof(secondLine));
        
        if (thirdLine.Length != 5)
            throw new ArgumentException("ThirdLine must contain exactly 5 numbers.", nameof(thirdLine));

        FirstLine = firstLine.OrderBy(x => x).ToArray();
        SecondLine = secondLine.OrderBy(x => x).ToArray();
        ThirdLine = thirdLine.OrderBy(x => x).ToArray();
    }
}