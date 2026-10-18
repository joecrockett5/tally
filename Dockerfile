# syntax=docker/dockerfile:1

# ---- 1. Build the React PWA ---------------------------------------------
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-fund --no-audit
COPY web/ ./
RUN npm run build

# ---- 2. Publish the ASP.NET Core API -------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json ./
COPY src/Tally.Api/Tally.Api.csproj src/Tally.Api/
RUN dotnet restore src/Tally.Api/Tally.Api.csproj
COPY src/Tally.Api/ src/Tally.Api/
RUN dotnet publish src/Tally.Api/Tally.Api.csproj -c Release -o /out --no-restore -p:UseAppHost=false

# ---- 3. Runtime ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=api /out ./
COPY --from=web /web/dist ./wwwroot
COPY data/cofid ./data/cofid
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD ["dotnet", "Tally.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Tally.Api.dll"]
