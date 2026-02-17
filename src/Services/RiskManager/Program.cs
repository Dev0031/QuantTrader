using QuantTrader.Common.Configuration;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.RiskManager.Services;
using QuantTrader.RiskManager.Workers;
using Serilog;

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

    // Event bus
    builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

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
