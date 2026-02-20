using QuantTrader.Common.Configuration;
using QuantTrader.Common.Services;
using QuantTrader.Infrastructure.Extensions;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.Infrastructure.Resilience;
using QuantTrader.RiskManager.Services;
using QuantTrader.RiskManager.Workers;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting RiskManager service");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services));

    // Configuration
    builder.Services.Configure<RiskSettings>(
        builder.Configuration.GetSection(RiskSettings.SectionName));
    builder.Services.Configure<TradingModeSettings>(
        builder.Configuration.GetSection(TradingModeSettings.SectionName));

    // Event bus
    builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

    // Redis
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var config = ConfigurationOptions.Parse(
            builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    });
    builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

    // Time provider (injectable clock for tests)
    builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();

    // Circuit breaker state + health check
    builder.Services.AddPollyPolicies();

    // Services
    builder.Services.AddSingleton<IPositionSizer, PositionSizer>();
    builder.Services.AddSingleton<IDrawdownMonitor, DrawdownMonitor>();
    builder.Services.AddSingleton<IKillSwitchManager, KillSwitchManager>();
    builder.Services.AddScoped<IRiskEvaluator, RiskEvaluator>();

    // Health checks
    builder.Services.AddHealthChecks();

    // Hosted service
    builder.Services.AddHostedService<RiskManagerWorker>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RiskManager service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
