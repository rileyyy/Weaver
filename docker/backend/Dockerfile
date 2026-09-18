# Small, self-hosted-friendly image: publish on the full SDK, run on the
# Alpine ASP.NET runtime so the shipped image doesn't carry the SDK/compiler
# toolchain. Built from the repo root so it can COPY across all three
# projects' csproj files before the full source, keeping `dotnet restore`
# cacheable as its own layer.

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /source

COPY backend/src/Weaver.Domain/Weaver.Domain.csproj src/Weaver.Domain/
COPY backend/src/Weaver.Infrastructure/Weaver.Infrastructure.csproj src/Weaver.Infrastructure/
COPY backend/src/Weaver.Api/Weaver.Api.csproj src/Weaver.Api/
RUN dotnet restore src/Weaver.Api/Weaver.Api.csproj

COPY backend/src src
RUN dotnet publish src/Weaver.Api/Weaver.Api.csproj \
    --configuration Release \
    --output /app \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# The app has no culture-specific formatting needs today (it's a JSON API;
# any locale-aware display belongs to the Flutter client), so invariant
# globalization avoids pulling in ICU data and keeps the image smaller.
# Revisit if the backend ever needs to do culture-aware formatting itself.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app .

# $APP_UID is defined by the base image; running as that non-root user is
# the current officially recommended pattern for .NET container images.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Weaver.Api.dll"]
