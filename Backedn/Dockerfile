# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: restore against the project files first so the layer cache
# survives ordinary source edits.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Ustaz.sln ./
COPY src/Ustaz.Domain/Ustaz.Domain.csproj                     src/Ustaz.Domain/
COPY src/Ustaz.Application/Ustaz.Application.csproj           src/Ustaz.Application/
COPY src/Ustaz.Infrastructure/Ustaz.Infrastructure.csproj     src/Ustaz.Infrastructure/
COPY src/Ustaz.Persistence/Ustaz.Persistence.csproj           src/Ustaz.Persistence/
COPY src/Ustaz.API/Ustaz.API.csproj                           src/Ustaz.API/
COPY tests/Ustaz.Application.Tests/Ustaz.Application.Tests.csproj tests/Ustaz.Application.Tests/
COPY tests/Ustaz.API.Tests/Ustaz.API.Tests.csproj             tests/Ustaz.API.Tests/

RUN dotnet restore src/Ustaz.API/Ustaz.API.csproj

COPY . .

RUN dotnet publish src/Ustaz.API/Ustaz.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is only here so the container can answer HEALTHCHECK: the aspnet image ships
# without an HTTP client, and a health probe that cannot call the app is worthless.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Private uploads live here. Created before the user switch so the non-root runtime
# account owns it, and mounted as a volume so files survive a redeploy.
RUN mkdir -p /app/storage /app/logs && chown -R $APP_UID:$APP_UID /app/storage /app/logs

# Run as the non-root user that ships with the base image.
USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    Storage__RootPath=/app/storage

COPY --from=build /app/publish .

EXPOSE 8080

VOLUME ["/app/storage"]

# Liveness only: readiness depends on the database, which is the orchestrator's
# business, not a reason to restart an otherwise healthy process.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Ustaz.API.dll"]
