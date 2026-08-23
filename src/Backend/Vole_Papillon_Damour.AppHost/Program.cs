using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

const string ApiResourceName = "api";
const string AzureBlobStorageConnectionStringEnvironmentName = "ConnectionStrings__AzureBlobStorageConnectionString";
const string BackOfficeResourceName = "backoffice";
const string DefaultHttpEndpointName = "http";
const string ProjectDatabaseName = "ProjectDatabase";
const string SqlServerName = "sql-server";
const string SqlServerPasswordParameterName = "sql-server-password";
const string StorageName = "storage";
const string UseDevelopmentStorageConnectionString = "UseDevelopmentStorage=true";
const string WebsiteResourceName = "website";
const int BackOfficePort = 4200;
const int WebsitePort = 4201;

var builder = DistributedApplication.CreateBuilder(args);

var sqlServerPassword = builder.AddParameter(SqlServerPasswordParameterName, secret: true);

var projectDatabase = builder.AddSqlServer(SqlServerName, password: sqlServerPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase(ProjectDatabaseName, "vole-papillon-damour-db");

var storage = builder.AddAzureStorage(StorageName)
    .RunAsEmulator(container => container.WithLifetime(ContainerLifetime.Persistent));

var api = builder.AddProject<Projects.Vole_Papillon_Damour_Api>(ApiResourceName)
    .WithReference(projectDatabase)
    .WaitFor(projectDatabase)
    .WaitFor(storage)
    .WithEnvironment(AzureBlobStorageConnectionStringEnvironmentName, UseDevelopmentStorageConnectionString)
    .WithExternalHttpEndpoints();

builder.AddNpmApp(BackOfficeResourceName, GetFrontendDirectory("BackOffice"), "start", ["--host", "0.0.0.0", "--port", BackOfficePort.ToString()])
    .WithHttpEndpoint(targetPort: BackOfficePort, port: BackOfficePort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

builder.AddNpmApp(WebsiteResourceName, GetFrontendDirectory("Website"), "start", ["--host", "0.0.0.0", "--port", WebsitePort.ToString()])
    .WithHttpEndpoint(targetPort: WebsitePort, port: WebsitePort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

await builder.Build().RunAsync();

static string GetFrontendDirectory(string frontendDirectoryName)
{
    return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", frontendDirectoryName));
}