# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies (API only for now)
COPY ["crawler.api/CrawlerWebApi/CrawlerWebApi.csproj", "CrawlerWebApi/"]

# For now, comment out the Playwright dependency to test basic containerization
# We'll add it back once we test the basic setup works
# COPY ["crawler.playwright/IC.Test.Playwright.Crawler/IC.Test.Playwright.Crawler.csproj", "crawler.playwright/IC.Test.Playwright.Crawler/"]

RUN dotnet restore "CrawlerWebApi/CrawlerWebApi.csproj"

# Copy everything else and build
COPY ["crawler.api/CrawlerWebApi/", "CrawlerWebApi/"]
# COPY ["crawler.playwright/IC.Test.Playwright.Crawler/", "crawler.playwright/IC.Test.Playwright.Crawler/"]

WORKDIR "/src/CrawlerWebApi"
RUN dotnet build "CrawlerWebApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "CrawlerWebApi.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Copy configuration files
COPY --from=build /src/CrawlerWebApi/config ./config

# Create directory for artifacts (if needed)
RUN mkdir -p /app/test-artifacts

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "CrawlerWebApi.dll"]