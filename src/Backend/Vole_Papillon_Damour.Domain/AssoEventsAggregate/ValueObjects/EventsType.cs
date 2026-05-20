using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

public class EventsType : EnumValueObject<EventsType.EventsTypeEnum>
{
    public enum EventsTypeEnum
    {
        Bingo,
        Books,
        Other,
        Unknown
    }
    public EventsType(EventsTypeEnum value) : base(value)
    {
    }
    
    public static EventsType CreateFromString(string? status)
    {
        return new EventsType(ParseOrDefault(status, EventsTypeEnum.Unknown));
    }
}