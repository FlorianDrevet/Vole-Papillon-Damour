# Caisse – ShopAppVpd

A cross-platform cash register application built with .NET MAUI.
This project is designed for associations [Vole Papillon D'amour](volepapillondamour.fr) hat need an application
to manage sales, track transactions, and stay functional even without an internet connection thanks to offline caching.

# ✨ Features
- 📱 Cross-platform: Works on Android, iOS, Windows, and Mac Catalyst with a single codebase.
- 🔄 API Integration with Refit: Strongly-typed REST API clients using Refit
- 💾 Offline caching: API data is cached locally using SQLite so the app remains usable even without an internet connection.
- ⚡ MVVM Architecture: Built with CommunityToolkit.MVVM
- ⚙️ Configuration management: Uses appsettings.json with Microsoft.Extensions.Configuration for flexible configuration.

# 🛠️ Tech Stack

- [.NET MAUI](https://learn.microsoft.com/fr-fr/dotnet/maui/?view=net-maui-9.0) – Cross-platform UI framework
- [Refit](https://github.com/reactiveui/refit) – Type-safe REST API client
- [SQLite](https://github.com/praeclarum/sqlite-net) – Local database for offline caching
- [CommunityToolkit.MVVM](https://learn.microsoft.com/fr-fr/dotnet/communitytoolkit/mvvm/) – MVVM architecture and utilities
- [ErrorOr](https://github.com/amantinband/error-or) – Error handling and result types

# 📸 Screenshots

![img.png](/img.png)
![img2.png](/img2.png)