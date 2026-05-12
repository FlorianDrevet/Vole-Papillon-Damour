namespace Vole_Papillon_Damour.Domain.Common.Models;

public abstract class EnumValueObject<TEnum> : ValueObject where TEnum : struct, Enum
{
    public TEnum Value { get; protected set; }

    protected EnumValueObject()
    {
    }

    protected EnumValueObject(TEnum value)
    {
        this.Value = value;
    }

    public static TEnum ParseOrDefault(string? status, TEnum defaultValue)
    {
        if (Enum.TryParse<TEnum>(status, true, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public static TEnum GetDefaultValue()
    {
        return default;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
