using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public class PartieType : EnumValueObject<PartieType.PartieTypeEnum>
{
    public enum PartieTypeEnum
    {
        Standard,
        Americaine,
        PlusUnMoinsUn,
        CartonPlein,
        Bingo,
        Unknown
    }

    public PartieType(PartieTypeEnum value) : base(value)
    {
    }
    
    public static PartieType CreateFromString(string? status)
    {
        return new PartieType(ParseOrDefault(status, PartieTypeEnum.Unknown));
    }
}