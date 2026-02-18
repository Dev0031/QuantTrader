using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using QuantTrader.ApiGateway.Hubs;
using QuantTrader.ApiGateway.Middleware;
using QuantTrader.ApiGateway.Services;
using QuantTrader.ApiGateway.Workers;
using QuantTrader.Common.Configuration;
using QuantTrader.Infrastructure.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---------------------------------------------------------------------------
    // Serilog
    // ---------------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

    // ---------------------------------------------------------------------------
    // Configuration bindings
    // ---------------------------------------------------------------------------
    builder.Services.Configure<RiskSettings>(builder.Configuration.GetSection(RiskSettings.SectionName));
    builder.Services.Configure<StrategySettings>(builder.Configuration.GetSection(StrategySettings.SectionName));

    // ---------------------------------------------------------------------------
    // CORS - allow React dashboard origin
    // ---------------------------------------------------------------------------
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                         ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // ---------------------------------------------------------------------------
    // JWT Bearer Authentication
    // ---------------------------------------------------------------------------
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Jwt:Authority"];
            options.Audience = builder.Configuration["Jwt:Audience"];

            var secretKey = builder.Configuration["Jwt:SecretKey"];
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(options.Authority),
                    ValidIssuer = options.Authority,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            }

            // Allow SignalR to receive the token via query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/trading"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ---------------------------------------------------------------------------
    // Swagger / OpenAPI
    // ---------------------------------------------------------------------------
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "QuantTrader API Gateway",
            Version = "v1",
            Description = "REST API for the QuantTrader crypto trading bot"
        });

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ---------------------------------------------------------------------------
    // SignalR
    // ---------------------------------------------------------------------------
    builder.Services.AddSignalR();

    // ---------------------------------------------------------------------------
    // Controllers
    // ---------------------------------------------------------------------------
    builder.Services.AddControllers();

    // ---------------------------------------------------------------------------
    // Infrastructure services (Redis, DB, EventBus, Observability, Health Checks)
    // ---------------------------------------------------------------------------
    builder.Services.AddRedisCache(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

    builder.Services.AddTradingDatabase(
        builder.Configuration.GetConnectionString("TradingDb") ?? "Host=localhost;Database=quanttrader;Username=postgres;Password=postgres");

    builder.Services.AddEventBus(builder.Configuration);

    builder.Services.AddObservability("ApiGateway");

    builder.Services.AddInfrastructureHealthChecks();

    // ---------------------------------------------------------------------------
    // HTTP clients for API verification
    // ---------------------------------------------------------------------------
    builder.Services.AddHttpClient("ApiVerification", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ---------------------------------------------------------------------------
    // Background services
    // ---------------------------------------------------------------------------
    builder.Services.AddHostedService<RealTimeNotifier>();
    builder.Services.AddHostedService<PortfolioSyncWorker>();

    // ---------------------------------------------------------------------------
    // Rate Limiting
    // ---------------------------------------------------------------------------
    var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
    var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
    });

    // ---------------------------------------------------------------------------
    // Build the application
    // ---------------------------------------------------------------------------
    var app = builder.Build();

    // ---------------------------------------------------------------------------
    // Middleware pipeline
    // ---------------------------------------------------------------------------
    app.UseRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // ---------------------------------------------------------------------------
    // Health checks
    // ---------------------------------------------------------------------------
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false // Liveness: always 200 if the process is running
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    // ---------------------------------------------------------------------------
    // Prometheus metrics endpoint
    // ---------------------------------------------------------------------------
    app.MapGet("/metrics", () => Results.Ok("# Prometheus metrics endpoint placeholder"));

    // ---------------------------------------------------------------------------
    // Map controllers and SignalR hubs
    // ---------------------------------------------------------------------------
    app.MapControllers();
    app.MapHub<TradingHub>("/hubs/trading");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
