using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

public class StatusEnum : EnumValueObject<StatusEnum.StatusEnumEnum>
{
    public enum StatusEnumEnum
    {
        New,
        Ready,
        Done,
        Unknown
    }

    public StatusEnum(StatusEnumEnum value) : base(value)
    {
    }

    public static StatusEnum CreateFromString(string? status)
    {
        return new StatusEnum(ParseOrDefault(status, StatusEnum.StatusEnumEnum.Unknown));
    }
}