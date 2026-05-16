using ConnectHub.ChatRoom.Hubs;
using ConnectHub.ChatRoom.Controllers;
using ConnectHub.ChatRoom.Data;
using ConnectHub.ChatRoom.Middleware;
using ConnectHub.ChatRoom.Models.DTOs;
using ConnectHub.ChatRoom.Models.Entities;
using ConnectHub.ChatRoom.Models.Events;
using ConnectHub.ChatRoom.Repositories.Implementations;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using ConnectHub.ChatRoom.Services.Implementations;
using ConnectHub.ChatRoom.Services.Interfaces;


namespace ConnectHub.ChatRoom.Models.Entities;

/// <summary>
/// IRabbitMqPublisher — typed event publishing for ChatRoom-Service.
/// Same pattern as UC2: placed in Repositories/Interfaces.
/// Publishes AFTER DB save so RoomId is always valid.
/// Fire-and-forget: publish failures are logged but never block DB operations.
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishRoomCreatedAsync(RoomCreatedEvent @event);
    
    Task PublishRoomInviteSentAsync(RoomInviteSentEvent @event);
    Task PublishRoomDeletedAsync(RoomDeletedEvent @event);
    Task PublishRoomMemberJoinedAsync(RoomMemberJoinedEvent @event);
    Task PublishRoomMemberLeftAsync(RoomMemberLeftEvent @event);
    Task PublishRoomUpdatedAsync(RoomUpdatedEvent @event);
}




