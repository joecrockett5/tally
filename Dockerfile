# syntax=docker/dockerfile:1

# Both build stages produce platform-independent output (JS bundles, portable
# .NET IL), so they run once on the build machine and are never emulated. Tally
# has no native dependencies, so no target-architecture install stage is needed.

# ---- 1. Build the React PWA ---------------------------------------------
FROM --platform=$BUILDPLATFORM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-fund --no-audit
COPY web/ ./
RUN npm run build

# ---- 2. Publish the ASP.NET Core API -------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json ./
COPY src/Tally.Api/Tally.Api.csproj src/Tally.Api/
RUN dotnet restore src/Tally.Api/Tally.Api.csproj
COPY src/Tally.Api/ src/Tally.Api/
COPY data/cofid/ data/cofid/
RUN dotnet publish src/Tally.Api/Tally.Api.csproj -c Release -o /out --no-restore -p:UseAppHost=false

# ---- 3. Runtime ----------------------------------------------------------
# This stage alone is built for each target architecture. Chiseled Ubuntu has no
# shell or package manager; "extra" adds the ICU and tzdata the app needs for
# timezones. There is no VOLUME: the app keeps nothing on disk, all data lives in
# Postgres (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
ARG APP_VERSION=dev
LABEL org.opencontainers.image.title="Tally" \
      org.opencontainers.image.description="Self-hosted food diary and nutrition tracker for the UK" \
      org.opencontainers.image.source="https://github.com/joecrockett5/tally" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${APP_VERSION}"
WORKDIR /app
ENV APP_VERSION=${APP_VERSION} \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=api /out ./
COPY --from=web /web/dist ./wwwroot
# Non-root: $APP_UID is defined by the .NET base image.
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD ["dotnet", "Tally.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Tally.Api.dll"]
