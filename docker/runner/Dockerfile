# The optional agent runner (ADR-0010). Built like the backend image: publish
# on the full SDK, run on the Alpine .NET runtime. The runner is a console
# worker, not a web app, so it needs only the base runtime, not ASP.NET Core.

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /source

COPY backend/Directory.Packages.props ./
COPY backend/src/Weaver.Runner/Weaver.Runner.csproj src/Weaver.Runner/
RUN dotnet restore src/Weaver.Runner/Weaver.Runner.csproj

COPY backend/src/Weaver.Runner src/Weaver.Runner
RUN dotnet publish src/Weaver.Runner/Weaver.Runner.csproj \
    --configuration Release \
    --output /app \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

# ConfigFile points at a directory meant to be mounted, so a runner.json can
# be supplied without shadowing the app's own files in /app.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    ConfigFile=/config/runner.json

# A task's TimeZone (or TZ) is an IANA id looked up in /usr/share/zoneinfo;
# with invariant globalization there's no ICU fallback, so without tzdata
# every zone but UTC would be rejected.
RUN apk add --no-cache tzdata

COPY --from=build /app .

USER $APP_UID

# No arguments: run every configured task on its cron schedule.
# `--run when-resolution`: run that task once and exit.
ENTRYPOINT ["dotnet", "Weaver.Runner.dll"]
