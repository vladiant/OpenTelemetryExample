FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj", "DotnetTelemetryPlayground.ApiServiceAtFront/"]
COPY ["DotnetTelemetryPlayground.ServiceDefaults/DotnetTelemetryPlayground.ServiceDefaults.csproj", "DotnetTelemetryPlayground.ServiceDefaults/"]
COPY ["DotnetTelemetryPlayground.Shared/DotnetTelemetryPlayground.Shared.csproj", "DotnetTelemetryPlayground.Shared/"]
RUN dotnet restore "DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj"
COPY . .
WORKDIR "/src/DotnetTelemetryPlayground.ApiServiceAtFront"
RUN dotnet build "DotnetTelemetryPlayground.ApiServiceAtFront.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DotnetTelemetryPlayground.ApiServiceAtFront.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DotnetTelemetryPlayground.ApiServiceAtFront.dll"]
