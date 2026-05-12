using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public class NumberLine : EnumValueObject<NumberLine.NumberLineEnum>
{
    public enum NumberLineEnum
    {
        OneLine = 0,
        TwoLine = 1,
        CartonPlein = 2,
        Unknown
    }

    public NumberLine()
    {
    }

    public NumberLine(NumberLineEnum value): base(value)
    {
    }
    
    public static NumberLine CreateFromString(string? status)
    {
        return new NumberLine(ParseOrDefault(status, NumberLineEnum.Unknown));
    }
}