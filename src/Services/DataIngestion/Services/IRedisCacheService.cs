namespace QuantTrader.DataIngestion.Services;

/// <summary>Abstraction over Redis for caching market data.</summary>
public interface IRedisCacheService
{
    /// <summary>Set the latest price for a symbol.</summary>
    Task SetLatestPriceAsync(string symbol, decimal price, CancellationToken cancellationToken = default);

    /// <summary>Get the latest cached price for a symbol.</summary>
    Task<decimal?> GetLatestPriceAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>Set a value in cache with optional expiry.</summary>
    Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>Get a value from cache.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
}
