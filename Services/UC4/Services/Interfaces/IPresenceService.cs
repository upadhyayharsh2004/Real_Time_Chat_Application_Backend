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



namespace ConnectHub.Presence.Services.Interfaces;


public interface IPresenceService
{
    // ── Class diagram sync methods (ConcurrentDictionary — no DB round-trip) ──
    void UserConnected(int userId, string connectionId, string? deviceInfo = null);
    void UserDisconnected(int userId, string connectionId);
    IList<string> GetConnectionsByUserId(int userId);
    IList<int> GetOnlineUserIds();
    bool IsUserOnline(int userId);
    int GetConnectionCount();
    IList<UserConnection> GetOnlineUsersInfo();
    void ClearUserConnections(int userId);

    // ── Async helpers (controller bulk queries, consumer LastActiveAt update) ──
    Task<IList<UserPresenceDto>> GetPresenceForUsers(IList<int> userIds);
    Task<IList<UserPresenceDto>> GetOnlineUsersDtos();
    Task UpdateLastActiveAt(int userId);
}




