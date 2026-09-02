using Microsoft.Identity.Client;
using Microsoft.Maui.ApplicationModel;
using ShopAppVpd.Interfaces;

namespace ShopAppVpd.Services;

internal sealed class MsalAuthService : IAuthService
{
    private const string ClientId = "427c90de-bf59-4b01-af63-dc0799248496";
    private const string Authority = "https://volepapillondamour.ciamlogin.com/";
    private const string ApiScope = "api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user";
    private static readonly string[] Scopes = [ApiScope];

    private readonly IPublicClientApplication _publicClientApplication;
    private IAccount? _cachedAccount;

    public MsalAuthService()
    {
        _publicClientApplication = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(Authority)
            .WithRedirectUri($"msal{ClientId}://auth")
            .Build();
    }

    public async Task<string> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _publicClientApplication.GetAccountsAsync();
        _cachedAccount = accounts.FirstOrDefault();

        if (_cachedAccount is not null)
        {
            try
            {
                var silentResult = await _publicClientApplication
                    .AcquireTokenSilent(Scopes, _cachedAccount)
                    .ExecuteAsync(cancellationToken);

                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // An interactive sign-in is required when no usable cached token exists.
            }
        }

        var interactiveBuilder = _publicClientApplication
            .AcquireTokenInteractive(Scopes);

#if ANDROID
        interactiveBuilder = interactiveBuilder.WithParentActivityOrWindow(
            Platform.CurrentActivity
            ?? throw new InvalidOperationException(
                "No current Android Activity. Ensure Platform.Init() is called in MainActivity.OnCreate."));
#endif

        var interactiveResult = await interactiveBuilder.ExecuteAsync(cancellationToken);
        _cachedAccount = interactiveResult.Account;
        return interactiveResult.AccessToken;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var accounts = (await _publicClientApplication.GetAccountsAsync()).ToList();
        foreach (var account in accounts)
        {
            await _publicClientApplication.RemoveAsync(account);
        }

        _cachedAccount = null;
    }
}
