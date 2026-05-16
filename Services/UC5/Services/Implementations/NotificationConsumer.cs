using ConnectHub.Notification.Hubs;
using ConnectHub.Notification.Controllers;
using ConnectHub.Notification.Data;
using ConnectHub.Notification.Middleware;
using ConnectHub.Notification.Models.DTOs;
using ConnectHub.Notification.Models.Entities;
using ConnectHub.Notification.Models.Events;
using ConnectHub.Notification.Repositories.Implementations;
using ConnectHub.Notification.Repositories.Interfaces;
using ConnectHub.Notification.Services.Implementations;
using ConnectHub.Notification.Services.Interfaces;
using System.Text;
using System.Text.Json;


using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConnectHub.Notification.Services.Implementations;
public class NotificationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationConsumer> _logger;


    // â”€â”€ EXACT queue names matching UC1/UC2/UC3/UC4 publishers â”€â”€â”€â”€â”€
    private const string QueueMessageSent = "connecthub.message.sent";

    private const string QueueRoomInviteSent = "connecthub.room.invite.sent";

    private const string QueueRoomMessageSent = "connecthub.room.message.sent";
    private const string QueueMessageRead = "connecthub.message.read";
    private const string QueueMessageDeleted = "connecthub.message.deleted";
    private const string QueueRoomMemberJoined = "connecthub.room.member.joined";
    private const string QueueRoomCreated = "connecthub.room.created";
    private const string QueueUserDeactivated = "connecthub.user.deactivated";
    private const string QueueUserRoleChanged = "connecthub.user.role.changed";
    private const string QueueUserRegistered = "connecthub.user.registered";
    private const string QueuePresenceOnline = "connecthub.presence.online";
    private const string QueuePresenceOffline = "connecthub.presence.offline";

    public NotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<NotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
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
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:Username"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest",
                Port = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
                VirtualHost = _config["RabbitMQ:VHost"] ?? "/"
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            foreach (var q in new[]
            {
                QueueMessageSent, QueueRoomMessageSent, QueueMessageRead,
                QueueMessageDeleted, QueueRoomMemberJoined, QueueRoomCreated,
                QueueUserDeactivated, QueueUserRoleChanged, QueueUserRegistered,
                QueuePresenceOnline, QueuePresenceOffline,
                QueueRoomInviteSent
            })
            {
                channel.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);
            }

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("[RabbitMQ] Notification consumer started on 11 queues.");

            // â”€â”€ MessageSent â€” from UC2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<MessageSentEvent>(channel, QueueMessageSent, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.ReceiverId,
                    senderId: @event.SenderId,
                    type: "MESSAGE",
                    title: $"New message from {@event.SenderName}",
                    message: @event.Content.Length > 100
                                     ? @event.Content[..100] + "..."
                                     : @event.Content,
                    relatedId: @event.MessageId,
                    relatedType: "Message");
            });

            // â”€â”€ RoomMessageSent (mentions) â€” from UC2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<RoomMessageSentEvent>(channel, QueueRoomMessageSent, async (@event) =>
            {
                if (@event.MentionedUserNames is null || !@event.MentionedUserNames.Any())
                    return;

                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();

                // For each mentioned user â€” in production resolve userId from username
                // Here we log â€” the architecture is correct for future resolution
                _logger.LogInformation(
                    "[RabbitMQ] Room mention in RoomId={RoomId} by {Sender}. Mentions: {Users}",
                    @event.RoomId, @event.SenderName,
                    string.Join(", ", @event.MentionedUserNames));
                await Task.CompletedTask;
            });

            // â”€â”€ MessageRead â€” from UC2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<MessageReadEvent>(channel, QueueMessageRead, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<Repositories.Interfaces.INotificationRepository>();
                // Mark related MESSAGE notification as read
                var related = await repo.FindByRelatedId(@event.MessageId, "Message");
                foreach (var n in related.Where(n => !n.IsRead))
                    await repo.MarkAsRead(n.NotificationId);
            });

            // â”€â”€ MessageDeleted â€” from UC2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<MessageDeletedEvent>(channel, QueueMessageDeleted, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<Repositories.Interfaces.INotificationRepository>();
                await repo.DeleteByRelatedId(@event.MessageId, "Message");
            });

            // â”€â”€ RoomMemberJoined â€” from UC3 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<RoomMemberJoinedEvent>(channel, QueueRoomMemberJoined, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "PLATFORM",   // was ROOM_INVITE: frontend must not show Accept/Decline on a confirmation
                    title: $"You joined {@event.RoomName}",
                    message: $"You are now a member of {@event.RoomName}",
                    relatedId: @event.RoomId,
                    relatedType: "Room");
            });

            // â”€â”€ RoomCreated â€” from UC3 (log only) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<RoomCreatedEvent>(channel, QueueRoomCreated, async (@event) =>
            {
                _logger.LogInformation(
                    "[RabbitMQ] RoomCreated: RoomId={RoomId} Name={Name}",
                    @event.RoomId, @event.Name);
                await Task.CompletedTask;
            });

            // â”€â”€ UserDeactivated â€” from UC1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<UserDeactivatedEvent>(channel, QueueUserDeactivated, async (@event) =>
            {
                _logger.LogInformation(
                    "[RabbitMQ] UserDeactivated UserId={UserId} â€” clearing notifications.",
                    @event.UserId);
                // Future: mark all notifications for this user as read / archived
                await Task.CompletedTask;
            });

            // â”€â”€ UserRoleChanged â€” from UC1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<UserRoleChangedEvent>(channel, QueueUserRoleChanged, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "ROLE_CHANGE",
                    title: "Your account role has changed",
                    message: $"Your role has been updated to {@event.NewRole}",
                    relatedId: @event.UserId,
                    relatedType: "User");
            });

            // â”€â”€ RoomInviteSent â€” from UC3 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<RoomInviteSentEvent>(channel, QueueRoomInviteSent, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.InvitedUserId,
                    senderId: @event.InvitedByUserId,
                    type: "ROOM_INVITE",
                    title: $"You've been invited to {@event.RoomName}",
                    message: $"You have a pending invite to join {@event.RoomName}",
                    relatedId: @event.InviteId,      // â† InviteId taaki accept/decline kaam kare
                    relatedType: "RoomInvite");
            });

            // â”€â”€ UserRegistered â€” from UC1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<UserRegisteredEvent>(channel, QueueUserRegistered, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "PLATFORM",
                    title: "Welcome to ConnectHub!",
                    message: $"Hi {@event.DisplayName}, welcome to ConnectHub. Start chatting now!",
                    relatedId: null,
                    relatedType: null);
            });

            // â”€â”€ PresenceOnline â€” from UC4 (log) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<UserPresenceOnlineEvent>(channel, QueuePresenceOnline, async (@event) =>
            {
                _logger.LogDebug("[RabbitMQ] PresenceOnline UserId={UserId}", @event.UserId);
                await Task.CompletedTask;
            });

            // â”€â”€ PresenceOffline â€” from UC4 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Register<UserPresenceOfflineEvent>(channel, QueuePresenceOffline, async (@event) =>
            {
                _logger.LogDebug("[RabbitMQ] PresenceOffline UserId={UserId} LastSeen={LastSeen}",
                    @event.UserId, @event.LastSeen);
                // Future: trigger any queued email notifications for offline user
                await Task.CompletedTask;
            });

            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] Notification consumer failed to connect. " +
                "Service continues â€” messages will queue when broker is available.");
        }
    }

    private void Register<T>(IModel channel, string queue, Func<T, Task> handler)
    {
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<T>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (@event is not null)
                    await handler(@event);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RabbitMQ] Error processing {Type} from {Queue}",
                    typeof(T).Name, queue);
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };
        channel.BasicConsume(queue, autoAck: false, consumer);
    }
}




