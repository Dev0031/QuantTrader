using Serilog;
using QuantTrader.Common.Configuration;
using QuantTrader.StrategyEngine.Configuration;
using QuantTrader.StrategyEngine.Services;
using QuantTrader.StrategyEngine.Strategies;
using QuantTrader.StrategyEngine.Workers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting StrategyEngine service");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog from appsettings
    builder.Services.AddSerilog(config => config
        .ReadFrom.Configuration(builder.Configuration));

    // Bind configuration sections
    builder.Services.Configure<StrategySettings>(
        builder.Configuration.GetSection(StrategySettings.SectionName));
    builder.Services.Configure<MaCrossoverSettings>(
        builder.Configuration.GetSection(MaCrossoverSettings.SectionName));
    builder.Services.Configure<MomentumSettings>(
        builder.Configuration.GetSection(MomentumSettings.SectionName));
    builder.Services.Configure<MeanReversionSettings>(
        builder.Configuration.GetSection(MeanReversionSettings.SectionName));
    builder.Services.Configure<BreakoutSettings>(
        builder.Configuration.GetSection(BreakoutSettings.SectionName));

    // Register strategy implementations
    builder.Services.AddSingleton<IStrategy, MaCrossoverStrategy>();
    builder.Services.AddSingleton<IStrategy, MomentumStrategy>();
    builder.Services.AddSingleton<IStrategy, MeanReversionStrategy>();
    builder.Services.AddSingleton<IStrategy, BreakoutStrategy>();

    // Register services
    builder.Services.AddSingleton<IStrategyManager, StrategyManager>();
    builder.Services.AddSingleton<CandleAggregator>();

    // Register hosted service
    builder.Services.AddHostedService<StrategyWorker>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
            tags: ["live"])
        .AddCheck("ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
            tags: ["ready"]);

    var host = builder.Build();

    // Map health check endpoints (requires WebApplication, but for Worker SDK we log availability)
    // For a Worker service, health checks are typically exposed via a minimal Kestrel endpoint:
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "StrategyEngine terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
