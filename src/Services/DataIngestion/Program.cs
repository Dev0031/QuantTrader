using Microsoft.EntityFrameworkCore;
using QuantTrader.Common.Configuration;
using QuantTrader.DataIngestion;
using QuantTrader.DataIngestion.Clients;
using QuantTrader.DataIngestion.Services;
using QuantTrader.DataIngestion.Workers;
using QuantTrader.Infrastructure.Database;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting QuantTrader DataIngestion service");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services));

    // Configuration sections
    builder.Services.Configure<BinanceSettings>(builder.Configuration.GetSection(BinanceSettings.SectionName));
    builder.Services.Configure<CoinGeckoSettings>(builder.Configuration.GetSection(CoinGeckoSettings.SectionName));
    builder.Services.Configure<CryptoPanicSettings>(builder.Configuration.GetSection(CryptoPanicSettings.SectionName));

    // Bind symbols list for injection
    builder.Services.Configure<SymbolsOptions>(opts =>
        opts.Symbols = builder.Configuration.GetSection("Symbols").Get<List<string>>() ?? []);

    // Redis
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
    builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

    // EF Core - TimescaleDB / PostgreSQL
    builder.Services.AddDbContext<TradingDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

    // HttpClientFactory - typed clients
    builder.Services.AddHttpClient<IBinanceRestClient, BinanceRestClient>(client =>
    {
        var settings = builder.Configuration.GetSection(BinanceSettings.SectionName).Get<BinanceSettings>();
        client.BaseAddress = new Uri(settings?.BaseUrl ?? "https://testnet.binance.vision");
    });

    builder.Services.AddHttpClient<ICoinGeckoClient, CoinGeckoClient>(client =>
    {
        var settings = builder.Configuration.GetSection(CoinGeckoSettings.SectionName).Get<CoinGeckoSettings>();
        client.BaseAddress = new Uri(settings?.BaseUrl ?? "https://api.coingecko.com/api/v3");
    });

    builder.Services.AddHttpClient<ICryptoPanicClient, CryptoPanicClient>(client =>
    {
        var settings = builder.Configuration.GetSection(CryptoPanicSettings.SectionName).Get<CryptoPanicSettings>();
        client.BaseAddress = new Uri(settings?.BaseUrl ?? "https://cryptopanic.com/api/v1");
    });

    // Application services
    builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
    builder.Services.AddSingleton<IDataNormalizerService, DataNormalizerService>();
    builder.Services.AddScoped<IDataPersistenceService, DataPersistenceService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? string.Empty, name: "postgresql")
        .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

    // Hosted workers
    builder.Services.AddHostedService<BinanceWebSocketWorker>();
    builder.Services.AddHostedService<MarketDataPollingWorker>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Health check endpoints
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false // liveness: always healthy if process is up
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true // readiness: checks DB + Redis
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DataIngestion service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
