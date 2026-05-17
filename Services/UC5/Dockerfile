FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConnectHub.Notification.API.csproj", "."]
RUN dotnet restore "ConnectHub.Notification.API.csproj"
COPY . .
RUN dotnet build "ConnectHub.Notification.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConnectHub.Notification.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ConnectHub.Notification.API.dll"]
