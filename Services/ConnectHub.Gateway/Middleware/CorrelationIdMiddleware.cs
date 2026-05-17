using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
namespace ConnectHub.Gateway.Middleware;

/// <summary>
/// Ensures every request has an X-Correlation-Id header.
/// If the caller supplies one it is preserved; otherwise a new GUID is generated.
/// The same value again is echoed back in the response so clients can trace calls end-to-end.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationHeader, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationHeader] = correlationId;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}




