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
using System.Net;
using System.Text.Json;


namespace ConnectHub.Presence.Middleware;

/// <summary>
/// ExceptionHandlingMiddleware — global exception handler.
/// Same pattern as UC1/UC2/UC3. Returns consistent ApiResponseDto JSON.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted) return;
        context.Response.ContentType = "application/json";

        var (statusCode, message) = ex switch
        {
            KeyNotFoundException      => (HttpStatusCode.NotFound,             ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,       ex.Message),
            ArgumentException         => (HttpStatusCode.BadRequest,           ex.Message),
            InvalidOperationException => (HttpStatusCode.Conflict,             ex.Message),
            _                         => (HttpStatusCode.InternalServerError,  "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        var json = JsonSerializer.Serialize(
            ApiResponseDto<object>.Fail(message),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}




