using ConnectHub.Media.Models.Options;
using ConnectHub.Media.Controllers;
using ConnectHub.Media.Data;
using ConnectHub.Media.Middleware;
using ConnectHub.Media.Models.DTOs;
using ConnectHub.Media.Models.Entities;
using ConnectHub.Media.Models.Events;
using ConnectHub.Media.Repositories.Implementations;
using ConnectHub.Media.Repositories.Interfaces;
using ConnectHub.Media.Services.Implementations;
using ConnectHub.Media.Services.Interfaces;


namespace ConnectHub.Media.Models.Entities;

/// <summary>
/// IRabbitMqPublisher — typed event publishing for Media-Service.
/// Same pattern as UC1-UC5: reads config, durable queues, persistent messages.
/// Registered as AddSingleton.
/// Publishes AFTER DB save so FileId is always valid.
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishMediaUploadedAsync(MediaUploadedEvent @event);
    Task PublishMediaDeletedAsync(MediaDeletedEvent @event);
}




