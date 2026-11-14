using Android.App;
using Android.Content;
using Android.Content.PM;

namespace ShopAppVpd.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AppUpdateInstallReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;

        var status = intent.GetIntExtra(
            PackageInstaller.ExtraStatus,
            (int)PackageInstallStatus.Failure);
        if (status != (int)PackageInstallStatus.PendingUserAction)
            return;

        var confirmationIntent = GetConfirmationIntent(intent);
        if (confirmationIntent is null)
            return;

        confirmationIntent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(confirmationIntent);
    }

    private static Intent? GetConfirmationIntent(Intent statusIntent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return statusIntent.GetParcelableExtra(
                Intent.ExtraIntent,
                Java.Lang.Class.FromType(typeof(Intent))) as Intent;
        }

        return statusIntent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
    }
}
