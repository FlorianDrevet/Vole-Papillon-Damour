namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class SocialFeedAuthenticationException : Exception
{
    public SocialFeedAuthenticationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
