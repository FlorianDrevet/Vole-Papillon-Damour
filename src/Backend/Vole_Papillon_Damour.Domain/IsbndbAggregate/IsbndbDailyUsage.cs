namespace Vole_Papillon_Damour.Domain.IsbndbAggregate;

public sealed class IsbndbDailyUsage
{
    public DateOnly Day { get; private set; }
    public IsbndbUsage Usage { get; private set; }
    public int Credits { get; private set; }

    public IsbndbDailyUsage()
    {
    }

    private IsbndbDailyUsage(DateOnly day, IsbndbUsage usage)
    {
        Day = day;
        Usage = usage;
    }

    public static IsbndbDailyUsage Create(DateOnly day, IsbndbUsage usage)
    {
        if (!Enum.IsDefined(usage))
        {
            throw new ArgumentOutOfRangeException(nameof(usage));
        }

        return new IsbndbDailyUsage(day, usage);
    }

    public void Increment(int credits = 1)
    {
        if (credits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(credits), "The credit increment must be positive.");
        }

        Credits = checked(Credits + credits);
    }
}
