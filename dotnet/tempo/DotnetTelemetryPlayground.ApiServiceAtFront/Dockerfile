# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy solution and project files
COPY ["dotnet-telemetry-playground.slnx", "."]
COPY ["DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj", "DotnetTelemetryPlayground.ApiServiceAtFront/"]
COPY ["DotnetTelemetryPlayground.ServiceDefaults/DotnetTelemetryPlayground.ServiceDefaults.csproj", "DotnetTelemetryPlayground.ServiceDefaults/"]
COPY ["DotnetTelemetryPlayground.Shared/DotnetTelemetryPlayground.Shared.csproj", "DotnetTelemetryPlayground.Shared/"]
COPY ["Custom.Client/Custom.Client.csproj", "Custom.Client/"]

# Restore dependencies
RUN dotnet restore "DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj"

# Copy source code
COPY ["DotnetTelemetryPlayground.ApiServiceAtFront/", "DotnetTelemetryPlayground.ApiServiceAtFront/"]
COPY ["DotnetTelemetryPlayground.ServiceDefaults/", "DotnetTelemetryPlayground.ServiceDefaults/"]
COPY ["DotnetTelemetryPlayground.Shared/", "DotnetTelemetryPlayground.Shared/"]
COPY ["Custom.Client/", "Custom.Client/"]

# Build application
RUN dotnet build "DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DotnetTelemetryPlayground.ApiServiceAtFront/DotnetTelemetryPlayground.ApiServiceAtFront.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DotnetTelemetryPlayground.ApiServiceAtFront.dll"]
