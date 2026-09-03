FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["Vole_Papillon_Damour.Worker/Vole_Papillon_Damour.Worker.csproj", "Vole_Papillon_Damour.Worker/"]
COPY ["Vole_Papillon_Damour.Application/Vole_Papillon_Damour.Application.csproj", "Vole_Papillon_Damour.Application/"]
COPY ["Vole_Papillon_Damour.Domain/Vole_Papillon_Damour.Domain.csproj", "Vole_Papillon_Damour.Domain/"]
COPY ["Vole_Papillon_Damour.Contracts/Vole_Papillon_Damour.Contracts.csproj", "Vole_Papillon_Damour.Contracts/"]
COPY ["Vole_Papillon_Damour.Infrastructure/Vole_Papillon_Damour.Infrastructure.csproj", "Vole_Papillon_Damour.Infrastructure/"]
RUN dotnet restore "Vole_Papillon_Damour.Worker/Vole_Papillon_Damour.Worker.csproj"

COPY . .
WORKDIR "/src/Vole_Papillon_Damour.Worker"
RUN dotnet publish "Vole_Papillon_Damour.Worker.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=build /app/publish .

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
