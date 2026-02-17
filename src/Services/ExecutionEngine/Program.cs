using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using QuantTrader.ExecutionEngine.Clients;
using QuantTrader.ExecutionEngine.Services;
using QuantTrader.ExecutionEngine.Workers;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.HealthChecks;
using QuantTrader.Infrastructure.KeyVault;
using QuantTrader.Infrastructure.Messaging;
using QuantTrader.Infrastructure.Redis;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ExecutionEngine"));

// Configuration
builder.Services.Configure<BinanceTradeSettings>(builder.Configuration.GetSection(BinanceTradeSettings.SectionName));
builder.Services.Configure<ExecutionSettings>(builder.Configuration.GetSection(ExecutionSettings.SectionName));

// Database
builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TradingDb")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// Key Vault / Secret Provider
builder.Services.AddSingleton<ISecretProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<KeyVaultService>>();
    var vaultUri = config["KeyVault:VaultUri"];
    return new KeyVaultService(config, logger, vaultUri is not null ? new Uri(vaultUri) : null);
});

// HttpClient for Binance
builder.Services.AddHttpClient<IBinanceTradeClient, BinanceTradeClient>();

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

var host = builder.Build();

Log.Information("ExecutionEngine starting");

host.Run();

Log.Information("ExecutionEngine stopped");
Log.CloseAndFlush();
