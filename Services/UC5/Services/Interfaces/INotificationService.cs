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


namespace ConnectHub.Notification.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> Send(
        int recipientId, int? senderId, string type,
        string title, string message, int? relatedId = null, string? relatedType = null);

    Task SendBulk(IList<int> recipientIds, string title, string message);

    Task<IList<NotificationDto>> GetByRecipient(int recipientId, int page = 1, int pageSize = 20);
    Task<IList<NotificationDto>> GetUnread(int recipientId);
    Task<int> GetUnreadCount(int recipientId);
    Task MarkAsRead(int notificationId);
    Task MarkAllRead(int recipientId);
    Task DeleteNotification(int notificationId);

    Task SendEmail(string toEmail, string subject, string body);

    Task<IList<NotificationDto>> GetAll(int page = 1, int pageSize = 20);
}




