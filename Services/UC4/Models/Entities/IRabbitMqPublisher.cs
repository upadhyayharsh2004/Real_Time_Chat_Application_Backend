using ConnectHub.Presence.Hubs;
using ConnectHub.Presence.Controllers;
using ConnectHub.Presence.Data;
using ConnectHub.Presence.Middleware;
using ConnectHub.Presence.Models.DTOs;
using ConnectHub.Presence.Models.Entities;
using ConnectHub.Presence.Models.Events;
using ConnectHub.Presence.Repositories.Implementations;
using ConnectHub.Presence.Repositories.Interfaces;
using ConnectHub.Presence.Services.Implementations;
using ConnectHub.Presence.Services.Interfaces;


namespace ConnectHub.Presence.Models.Entities;

/// <summary>
/// IRabbitMqPublisher — typed event publishing for Presence-Service.
/// Same pattern as UC1/UC2/UC3: placed in Repositories/Interfaces.
/// Registered as AddSingleton — connection/channel reused across requests.
/// Publishes AFTER in-memory state is updated.
/// Fire-and-forget: publish failures logged but never block operations.
///
/// Queues published:
///   connecthub.presence.online   → Notification-Service
///   connecthub.presence.offline  → Notification-Service
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishUserPresenceOnlineAsync(UserPresenceOnlineEvent @event);
    Task PublishUserPresenceOfflineAsync(UserPresenceOfflineEvent @event);
}




