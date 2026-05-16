# ConnectHub API Gateway

A **YARP-based** reverse proxy gateway for the ConnectHub real-time chat application.
It sits in front of all 6 microservices and provides a single entry point on **port 5255**.

---

## Service Map

| Service       | Internal Port | Gateway Path Prefix           |
|---------------|---------------|-------------------------------|
| Auth          | 5000          | `/api/users/**`               |
| Message       | 5001          | `/api/messages/**`            |
| Message Admin | 5001          | `/api/admin/messages/**`      |
| Message Hub   | 5001          | `/hubs/message/**` (SignalR)  |
| ChatRoom      | 5043          | `/api/rooms/**`               |
| ChatRoom Admin| 5043          | `/api/admin/rooms/**`         |
| Presence      | 5007          | `/api/presence/**`            |
| Presence Hub  | 5007          | `/hubs/presence/**` (SignalR) |
| Notification  | 5009          | `/api/notifications/**`       |
| Notification Hub | 5009       | `/hubs/notification/**`       |
| Media         | 5008          | `/api/media/**`               |

---

## Quick Start (Local Development)

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or SQLEXPRESS)
- RabbitMQ running on `localhost:5672`
- All 6 services running on their respective ports

### Run the Gateway
```bash
cd ConnectHub.Gateway
dotnet restore
dotnet run
```

Gateway will start on `http://localhost:5255`.

Smoke test:
```
GET http://localhost:5255/gateway/status   → lists all services
GET http://localhost:5255/health           → gateway health
```

---

## Docker Deployment (Recommended)

The included `docker-compose.yml` spins up everything:

- SQL Server 2022 (port 1433)
- RabbitMQ with Management UI (ports 5672, 15672)
- Azurite (Azure Blob emulator, ports 10000–10002)
- All 6 microservices
- API Gateway (port 5255)

### Step 1 — Add Dockerfiles to each service

Each service needs a `Dockerfile` in its project root. Use this template
(replace `ConnectHub.Auth.API` with the actual assembly name):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE <PORT>
ENTRYPOINT ["dotnet", "ConnectHub.<ServiceName>.API.dll"]
```

Service → port mapping:

| Service            | Port |
|--------------------|------|
| Auth               | 5000 |
| Message            | 5001 |
| ChatRoom           | 5043 |
| Presence           | 5007 |
| Notification       | 5009 |
| Media              | 5008 |

### Step 2 — Place the Gateway folder

Put the `ConnectHub.Gateway` folder at:
```
Services/
  Gateway/
    ConnectHub.Gateway/   ← all gateway files go here
  UC1/
  UC2/
  ...
```

### Step 3 — Start everything

```bash
# From the folder containing docker-compose.yml
docker-compose up --build -d

# Watch logs
docker-compose logs -f gateway

# Stop everything
docker-compose down
```

### Step 4 — Verify

```
http://localhost:5255/gateway/status    → gateway status JSON
http://localhost:5255/health            → gateway health
http://localhost:15672                  → RabbitMQ Management UI (guest/guest)
```

---

## Environment Variables

Override any setting at runtime via environment variables using double-underscore notation:

```bash
# Change JWT secret
Jwt__Secret=MyNewSecret

# Change a downstream cluster address
ReverseProxy__Clusters__auth-cluster__Destinations__auth-primary__Address=http://my-auth-host:5000

# Set frontend URL for CORS
Cors__AllowedOrigins__0=https://myfrontend.com
```

---

## Features

- **JWT Authentication** — validates tokens at the gateway; services can trust forwarded identity
- **SignalR Support** — WebSocket upgrades pass through; JWT via query string (`?access_token=...`)
- **Rate Limiting** — 30 req/s general; 10 login attempts per 5 min; 5 registrations per hour
- **CORS** — configured once at the gateway for all services
- **Health Checks** — `/health` on gateway; active health checking of each downstream cluster
- **Correlation IDs** — `X-Correlation-Id` header injected/propagated on every request
- **Structured Logging** — Serilog to console + rolling file (`logs/gateway-YYYYMMDD.log`)
- **Status Endpoint** — `GET /gateway/status` for quick environment verification

---

## Production Checklist

- [ ] Change `JWT_SECRET` to a strong random value (min 32 chars)
- [ ] Change SQL Server `SA_PASSWORD`
- [ ] Replace `AzureBlob__ConnectionString` with a real Azure Storage connection string
- [ ] Set real `Cors__AllowedOrigins` to your frontend domain
- [ ] Enable HTTPS (add TLS certificate to Kestrel or put Nginx/Caddy in front)
- [ ] Enable email in UC5 Notification service (`Email__Enabled=true`)
- [ ] Set real Google OAuth credentials in UC1 Auth service
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` on all services
