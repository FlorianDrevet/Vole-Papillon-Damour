using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public sealed class LiveTheme : EnumValueObject<LiveTheme.LiveThemeEnum>
{
    public enum LiveThemeEnum
    {
        Classic = 0,
        Halloween = 1
    }

    public LiveTheme(LiveThemeEnum value) : base(value)
    {
    }

    public static LiveTheme CreateFromString(string? value)
    {
        return new LiveTheme(ParseOrDefault(value, LiveThemeEnum.Classic));
    }
}
