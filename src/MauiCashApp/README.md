# Caisse – ShopAppVpd

An Android product picker for the event cash desks of [Vole Papillon D'amour](volepapillondamour.fr).
It loads the public product catalog, shows only available Buvette and Loto items, and keeps an available-product cache on the tablet.
The basket calculates a total for manual payment; this app does not persist completed sales.

# ✨ Features
- 📱 Android only: The supported target is `net10.0-android` for phones and tablets.
- 📦 Distribution: BackOffice serves a standalone Android APK that includes its managed assemblies.
- 🔄 API Integration with Refit: Strongly-typed REST API clients using Refit
- 🔓 No tablet sign-in: the deployed `GET /product` endpoint is public.
- 💾 Offline catalog: available API products are cached locally using SQLite; cached disabled products are filtered out.
- ⚡ MVVM Architecture: Built with CommunityToolkit.MVVM
- ⚙️ Configuration management: Uses appsettings.json with Microsoft.Extensions.Configuration for flexible configuration.

The APK is currently signed with an Android debug key. Keep using the same signing certificate
when replacing the BackOffice download so installed devices can update in place; uninstalling an
older package with a different certificate clears its local product cache. A durable release key
is still needed for a managed signing workflow.

# 🛠️ Tech Stack

- [.NET MAUI](https://learn.microsoft.com/fr-fr/dotnet/maui/?view=net-maui-10.0) – Android UI framework
- [Refit](https://github.com/reactiveui/refit) – Type-safe REST API client
- [SQLite](https://github.com/praeclarum/sqlite-net) – Local database for offline caching
- [CommunityToolkit.MVVM](https://learn.microsoft.com/fr-fr/dotnet/communitytoolkit/mvvm/) – MVVM architecture and utilities

# 🔨 Local build

From the repository root:

```powershell
dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android --configuration Release -p:AndroidPackageFormat=apk
```

`EmbedAssembliesIntoApk=true` is set in the project because BackOffice serves the APK as a direct download. This disables Fast Deployment and packages the managed assemblies inside the APK. The generated APK is available under `src/MauiCashApp/bin/Release/net10.0-android/`.

Copy each release to a versioned BackOffice asset such as
`src/BackOffice/public/apks/caisse-app-v1.2.apk`, then update the login and `/caisse`
download links to that same filename. A new path avoids Cloudflare serving the previous APK
from the stable URL's cache.

The durable signing keystore and the organized APK redistribution channel are intentionally deferred. The current workflow uses the direct application build.

# 📸 Screenshots

![img.png](/img.png)
![img2.png](/img2.png)
