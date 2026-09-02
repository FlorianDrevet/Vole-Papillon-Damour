using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Identity.Client;

namespace ShopAppVpd;

[Activity(
    Exported = true,
    LaunchMode = LaunchMode.SingleTask,
    NoHistory = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataHost = "auth",
    DataScheme = "msal427c90de-bf59-4b01-af63-dc0799248496")]
public sealed class MsalActivity : BrowserTabActivity
{
}
