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
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Microsoft.Extensions.Options;

// ── Serilog ───────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Database — PostgreSQL + EF Core 8 ─────────────────────────────
builder.Services.AddDbContext<MediaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MediaDb"),
        npgsqlOptions => {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "media");
        }));

builder.Services.Configure<AzureBlobOptions>(
    builder.Configuration.GetSection("AzureBlob"));

var azureBlobConnectionString = builder.Configuration["AzureBlob:ConnectionString"]
    ?? "UseDevelopmentStorage=true"; // Azurite for local dev

builder.Services.AddSingleton(new BlobServiceClient(azureBlobConnectionString));

// ── JWT Bearer — same secret as UC1-UC5 ───────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwt["Issuer"],
        ValidAudience            = jwt["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Secret"]!))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ── Repositories ──────────────────────────────────────────────────
builder.Services.AddScoped<IMediaRepository, MediaRepository>();

// ── RabbitMQ Publisher ────────────────────────────────────────────
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IMediaService, MediaService>();

// ── RabbitMQ Consumer ─────────────────────────────────────────────
builder.Services.AddHostedService<MediaConsumer>();

// ── MediaCleanupService ───────────────────────────────────────────
builder.Services.AddHostedService<MediaCleanupService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ConnectHub — Media API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConnectHubPolicy", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5000", "http://localhost:5255" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

builder.Services.AddHealthChecks().AddDbContextCheck<MediaDbContext>("media-db");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    var retries = 0;
    while (true)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (retries < 10)
        {
            retries++;
            logger.LogWarning(ex, "Migration failed (attempt {Retry}/10). Retrying in 5s...", retries);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Media API v1"); });
app.UseSerilogRequestLogging();
app.UseCors("ConnectHubPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
