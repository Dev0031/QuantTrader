using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using QuantTrader.Common.Configuration;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.ExecutionEngine.Workers;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.HealthChecks;
using QuantTrader.Infrastructure.KeyVault;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Service", "ExecutionEngine"));

// Configuration
builder.Services.Configure<BinanceTradeSettings>(builder.Configuration.GetSection(BinanceTradeSettings.SectionName));
builder.Services.Configure<ExecutionSettings>(builder.Configuration.GetSection(ExecutionSettings.SectionName));
builder.Services.Configure<TradingModeSettings>(builder.Configuration.GetSection(TradingModeSettings.SectionName));

// Database
builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TradingDb")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConfig = ConfigurationOptions.Parse(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
    redisConfig.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(redisConfig);
});
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// Key Vault / Secret Provider
builder.Services.AddSingleton<ISecretProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<KeyVaultService>>();
    var vaultUri = config["KeyVault:VaultUri"];
    return new KeyVaultService(config, logger, !string.IsNullOrWhiteSpace(vaultUri) ? new Uri(vaultUri) : null);
});

// HttpClient for Binance
builder.Services.AddHttpClient<IBinanceTradeClient, BinanceTradeClient>();

// Trading Mode Provider
builder.Services.AddSingleton<ITradingModeProvider, TradingModeProvider>();

// Order Adapters
builder.Services.AddSingleton<LiveOrderAdapter>();
builder.Services.AddSingleton<PaperOrderAdapter>();
builder.Services.AddSingleton<OrderAdapterFactory>();

// Services
builder.Services.AddScoped<IOrderExecutor, OrderExecutor>();
builder.Services.AddScoped<IOrderTracker, OrderTracker>();
builder.Services.AddScoped<IPositionTracker, PositionTracker>();
builder.Services.AddScoped<ITradeJournal, TradeJournal>();

// Event Bus
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<DatabaseHealthCheck>("database");

// Hosted Service
builder.Services.AddHostedService<ExecutionWorker>();

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

Log.Information("ExecutionEngine starting");

app.Run();

Log.Information("ExecutionEngine stopped");
Log.CloseAndFlush();
