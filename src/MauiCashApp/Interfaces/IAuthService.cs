namespace ShopAppVpd.Interfaces;

public interface IAuthService
{
    Task<string> AcquireAccessTokenAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
