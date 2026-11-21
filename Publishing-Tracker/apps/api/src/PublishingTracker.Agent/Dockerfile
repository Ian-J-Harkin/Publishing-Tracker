# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy props and solution definition files
COPY Directory.Build.props Directory.Packages.props ./

# Copy project files for caching restore layer
COPY src/PublishingTracker.Domain/PublishingTracker.Domain.csproj src/PublishingTracker.Domain/
COPY src/PublishingTracker.Application/PublishingTracker.Application.csproj src/PublishingTracker.Application/
COPY src/PublishingTracker.Infrastructure/PublishingTracker.Infrastructure.csproj src/PublishingTracker.Infrastructure/
COPY src/PublishingTracker.Mcp/PublishingTracker.Mcp.csproj src/PublishingTracker.Mcp/
COPY src/PublishingTracker.Rag/PublishingTracker.Rag.csproj src/PublishingTracker.Rag/
COPY src/PublishingTracker.Agent/PublishingTracker.Agent.csproj src/PublishingTracker.Agent/
COPY tools/PublishingTracker.HealthProbe/PublishingTracker.HealthProbe.csproj tools/PublishingTracker.HealthProbe/

RUN dotnet restore src/PublishingTracker.Agent/PublishingTracker.Agent.csproj
RUN dotnet restore tools/PublishingTracker.HealthProbe/PublishingTracker.HealthProbe.csproj

# Copy source tree and RAG corpus
COPY src/ src/
COPY tools/ tools/
COPY docs/rag-corpus/ docs/rag-corpus/

# Build standalone HealthProbe
RUN dotnet publish tools/PublishingTracker.HealthProbe/PublishingTracker.HealthProbe.csproj -c Release -o /app/healthcheck-bin --no-restore

# Build and publish Agent
RUN dotnet publish src/PublishingTracker.Agent/PublishingTracker.Agent.csproj -c Release -o /app/publish --no-restore

# Runtime Stage: Hardened, minimal Chiseled Ubuntu (.NET 10)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

# Run as non-root user app (UID 1654)
USER 1654:1654

COPY --from=build --chown=1654:1654 /app/publish .
COPY --from=build --chown=1654:1654 /app/healthcheck-bin/PublishingTracker.HealthProbe ./healthcheck
COPY --from=build --chown=1654:1654 /src/docs/rag-corpus ./docs/rag-corpus

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

ENTRYPOINT ["dotnet", "PublishingTracker.Agent.dll"]
