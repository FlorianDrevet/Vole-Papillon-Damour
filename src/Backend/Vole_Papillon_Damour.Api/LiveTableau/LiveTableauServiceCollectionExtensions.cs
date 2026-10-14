namespace Vole_Papillon_Damour.Api.LiveTableau;

public static class LiveTableauServiceCollectionExtensions
{
    public static IServiceCollection AddLiveTableau(this IServiceCollection services)
    {
        services.AddOptions<LiveTableauOptions>().BindConfiguration(LiveTableauOptions.SectionName);
        services.AddSingleton<ILiveTableauHub, LiveTableauHub>();
        services.AddHostedService<LiveTableauSyncService>();
        return services;
    }
}
