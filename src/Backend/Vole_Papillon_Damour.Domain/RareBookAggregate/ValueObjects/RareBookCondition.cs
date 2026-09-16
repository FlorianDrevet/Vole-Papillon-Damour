using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

public sealed class RareBookCondition : EnumValueObject<RareBookCondition.RareBookConditionEnum>
{
    public enum RareBookConditionEnum : byte
    {
        AsNew,
        GoodWithFlaws,
        Worn,
        Damaged
    }

    public RareBookCondition() : base(RareBookConditionEnum.AsNew)
    {
    }

    public RareBookCondition(RareBookConditionEnum value) : base(EnsureDefined(value))
    {
    }

    public static RareBookCondition AsNew => new(RareBookConditionEnum.AsNew);
    public static RareBookCondition GoodWithFlaws => new(RareBookConditionEnum.GoodWithFlaws);
    public static RareBookCondition Worn => new(RareBookConditionEnum.Worn);
    public static RareBookCondition Damaged => new(RareBookConditionEnum.Damaged);

    public static RareBookCondition CreateFromString(string? value)
    {
        if (!Enum.TryParse<RareBookConditionEnum>(value, true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new ArgumentException("The rare book condition is not supported.", nameof(value));
        }

        return new RareBookCondition(parsed);
    }

    public static bool TryCreate(string? value, out RareBookCondition condition)
    {
        try
        {
            condition = CreateFromString(value);
            return true;
        }
        catch (ArgumentException)
        {
            condition = null!;
            return false;
        }
    }

    private static RareBookConditionEnum EnsureDefined(RareBookConditionEnum value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The rare book condition is not supported.");
        }

        return value;
    }
}
