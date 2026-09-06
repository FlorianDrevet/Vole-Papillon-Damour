using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

const string ApiResourceName = "api";
const string AzureBlobStorageConnectionStringEnvironmentName = "ConnectionStrings__AzureBlobStorageConnectionString";
const string BlobContainerActualityImagesEnvironmentName = "BlobSettings__ContainerActualityImagesName";
const string BlobContainerEventImagesEnvironmentName = "BlobSettings__BlobContainerEventImagesClient";
const string BlobContainerLotoImagesEnvironmentName = "BlobSettings__ContainerName";
const string BlobContainerProductsImagesEnvironmentName = "BlobSettings__BlobContainerProductsImagesClient";
const string BackOfficeResourceName = "backoffice";
const string CatalogResourceName = "catalog";
const string DefaultHttpEndpointName = "http";
const string ProjectDatabaseName = "ProjectDatabase";
const string ScanResourceName = "scan";
const string SqlServerName = "sql-server";
const string SqlServerPasswordParameterName = "sql-server-password";
const string StorageName = "storage";
const string WebsiteResourceName = "website";
const int BackOfficePort = 4200;
const int CatalogPort = 4203;
const int ScanPort = 4202;
const int WebsitePort = 4201;

var builder = DistributedApplication.CreateBuilder(args);

var sqlServerPassword = builder.AddParameter(SqlServerPasswordParameterName, secret: true);

var projectDatabase = builder.AddSqlServer(SqlServerName, password: sqlServerPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase(ProjectDatabaseName, "vole-papillon-damour-db");

var storage = builder.AddAzureStorage(StorageName);
storage.RunAsEmulator(container => container.WithLifetime(ContainerLifetime.Persistent));
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.Vole_Papillon_Damour_Api>(ApiResourceName)
    .WithReference(projectDatabase)
    .WaitFor(projectDatabase)
    .WaitFor(storage)
    .WithEnvironment(
        AzureBlobStorageConnectionStringEnvironmentName,
        blobs.Resource.ConnectionStringExpression)
    .WithEnvironment(BlobContainerLotoImagesEnvironmentName, "images")
    .WithEnvironment(BlobContainerActualityImagesEnvironmentName, "actuality-images")
    .WithEnvironment(BlobContainerEventImagesEnvironmentName, "event-images")
    .WithEnvironment(BlobContainerProductsImagesEnvironmentName, "product-images")
    .WithExternalHttpEndpoints();

builder.AddAzureFunctionsProject<Projects.Vole_Papillon_Damour_Worker>("worker")
    .WithReference(projectDatabase)
    .WithEnvironment(
        AzureBlobStorageConnectionStringEnvironmentName,
        blobs.Resource.ConnectionStringExpression)
    .WithEnvironment(BlobContainerLotoImagesEnvironmentName, "images")
    .WithEnvironment(BlobContainerActualityImagesEnvironmentName, "actuality-images")
    .WithEnvironment(BlobContainerEventImagesEnvironmentName, "event-images")
    .WithEnvironment(BlobContainerProductsImagesEnvironmentName, "product-images")
    .WaitFor(projectDatabase)
    .WaitFor(storage);

builder.AddJavaScriptApp(ScanResourceName, GetFrontendDirectory("Scan"))
    .WithRunScript("start")
    .WithArgs("--", "--host", "0.0.0.0", "--port", ScanPort.ToString())
    .WithHttpEndpoint(targetPort: ScanPort, port: ScanPort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

builder.AddJavaScriptApp(BackOfficeResourceName, GetFrontendDirectory("BackOffice"))
    .WithRunScript("start")
    .WithArgs("--", "--host", "0.0.0.0", "--port", BackOfficePort.ToString())
    .WithHttpEndpoint(targetPort: BackOfficePort, port: BackOfficePort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

builder.AddJavaScriptApp(WebsiteResourceName, GetFrontendDirectory("Website"))
    .WithRunScript("start")
    .WithArgs("--", "--host", "0.0.0.0", "--port", WebsitePort.ToString())
    .WithHttpEndpoint(targetPort: WebsitePort, port: WebsitePort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

builder.AddJavaScriptApp(CatalogResourceName, GetFrontendDirectory("Catalog"))
    .WithRunScript("start")
    .WithArgs("--", "--host", "0.0.0.0", "--port", CatalogPort.ToString())
    .WithHttpEndpoint(targetPort: CatalogPort, port: CatalogPort, name: DefaultHttpEndpointName, isProxied: false)
    .WaitFor(api);

await builder.Build().RunAsync();

static string GetFrontendDirectory(string frontendDirectoryName)
{
    return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", frontendDirectoryName));
}
