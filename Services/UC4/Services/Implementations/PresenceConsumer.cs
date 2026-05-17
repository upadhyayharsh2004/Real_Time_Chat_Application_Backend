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
using RabbitMQ.Client.Events;

namespace ConnectHub.Presence.Services.Implementations;

public class PresenceConsumer : BackgroundService
{
    private readonly IPresenceService _presenceService;
    private readonly IConfiguration   _config;
    private readonly ILogger<PresenceConsumer> _logger;

    // EXACT queue names from UC1 RabbitMqPublisher constants
    // UC1 publishes SEPARATE queues for UC2 (message.*) and UC3/UC4 (chatroom.*)
    private const string QueueUserOnline          = "connecthub.chatroom.user.online";   // UC1: QueueChatRoomUserOnline
    private const string QueueUserOffline         = "connecthub.chatroom.user.offline";  // UC1: QueueChatRoomUserOffline
    private const string QueueUserDeactivated     = "connecthub.user.deactivated";       // UC1: QueueUserDeactivated (shared)
    private const string QueueUserReactivated     = "connecthub.chatroom.user.reactivated"; // UC1: QueueChatRoomUserReactivated
    private const string QueueUserProfileUpdated  = "connecthub.user.profile.updated";  // UC1: QueueUserProfileUpdated (shared)

    // EXACT queue name from UC2 RabbitMqPublisher constants
    private const string QueueMessageSent         = "connecthub.message.sent";            // UC2: QueueMessageSent

    public PresenceConsumer(
        IPresenceService presenceService,
        IConfiguration config,
        ILogger<PresenceConsumer> logger)
    {
        _presenceService = presenceService;
        _config          = config;
        _logger          = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private void StartConsuming(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName    = _config["RabbitMQ:Host"]     ?? "localhost",
                UserName    = _config["RabbitMQ:Username"] ?? "guest",
                Password    = _config["RabbitMQ:Password"] ?? "guest",
                Port        = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
                VirtualHost = _config["RabbitMQ:VHost"]    ?? "/"
            };

            var connection = factory.CreateConnection();
            var channel    = connection.CreateModel();

            // Declare all queues we consume (durable — matches what UC1/UC2 declare)
            foreach (var q in new[]
            {
                QueueUserOnline, QueueUserOffline,
                QueueUserDeactivated, QueueUserReactivated,
                QueueUserProfileUpdated, QueueMessageSent
            })
            {
                channel.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);
            }

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation(
                "[RabbitMQ] Presence consumer listening on 6 queues from UC1 and UC2");

            // ── connecthub.user.online (UC1) ──────────────────────
            var userOnlineConsumer = new EventingBasicConsumer(channel);
            userOnlineConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<UserOnlineFromAuthEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] user.online: UserId={UserId}", @event.UserId);
                        // In-memory is managed by PresenceHub.OnConnectedAsync.
                        // Just update LastActiveAt in DB.
                        await _presenceService.UpdateLastActiveAt(@event.UserId);
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing user.online");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── connecthub.user.offline (UC1) ─────────────────────
            var userOfflineConsumer = new EventingBasicConsumer(channel);
            userOfflineConsumer.Received += (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<UserOfflineFromAuthEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] user.offline: UserId={UserId}", @event.UserId);
                        // Clear all in-memory connections (user logged out via REST)
                        _presenceService.ClearUserConnections(@event.UserId);
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing user.offline");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── connecthub.user.deactivated (UC1) ─────────────────
            var userDeactivatedConsumer = new EventingBasicConsumer(channel);
            userDeactivatedConsumer.Received += (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<UserDeactivatedEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] user.deactivated: UserId={UserId}", @event.UserId);
                        // Force offline — clears ConcurrentDictionary + async DB + publishes offline event
                        _presenceService.ClearUserConnections(@event.UserId);
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing user.deactivated");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── connecthub.user.reactivated (UC1) ─────────────────
            var userReactivatedConsumer = new EventingBasicConsumer(channel);
            userReactivatedConsumer.Received += (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<UserReactivatedEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] user.reactivated: UserId={UserId} — user can now reconnect",
                            @event.UserId);
                        // No action needed — user will connect via PresenceHub when they log back in
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing user.reactivated");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── connecthub.user.profile.updated (UC1) ─────────────
            var userProfileUpdatedConsumer = new EventingBasicConsumer(channel);
            userProfileUpdatedConsumer.Received += (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<UserProfileUpdatedEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] user.profile.updated: UserId={UserId}", @event.UserId);
                        // UC4 has no profile cache — log only
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing user.profile.updated");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── connecthub.message.sent (UC2) ─────────────────────
            var messageSentConsumer = new EventingBasicConsumer(channel);
            messageSentConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var @event = Deserialize<MessageSentEvent>(ea.Body.ToArray());
                    if (@event != null)
                    {
                        // Update LastActiveAt for both sender and receiver
                        await _presenceService.UpdateLastActiveAt(@event.SenderId);
                        await _presenceService.UpdateLastActiveAt(@event.ReceiverId);
                    }
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing message.sent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // Register all consumers
            channel.BasicConsume(QueueUserOnline,         autoAck: false, userOnlineConsumer);
            channel.BasicConsume(QueueUserOffline,        autoAck: false, userOfflineConsumer);
            channel.BasicConsume(QueueUserDeactivated,    autoAck: false, userDeactivatedConsumer);
            channel.BasicConsume(QueueUserReactivated,    autoAck: false, userReactivatedConsumer);
            channel.BasicConsume(QueueUserProfileUpdated, autoAck: false, userProfileUpdatedConsumer);
            channel.BasicConsume(QueueMessageSent,        autoAck: false, messageSentConsumer);

            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] Presence consumer failed to connect. " +
                "Service continues — messages will queue when broker is available.");
        }
    }

    private static T? Deserialize<T>(byte[] body) =>
        JsonSerializer.Deserialize<T>(
            Encoding.UTF8.GetString(body),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}




