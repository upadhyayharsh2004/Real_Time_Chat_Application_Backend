using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;
using AspNetCoreRateLimit;
using ConnectHub.Gateway.Middleware;

// ─── Bootstrap Serilog immediately so startup errors are logged ────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ConnectHub API Gateway on port 5255...");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ──────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/gateway-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    });

    // ─── JWT Authentication ───────────────────────────────────────────────
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("JWT Secret is missing from configuration.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Support JWT in query string for SignalR WebSocket connections
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"];
                    var path = ctx.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        ctx.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    // ─── Authorization Policies ───────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("authenticated", policy =>
            policy.RequireAuthenticatedUser());
    });

    // ─── CORS ─────────────────────────────────────────────────────────────
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("GatewayPolicy", policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // required for SignalR
        });
    });

    // ─── Rate Limiting ────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(
        builder.Configuration.GetSection("RateLimit"));
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    builder.Services.AddInMemoryRateLimiting();

    // ─── Health Checks ────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // ─── YARP Reverse Proxy ───────────────────────────────────────────────
    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                                   Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ─── Build App ────────────────────────────────────────────────────────
    var app = builder.Build();

    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";
    });

    app.UseIpRateLimiting();

    app.UseCors("GatewayPolicy");

    app.UseAuthentication();
    app.UseAuthorization();

    // Gateway-level request logging / correlation-id injection
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GatewayLoggingMiddleware>();

    // Health endpoint for the gateway itself
    app.MapHealthChecks("/health");

    // Status endpoint — handy for quick smoke tests
    app.MapGet("/gateway/status", () => new
    {
        Service = "ConnectHub API Gateway",
        Version = "1.0.0",
        Timestamp = DateTime.UtcNow,
        Services = new[]
        {
            new { Name = "Auth",         Port = 5000, Path = "/api/users" },
            new { Name = "Message",      Port = 5001, Path = "/api/messages" },
            new { Name = "ChatRoom",     Port = 5043, Path = "/api/rooms" },
            new { Name = "Presence",     Port = 5007, Path = "/api/presence" },
            new { Name = "Notification", Port = 5009, Path = "/api/notifications" },
            new { Name = "Media",        Port = 5008, Path = "/api/media" }
        }
    });

    // Mount the YARP proxy — this handles everything else
    app.MapReverseProxy();

    Log.Information("ConnectHub Gateway ready. Listening on http://localhost:5255");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}






