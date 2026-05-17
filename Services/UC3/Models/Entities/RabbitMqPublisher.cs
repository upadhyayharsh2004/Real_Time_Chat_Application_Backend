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
using System.Text;
using System.Text.Json;


using RabbitMQ.Client;

namespace ConnectHub.ChatRoom.Models.Entities;
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public const string QueueRoomCreated = "connecthub.room.created";
    public const string QueueRoomDeleted = "connecthub.room.deleted";
    public const string QueueRoomMemberJoined = "connecthub.room.member.joined";

    public const string QueueRoomInviteSent = "connecthub.room.invite.sent";
    public const string QueueRoomMemberLeft = "connecthub.room.member.left";
    public const string QueueRoomUpdated = "connecthub.room.updated";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"] ?? "/"
        };

        Connect();
    }

    private void Connect()
    {
        try
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                return;

            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare all queues as durable (survive broker restart)
            foreach (var queue in new[]
            {
                QueueRoomCreated, QueueRoomDeleted,
                QueueRoomMemberJoined, QueueRoomMemberLeft,
                QueueRoomUpdated, QueueRoomInviteSent
            })
            {
                _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            }

            _logger.LogInformation("[RabbitMQ] ChatRoom publisher connected to {Host}", _factory.HostName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Publisher failed to connect to {Host}.", _factory.HostName);
        }
    }

    public Task PublishRoomCreatedAsync(RoomCreatedEvent @event) =>
        Publish(QueueRoomCreated, @event);

    public Task PublishRoomInviteSentAsync(RoomInviteSentEvent @event) =>
        Publish(QueueRoomInviteSent, @event);

    public Task PublishRoomDeletedAsync(RoomDeletedEvent @event) =>
        Publish(QueueRoomDeleted, @event);

    public Task PublishRoomMemberJoinedAsync(RoomMemberJoinedEvent @event) =>
        Publish(QueueRoomMemberJoined, @event);

    public Task PublishRoomMemberLeftAsync(RoomMemberLeftEvent @event) =>
        Publish(QueueRoomMemberLeft, @event);

    public Task PublishRoomUpdatedAsync(RoomUpdatedEvent @event) =>
        Publish(QueueRoomUpdated, @event);

    private Task Publish(string queue, object @event)
    {
        try
        {
            if (_channel == null || _channel.IsClosed)
            {
                Connect(); // Try to reconnect
            }

            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogWarning("[RabbitMQ] Channel is null or closed. Cannot publish {EventType} to {Queue}", @event.GetType().Name, queue);
                return Task.CompletedTask;
            }

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            // Non-blocking — DB save already succeeded
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        _disposed = true;
    }
}




