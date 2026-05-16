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


namespace ConnectHub.Auth.Services.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int? GetUserIdFromToken(string token);
    bool ValidateToken(string token);
}



