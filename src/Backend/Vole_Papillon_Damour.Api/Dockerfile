FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Vole_Papillon_Damour.Api/Vole_Papillon_Damour.Api.csproj", "Vole_Papillon_Damour.Api/"]
COPY ["Vole_Papillon_Damour.Application/Vole_Papillon_Damour.Application.csproj", "Vole_Papillon_Damour.Application/"]
COPY ["Vole_Papillon_Damour.Domain/Vole_Papillon_Damour.Domain.csproj", "Vole_Papillon_Damour.Domain/"]
COPY ["Vole_Papillon_Damour.Contracts/Vole_Papillon_Damour.Contracts.csproj", "Vole_Papillon_Damour.Contracts/"]
COPY ["Vole_Papillon_Damour.Infrastructure/Vole_Papillon_Damour.Infrastructure.csproj", "Vole_Papillon_Damour.Infrastructure/"]
RUN dotnet restore "Vole_Papillon_Damour.Api/Vole_Papillon_Damour.Api.csproj"
COPY . .
WORKDIR "/src/Vole_Papillon_Damour.Api"
RUN dotnet build "Vole_Papillon_Damour.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Vole_Papillon_Damour.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Vole_Papillon_Damour.Api.dll"]
