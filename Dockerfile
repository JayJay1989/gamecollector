# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /source
COPY global.json Directory.Build.props GameCollector.slnx ./
COPY src/GameCollector.Api/GameCollector.Api.csproj src/GameCollector.Api/
COPY src/GameCollector.Application/GameCollector.Application.csproj src/GameCollector.Application/
COPY src/GameCollector.Contracts/GameCollector.Contracts.csproj src/GameCollector.Contracts/
COPY src/GameCollector.Domain/GameCollector.Domain.csproj src/GameCollector.Domain/
COPY src/GameCollector.Infrastructure/GameCollector.Infrastructure.csproj src/GameCollector.Infrastructure/
RUN dotnet restore src/GameCollector.Api/GameCollector.Api.csproj
COPY src/ src/
ARG APP_VERSION=0.0.0-local
RUN dotnet publish src/GameCollector.Api/GameCollector.Api.csproj -c Release --no-restore \
    -p:Version=${APP_VERSION} -p:ContinuousIntegrationBuild=true -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_VERSION=0.0.0-local
LABEL org.opencontainers.image.title="Game Collector API" \
      org.opencontainers.image.version="${APP_VERSION}"
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data \
    && chown "${APP_UID}:${APP_UID}" /data
WORKDIR /app
COPY --from=build --chown=${APP_UID}:${APP_UID} /publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__GameCollector="Data Source=/data/gamecollector.db;Foreign Keys=True;Default Timeout=5;Pooling=True"
VOLUME ["/data"]
EXPOSE 8080
USER ${APP_UID}
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "GameCollector.Api.dll"]
