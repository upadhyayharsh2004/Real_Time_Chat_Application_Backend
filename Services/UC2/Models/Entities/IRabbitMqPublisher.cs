using ConnectHub.Message.Hubs;
using ConnectHub.Message.Controllers;
using ConnectHub.Message.Data;
using ConnectHub.Message.Middleware;
using ConnectHub.Message.Models.DTOs;
using ConnectHub.Message.Models.Entities;
using ConnectHub.Message.Models.Events;
using ConnectHub.Message.Repositories.Implementations;
using ConnectHub.Message.Repositories.Interfaces;
using ConnectHub.Message.Services.Implementations;
using ConnectHub.Message.Services.Interfaces;


namespace ConnectHub.Message.Models.Entities;
public interface IRabbitMqPublisher
{
    Task PublishMessageSentAsync(MessageSentEvent @event);
    Task PublishRoomMessageSentAsync(RoomMessageSentEvent @event);
    Task PublishMessageReadAsync(MessageReadEvent @event);
    Task PublishMessageDeletedAsync(MessageDeletedEvent @event);
    Task PublishMessageEditedAsync(MessageEditedEvent @event);
}




