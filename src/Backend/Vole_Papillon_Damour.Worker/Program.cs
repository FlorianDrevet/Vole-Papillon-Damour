using Microsoft.Extensions.Hosting;
using Vole_Papillon_Damour.Application;
using Vole_Papillon_Damour.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplication()
            .AddInfrastructure(
                context.Configuration,
                runMigrations: false,
                registerAuthentication: false);
    })
    .Build();

await host.RunAsync();
