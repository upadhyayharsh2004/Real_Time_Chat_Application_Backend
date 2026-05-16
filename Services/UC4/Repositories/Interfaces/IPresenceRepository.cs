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


namespace ConnectHub.Presence.Repositories.Interfaces;

/// <summary>
/// IPresenceRepository — DB persistence for Presence service.
/// All methods are async. Called fire-and-forget from PresenceService
/// (Singleton) via IServiceScopeFactory to create scoped DB access.
/// Primary source of truth is in-memory ConcurrentDictionary in PresenceService.
/// DB is used for LastSeen persistence across restarts.
/// </summary>
public interface IPresenceRepository
{
    Task<UserPresence?> GetByUserId(int userId);
    Task<UserPresence> GetOrCreateByUserId(int userId);
    Task SetUserOnline(int userId, int connectionCount);
    Task SetUserOffline(int userId, DateTime lastSeen);
    Task UpdateLastActiveAt(int userId, DateTime lastActiveAt);
    Task<IList<UserPresence>> GetPresenceForUsers(IList<int> userIds);

    Task AddConnection(UserConnection connection);
    Task RemoveConnection(string connectionId);
    Task RemoveAllConnectionsForUser(int userId);
    Task<IList<UserConnection>> GetConnectionsForUser(int userId);
}




