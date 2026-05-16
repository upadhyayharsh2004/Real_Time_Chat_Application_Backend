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


namespace ConnectHub.Auth.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByToken(string token);
    Task<IList<RefreshToken>> FindActiveByUserId(int userId);
    Task AddToken(RefreshToken token);
    Task RevokeToken(string token);
    Task RevokeAllUserTokens(int userId);
    Task SaveChangesAsync();
}



