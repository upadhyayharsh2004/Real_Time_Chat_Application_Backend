# ConnectHub — Real-Time Chat Application Backend

ConnectHub is a high-performance, scalable real-time chat application built using a microservices architecture with .NET Core, SignalR, RabbitMQ, and PostgreSQL.

## 🚀 Quick Start

### 1. Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (optional, for local development)

### 2. Setup Environment
Create a `.env` file in the root directory (already ignored by Git) and add your secrets:
```env
# Database Credentials
POSTGRES_PASSWORD=your_local_db_password
NEON_DB_PASSWORD=your_neon_db_password

# JWT Configuration
JWT_SECRET=your_super_secret_jwt_key

# Admin Configuration
ADMIN_PASSWORD=your_admin_password
```

### 3. Run with Docker Compose
```bash
docker-compose up --build
```

## 🏗️ Architecture

The system consists of several microservices, each responsible for a specific domain:

- **Auth Service (UC1)**: User registration, login, and JWT issuing.
- **Message Service (UC2)**: Handling real-time messaging and SignalR hubs.
- **ChatRoom Service (UC3)**: Managing group chats and private rooms.
- **Presence Service (UC4)**: Tracking user online/offline status.
- **Notification Service (UC5)**: Push and email notifications.
- **Media Service (UC6)**: Image and file uploads (integrated with Azure Blob/Azurite).
- **API Gateway (YARP)**: Unified entry point for all frontend requests.

## 🛠️ Tech Stack
- **Backend**: ASP.NET Core 8.0
- **Real-time**: SignalR
- **Messaging**: RabbitMQ
- **Database**: PostgreSQL (Neon.tech & Local)
- **Storage**: Azure Blob Storage (Azurite for local)
- **Gateway**: YARP (Yet Another Reverse Proxy)
- **Containerization**: Docker & Docker Compose

---
Developed with ❤️ by Harsh Upadhyay
