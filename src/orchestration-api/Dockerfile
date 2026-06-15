# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files for restore layer
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .
COPY AgenticTradersDesk.sln .
COPY src/orchestration-api/orchestration-api.csproj src/orchestration-api/
COPY src/shared/Observability/Observability.csproj src/shared/Observability/

# Restore dependencies
RUN dotnet restore src/orchestration-api/orchestration-api.csproj

# Copy source and build
COPY src/orchestration-api/ src/orchestration-api/
COPY src/shared/Observability/ src/shared/Observability/
RUN dotnet publish src/orchestration-api/orchestration-api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Build provenance baked into the image (see tasks/Taskfile.build.yml). BUILD_GIT_SHA is
# surfaced at runtime by the /version endpoint so a running container maps to a repo commit.
ARG GIT_SHA=unknown
ARG BUILD_TIME=unknown
ENV BUILD_GIT_SHA=$GIT_SHA \
    BUILD_TIME=$BUILD_TIME
LABEL org.opencontainers.image.revision=$GIT_SHA

# Reuse the non-root user provided by the base image (uid from APP_UID env);
# adduser is idempotent here since the base already defines this uid.
ARG APP_UID=1000
RUN adduser -D -u $APP_UID appuser 2>/dev/null || true
USER $APP_UID

# Copy published output
COPY --from=build /app/publish .

# Configure ASP.NET
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "orchestration-api.dll"]
