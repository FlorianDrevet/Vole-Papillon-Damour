using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;
using ShopAppVpd.Apis;
using ShopAppVpd.Databases;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Services;
using ShopAppVpd.Settings;
using ShopAppVpd.ViewModels;

namespace ShopAppVpd;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("CaveatBrush-Regular.ttf", "CaveatBrushRegular");
            });
        
        var assembly = Assembly.GetExecutingAssembly();
        var appSettings = $"{assembly.GetName().Name}.appsettings.json";
        using var stream = assembly.GetManifestResourceStream(appSettings);
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
        builder.Configuration.AddConfiguration(config);
        
        builder.Services.AddSingleton<ProductDatabase>();
        builder.Services.AddTransient<IProductService, ProductService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();
        
        var vpdSettings = config.GetSection("VpdSettings");
        builder.Services
            .AddRefitClient<IVpdApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(vpdSettings.GetValue<string>(nameof(VpdSettings.BaseUrl)) ?? throw new InvalidOperationException()));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}