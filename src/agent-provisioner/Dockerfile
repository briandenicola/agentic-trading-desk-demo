# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files for restore layer
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .
COPY WF-Garage.sln .
COPY src/agent-provisioner/agent-provisioner.csproj src/agent-provisioner/
COPY src/shared/Observability/Observability.csproj src/shared/Observability/

# Restore dependencies
RUN dotnet restore src/agent-provisioner/agent-provisioner.csproj

# Copy source and build (includes linked morning-brief.md)
COPY src/agent-provisioner/ src/agent-provisioner/
COPY src/orchestration-api/Prompts/ src/orchestration-api/Prompts/
COPY src/shared/Observability/ src/shared/Observability/
RUN dotnet publish src/agent-provisioner/agent-provisioner.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

# Reuse the non-root user provided by the base image (uid from APP_UID env);
# adduser is idempotent here since the base already defines this uid.
ARG APP_UID=1000
RUN adduser -D -u $APP_UID appuser 2>/dev/null || true
USER $APP_UID

# Copy published output
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "agent-provisioner.dll"]
