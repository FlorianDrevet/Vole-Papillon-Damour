# Caisse – ShopAppVpd

An Android product picker for the event cash desks of [Vole Papillon D'amour](volepapillondamour.fr).
It loads the public product catalog, shows only available Buvette and Loto items, and keeps an available-product cache on the tablet.
The basket calculates a total for manual payment; this app does not persist completed sales.

# ✨ Features
- 📱 Android only: The supported target is `net10.0-android` for phones and tablets.
- 📦 Distribution: the application is currently installed from the direct app build; a durable signing keystore and organized redistribution will be defined later.
- 🔄 API Integration with Refit: Strongly-typed REST API clients using Refit
- 🔓 No tablet sign-in: the deployed `GET /product` endpoint is public.
- 💾 Offline catalog: available API products are cached locally using SQLite; cached disabled products are filtered out.
- ⚡ MVVM Architecture: Built with CommunityToolkit.MVVM
- ⚙️ Configuration management: Uses appsettings.json with Microsoft.Extensions.Configuration for flexible configuration.

This APK is signed with the local Android debug key. The current package has the same app ID as
the earlier APK but a different signing certificate, so uninstall an older installation before
installing this build; uninstalling also clears the tablet's local product cache. A shared release
key is needed for future in-place updates.

# 🛠️ Tech Stack

- [.NET MAUI](https://learn.microsoft.com/fr-fr/dotnet/maui/?view=net-maui-10.0) – Android UI framework
- [Refit](https://github.com/reactiveui/refit) – Type-safe REST API client
- [SQLite](https://github.com/praeclarum/sqlite-net) – Local database for offline caching
- [CommunityToolkit.MVVM](https://learn.microsoft.com/fr-fr/dotnet/communitytoolkit/mvvm/) – MVVM architecture and utilities

# 🔨 Local build

From the repository root:

```powershell
dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android
```

The durable signing keystore and the organized APK redistribution channel are intentionally
deferred. The current workflow uses the direct application build.

# 📸 Screenshots

![img.png](/img.png)
![img2.png](/img2.png)
