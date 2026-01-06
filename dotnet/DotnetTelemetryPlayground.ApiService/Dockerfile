FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DotnetTelemetryPlayground.ApiService/DotnetTelemetryPlayground.ApiService.csproj", "DotnetTelemetryPlayground.ApiService/"]
COPY ["DotnetTelemetryPlayground.ServiceDefaults/DotnetTelemetryPlayground.ServiceDefaults.csproj", "DotnetTelemetryPlayground.ServiceDefaults/"]
COPY ["DotnetTelemetryPlayground.Shared/DotnetTelemetryPlayground.Shared.csproj", "DotnetTelemetryPlayground.Shared/"]
RUN dotnet restore "DotnetTelemetryPlayground.ApiService/DotnetTelemetryPlayground.ApiService.csproj"
COPY . .
WORKDIR "/src/DotnetTelemetryPlayground.ApiService"
RUN dotnet build "DotnetTelemetryPlayground.ApiService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DotnetTelemetryPlayground.ApiService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DotnetTelemetryPlayground.ApiService.dll"]
