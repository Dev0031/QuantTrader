using QuantTrader.Common.Configuration;
using QuantTrader.RiskManager.Services;
using QuantTrader.RiskManager.Workers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting RiskManager service");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog
    builder.Services.AddSerilog(config => config
        .ReadFrom.Configuration(builder.Configuration));

    // Configuration
    builder.Services.Configure<RiskSettings>(
        builder.Configuration.GetSection(RiskSettings.SectionName));

    // Services
    builder.Services.AddSingleton<IPositionSizer, PositionSizer>();
    builder.Services.AddSingleton<IDrawdownMonitor, DrawdownMonitor>();
    builder.Services.AddSingleton<IKillSwitchManager, KillSwitchManager>();
    builder.Services.AddScoped<IRiskEvaluator, RiskEvaluator>();

    // Health checks
    builder.Services.AddHealthChecks();

    // Hosted service
    builder.Services.AddHostedService<RiskManagerWorker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RiskManager service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
