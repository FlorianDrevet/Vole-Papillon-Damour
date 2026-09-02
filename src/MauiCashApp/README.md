# Caisse – ShopAppVpd

An Android cash register application built with .NET MAUI.
This project is designed for associations [Vole Papillon D'amour](volepapillondamour.fr) that need an application
to manage sales, track transactions, and stay functional even without an internet connection thanks to offline caching.

# ✨ Features
- 📱 Android only: The supported target is `net10.0-android` for phones and tablets.
- 📦 Distribution: the application is currently installed from the direct app build; a durable signing keystore and organized redistribution will be defined later.
- 🔄 API Integration with Refit: Strongly-typed REST API clients using Refit
- 🔑 Entra authentication: MSAL.NET acquires the API token silently first, then opens the interactive Android sign-in when required.
- 💾 Offline caching: API data is cached locally using SQLite so the app remains usable even without an internet connection.
- ⚡ MVVM Architecture: Built with CommunityToolkit.MVVM
- ⚙️ Configuration management: Uses appsettings.json with Microsoft.Extensions.Configuration for flexible configuration.

# 🛠️ Tech Stack

- [.NET MAUI](https://learn.microsoft.com/fr-fr/dotnet/maui/?view=net-maui-10.0) – Android UI framework
- [MSAL.NET](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/authentication?view=net-maui-10.0) – Entra External ID authentication
- [Refit](https://github.com/reactiveui/refit) – Type-safe REST API client
- [SQLite](https://github.com/praeclarum/sqlite-net) – Local database for offline caching
- [CommunityToolkit.MVVM](https://learn.microsoft.com/fr-fr/dotnet/communitytoolkit/mvvm/) – MVVM architecture and utilities
- [ErrorOr](https://github.com/amantinband/error-or) – Error handling and result types

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
