namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class SocialFeedQuotaException : Exception
{
    public SocialFeedQuotaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
