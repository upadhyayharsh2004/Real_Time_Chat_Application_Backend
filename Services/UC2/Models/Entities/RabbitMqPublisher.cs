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
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace ConnectHub.Message.Models.Entities;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly object _lock = new();

    public const string QueueMessageSent = "connecthub.message.sent";
    public const string QueueRoomMessageSent = "connecthub.room.message.sent";
    public const string QueueMessageRead = "connecthub.message.read";
    public const string QueueMessageDeleted = "connecthub.message.deleted";
    public const string QueueMessageEdited = "connecthub.message.edited";

    private static readonly string[] AllQueues =
    {
        QueueMessageSent, QueueRoomMessageSent,
        QueueMessageRead, QueueMessageDeleted, QueueMessageEdited
    };

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

        TryConnect();
    }

    private bool TryConnect()
    {
        lock (_lock)
        {
            if (_channel?.IsOpen == true) return true;

            try
            {
                _channel?.Dispose();
                _connection?.Dispose();

                _connection = _factory.CreateConnection();
                _channel = _connection.CreateModel();

                foreach (var queue in AllQueues)
                    _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);

                _logger.LogInformation("[RabbitMQ] Publisher connected to {Host}", _factory.HostName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[RabbitMQ] Could not connect to {Host}: {Message}. Messages will be skipped until reconnect.",
                    _factory.HostName, ex.Message);
                _channel = null;
                _connection = null;
                return false;
            }
        }
    }

    public Task PublishMessageSentAsync(MessageSentEvent @event) =>
        Publish(QueueMessageSent, @event);

    public Task PublishRoomMessageSentAsync(RoomMessageSentEvent @event) =>
        Publish(QueueRoomMessageSent, @event);

    public Task PublishMessageReadAsync(MessageReadEvent @event) =>
        Publish(QueueMessageRead, @event);

    public Task PublishMessageDeletedAsync(MessageDeletedEvent @event) =>
        Publish(QueueMessageDeleted, @event);

    public Task PublishMessageEditedAsync(MessageEditedEvent @event) =>
        Publish(QueueMessageEdited, @event);

    private Task Publish(string queue, object @event)
    {
        if (!TryConnect())
        {
            _logger.LogWarning("[RabbitMQ] Skipping publish of {EventType} — no connection available.", @event.GetType().Name);
            return Task.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
            lock (_lock) { _channel = null; }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
    }
}



