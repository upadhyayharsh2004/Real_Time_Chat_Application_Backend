using ConnectHub.Auth.Controllers;
using ConnectHub.Auth.Data;
using ConnectHub.Auth.Middleware;
using ConnectHub.Auth.Models.DTOs;
using ConnectHub.Auth.Models.Entities;
using ConnectHub.Auth.Models.Events;
using ConnectHub.Auth.Repositories.Implementations;
using ConnectHub.Auth.Repositories.Interfaces;
using ConnectHub.Auth.Services.Implementations;
using ConnectHub.Auth.Services.Interfaces;
namespace ConnectHub.Auth.Models.Events;
public class UserRegisteredEvent
{
    public string EventType { get; set; } = "UserRegistered";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime RegisteredAt { get; set; }
}
public class UserDeactivatedEvent
{
    public string EventType { get; set; } = "UserDeactivated";
    public int UserId { get; set; }
    public DateTime DeactivatedAt { get; set; }
}
public class UserReactivatedEvent
{
    public string EventType { get; set; } = "UserReactivated";
    public int UserId { get; set; }
    public DateTime ReactivatedAt { get; set; }
}
public class UserProfileUpdatedEvent
{
    public string EventType { get; set; } = "UserProfileUpdated";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public class UserPasswordChangedEvent
{
    public string EventType { get; set; } = "UserPasswordChanged";
    public int UserId { get; set; }
    public DateTime ChangedAt { get; set; }
}
public class UserRoleChangedEvent
{
    public string EventType { get; set; } = "UserRoleChanged";
    public int UserId { get; set; }
    public string NewRole { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
public class UserOnlineEvent
{
    public string EventType { get; set; } = "UserOnline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}
public class UserOfflineEvent
{
    public string EventType { get; set; } = "UserOffline";
    public int UserId { get; set; }
    public DateTime LastSeen { get; set; }
}



