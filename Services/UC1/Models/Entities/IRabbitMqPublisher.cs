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


namespace ConnectHub.Auth.Models.Entities;
public interface IRabbitMqPublisher
{
    Task PublishUserRegisteredAsync(UserRegisteredEvent @event);
    Task PublishUserDeactivatedAsync(UserDeactivatedEvent @event);
    Task PublishUserReactivatedAsync(UserReactivatedEvent @event);
    Task PublishUserProfileUpdatedAsync(UserProfileUpdatedEvent @event);
    Task PublishUserPasswordChangedAsync(UserPasswordChangedEvent @event);
    Task PublishUserRoleChangedAsync(UserRoleChangedEvent @event);
    Task PublishUserOnlineAsync(UserOnlineEvent @event);
    Task PublishUserOfflineAsync(UserOfflineEvent @event);
}



