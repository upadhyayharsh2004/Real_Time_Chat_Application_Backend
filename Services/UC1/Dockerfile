# ── Stage 1: Build ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ConnectHub.Auth.API.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish ConnectHub.Auth.API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ConnectHub.Auth.API.dll"]
