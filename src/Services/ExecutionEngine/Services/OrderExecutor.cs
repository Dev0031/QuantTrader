using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>
/// Executes orders via <see cref="OrderAdapterFactory"/> which selects the correct adapter
/// (Live or Paper) based on the current trading mode.
/// Retry logic is handled by Polly pipelines registered on the HTTP client; no manual loop needed.
/// </summary>
public sealed class OrderExecutor : IOrderExecutor
{
    private readonly OrderAdapterFactory _adapterFactory;
    private readonly ILogger<OrderExecutor> _logger;
    private readonly ExecutionSettings _settings;

    public OrderExecutor(
        OrderAdapterFactory adapterFactory,
        IOptions<ExecutionSettings> settings,
        ILogger<OrderExecutor> logger)
    {
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var adapter = _adapterFactory.Current;

        _logger.LogInformation(
            "[{Adapter}] Placing order {OrderId}: {Type} {Side} {Quantity} {Symbol} @ {Price}",
            adapter.Name, order.Id, order.Type, order.Side, order.Quantity, order.Symbol, order.Price);

        OrderResult? lastResult = null;
        for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
        {
            try
            {
                lastResult = order.Type switch
                {
                    OrderType.Market => await adapter.PlaceMarketOrderAsync(
                        order.Symbol, order.Side, order.Quantity, ct).ConfigureAwait(false),

                    OrderType.Limit => await adapter.PlaceLimitOrderAsync(
                        order.Symbol, order.Side, order.Quantity, order.Price ?? 0m, ct).ConfigureAwait(false),

                    OrderType.StopLoss => await adapter.PlaceStopLossOrderAsync(
                        order.Symbol, order.Side, order.Quantity, order.StopPrice ?? 0m, ct).ConfigureAwait(false),

                    _ => await adapter.PlaceMarketOrderAsync(
                        order.Symbol, order.Side, order.Quantity, ct).ConfigureAwait(false)
                };

                if (lastResult.Success)
                {
                    _logger.LogInformation(
                        "[{Adapter}] Order {OrderId} placed successfully. ExchangeOrderId: {ExchangeOrderId}",
                        adapter.Name, order.Id, lastResult.ExchangeOrderId);
                    return lastResult;
                }

                _logger.LogWarning(
                    "[{Adapter}] Order {OrderId} attempt {Attempt}/{Max} failed: {Error}",
                    adapter.Name, order.Id, attempt, _settings.MaxRetries, lastResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Adapter}] Order {OrderId} attempt {Attempt}/{Max} threw exception",
                    adapter.Name, order.Id, attempt, _settings.MaxRetries);
                lastResult = new OrderResult(false, null, ex.Message, null);
            }

            if (attempt < _settings.MaxRetries && _settings.RetryDelayMs > 0)
                await Task.Delay(_settings.RetryDelayMs, ct).ConfigureAwait(false);
        }

        return lastResult!;
    }

    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeOrderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var adapter = _adapterFactory.Current;
        _logger.LogInformation("[{Adapter}] Cancelling order {ExchangeOrderId} for {Symbol}", adapter.Name, exchangeOrderId, symbol);

        var result = await adapter.CancelOrderAsync(exchangeOrderId, symbol, ct).ConfigureAwait(false);

        if (result.Success)
            _logger.LogInformation("[{Adapter}] Order {ExchangeOrderId} cancelled", adapter.Name, exchangeOrderId);
        else
            _logger.LogWarning("[{Adapter}] Failed to cancel {ExchangeOrderId}: {Error}", adapter.Name, exchangeOrderId, result.ErrorMessage);

        return result;
    }

    public async Task<OrderResult> GetOrderStatusAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeOrderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var adapter = _adapterFactory.Current;
        _logger.LogDebug("[{Adapter}] Querying status of {ExchangeOrderId} for {Symbol}", adapter.Name, exchangeOrderId, symbol);

        var result = await adapter.QueryOrderAsync(exchangeOrderId, symbol, ct).ConfigureAwait(false);

        if (!result.Success)
            _logger.LogWarning("[{Adapter}] Failed to query {ExchangeOrderId}: {Error}", adapter.Name, exchangeOrderId, result.ErrorMessage);

        return result;
    }
}

/// <summary>Configuration settings for order execution behavior.</summary>
public sealed class ExecutionSettings
{
    public const string SectionName = "Execution";

    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int OrderTimeoutSeconds { get; set; } = 30;
}
