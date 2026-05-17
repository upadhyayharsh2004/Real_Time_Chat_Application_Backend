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
using System.Text;
using System.Text.Json;


using RabbitMQ.Client;

namespace ConnectHub.Presence.Models.Entities;

/// <summary>
/// RabbitMqPublisher — publishes Presence domain events to RabbitMQ.
/// Exact same pattern as UC1/UC2/UC3:
///   - Reads config from IConfiguration (not hardcoded)
///   - All queues declared durable
///   - Messages Persistent = true
///   - Registered as AddSingleton
///   - Fire-and-forget: publish failures logged but never block operations
///
/// Queues published:
///   connecthub.presence.online
///   connecthub.presence.offline
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public const string QueuePresenceOnline  = "connecthub.presence.online";
    public const string QueuePresenceOffline = "connecthub.presence.offline";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        _factory = new ConnectionFactory
        {
            HostName    = config["RabbitMQ:Host"]     ?? "localhost",
            UserName    = config["RabbitMQ:Username"] ?? "guest",
            Password    = config["RabbitMQ:Password"] ?? "guest",
            Port        = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"]    ?? "/"
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
            _channel    = _connection.CreateModel();

            foreach (var queue in new[] { QueuePresenceOnline, QueuePresenceOffline })
                _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);

            _logger.LogInformation("[RabbitMQ] Presence publisher connected to {Host}", _factory.HostName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Publisher failed to connect to {Host}.", _factory.HostName);
        }
    }

    public Task PublishUserPresenceOnlineAsync(UserPresenceOnlineEvent @event) =>
        Publish(QueuePresenceOnline, @event);

    public Task PublishUserPresenceOfflineAsync(UserPresenceOfflineEvent @event) =>
        Publish(QueuePresenceOffline, @event);

    private Task Publish(string queue, object @event)
    {
        try
        {
            if (_channel == null || _channel.IsClosed)
            {
                Connect();
            }

            if (_channel == null || _channel.IsClosed)
            {
                _logger.LogWarning("[RabbitMQ] Channel is null or closed. Cannot publish {EventType} to {Queue}", @event.GetType().Name, queue);
                return Task.CompletedTask;
            }

            var body  = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            var props = _channel.CreateBasicProperties();
            props.Persistent  = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange:        "",
                routingKey:      queue,
                basicProperties: props,
                body:            body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { _channel?.Close(); }    catch { }
        try { _connection?.Close(); } catch { }
        _disposed = true;
    }
}




