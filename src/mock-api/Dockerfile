# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files for restore layer
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .
COPY WF-Garage.sln .
COPY src/mock-api/mock-api.csproj src/mock-api/
COPY src/shared/Observability/Observability.csproj src/shared/Observability/

# Restore dependencies
RUN dotnet restore src/mock-api/mock-api.csproj

# Copy source and build
COPY src/mock-api/ src/mock-api/
COPY src/shared/Observability/ src/shared/Observability/
RUN dotnet publish src/mock-api/mock-api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

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

ENTRYPOINT ["dotnet", "mock-api.dll"]
