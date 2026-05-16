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
using System.Text;
using System.Text.Json;


using RabbitMQ.Client;

namespace ConnectHub.Auth.Models.Entities;
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    // ── Queue name constants — must match what UC2/UC3 consumers declare ──────
    public const string QueueUserRegistered = "connecthub.user.registered";
    public const string QueueUserDeactivated = "connecthub.user.deactivated";


    // public const string QueueUserReactivated = "connecthub.user.reactivated";

    public const string QueueMessageUserReactivated = "connecthub.message.user.reactivated";

    public const string QueueChatRoomUserReactivated = "connecthub.chatroom.user.reactivated";

    public const string QueueChatRoomUserDeactivated = "connecthub.chatroom.user.deactivated";
    public const string QueueUserProfileUpdated = "connecthub.user.profile.updated";
    public const string QueueUserPasswordChanged = "connecthub.user.password.changed";
    public const string QueueUserRoleChanged = "connecthub.user.role.changed";

    public const string QueueMessageUserOnline = "connecthub.message.user.online";

    public const string QueueChatRoomUserOnline = "connecthub.chatroom.user.online";

    public const string QueueMessageUserOffline = "connecthub.message.user.offline";

    public const string QueueChatRoomUserOffline = "connecthub.chatroom.user.offline";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"] ?? "/"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare ALL queues as durable so they survive a broker restart
        foreach (var queue in new[]
        {
            QueueUserRegistered,
            QueueUserDeactivated,
            QueueChatRoomUserDeactivated,
            QueueMessageUserReactivated,
            QueueChatRoomUserReactivated,
            QueueUserProfileUpdated,
            QueueUserPasswordChanged,
            QueueUserRoleChanged,
            QueueMessageUserOnline,
            QueueChatRoomUserOnline,
            QueueMessageUserOffline,
            QueueChatRoomUserOffline,
        })
        {
            _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
        }

        _logger.LogInformation("[RabbitMQ] Auth publisher connected to {Host}", factory.HostName);
    }

    // ── Typed publish methods ─────────────────────────────────────────────────

    public Task PublishUserRegisteredAsync(UserRegisteredEvent @event) =>
        Publish(QueueUserRegistered, @event);

    public async Task PublishUserDeactivatedAsync(UserDeactivatedEvent @event)
    {
        await Publish(QueueUserDeactivated, @event); // UC2
        await Publish(QueueChatRoomUserDeactivated, @event); // UC3
    }

    public async Task PublishUserReactivatedAsync(UserReactivatedEvent @event)
    {
        await Publish(QueueMessageUserReactivated, @event); // UC2
        await Publish(QueueChatRoomUserReactivated, @event); // UC3
    }

    public Task PublishUserProfileUpdatedAsync(UserProfileUpdatedEvent @event) =>
        Publish(QueueUserProfileUpdated, @event);

    public Task PublishUserPasswordChangedAsync(UserPasswordChangedEvent @event) =>
        Publish(QueueUserPasswordChanged, @event);

    public Task PublishUserRoleChangedAsync(UserRoleChangedEvent @event) =>
        Publish(QueueUserRoleChanged, @event);

    public async Task PublishUserOnlineAsync(UserOnlineEvent @event)
    {
        await Publish(QueueMessageUserOnline, @event); // UC2
        await Publish(QueueChatRoomUserOnline, @event); // UC3
    }

    public async Task PublishUserOfflineAsync(UserOfflineEvent @event)
    {
        await Publish(QueueMessageUserOffline, @event); // UC2
        await Publish(QueueChatRoomUserOffline, @event); // UC3
    }

    // ── Core publish helper ───────────────────────────────────────────────────

    private Task Publish(string queue, object @event)
    {
        try
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;              // survive broker restart
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: body);

            _logger.LogInformation(
                "[RabbitMQ] Published {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            // Non-blocking — DB save already succeeded; log and continue
            _logger.LogError(ex,
                "[RabbitMQ] Failed to publish {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
        }

        return Task.CompletedTask;
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        try { _channel?.Close(); } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
        _disposed = true;
    }
}



