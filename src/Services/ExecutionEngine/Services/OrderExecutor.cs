using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Adapters;
using QuantTrader.ExecutionEngine.Models;

namespace QuantTrader.ExecutionEngine.Services;

/// <summary>
/// Executes orders via the configured order adapter (Live or Paper depending on TradingMode).
/// Implements retry logic with exponential backoff (max 3 retries).
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
            "Placing order {OrderId}: {Type} {Side} {Quantity} {Symbol} @ {Price} via {Adapter}",
            order.Id, order.Type, order.Side, order.Quantity, order.Symbol, order.Price, adapter.Name);

        var lastResult = new OrderResult(false, null, "No attempts made", null);

        for (int attempt = 1; attempt <= _settings.MaxRetries; attempt++)
        {
            _logger.LogInformation("Order {OrderId} attempt {Attempt}/{MaxRetries}", order.Id, attempt, _settings.MaxRetries);

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
                        "Order {OrderId} placed successfully on attempt {Attempt}. ExchangeOrderId: {ExchangeOrderId}",
                        order.Id, attempt, lastResult.ExchangeOrderId);
                    return lastResult;
                }

                _logger.LogWarning(
                    "Order {OrderId} attempt {Attempt} failed: {Error}",
                    order.Id, attempt, lastResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Order {OrderId} attempt {Attempt} threw exception",
                    order.Id, attempt);
                lastResult = new OrderResult(false, null, ex.Message, null);
            }

            if (attempt < _settings.MaxRetries)
            {
                var delayMs = _settings.RetryDelayMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogInformation("Waiting {DelayMs}ms before retry", delayMs);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
        }

        _logger.LogError(
            "Order {OrderId} failed after {MaxRetries} attempts. Last error: {Error}",
            order.Id, _settings.MaxRetries, lastResult.ErrorMessage);

        return lastResult;
    }

    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeOrderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _logger.LogInformation("Cancelling order {ExchangeOrderId} for {Symbol}", exchangeOrderId, symbol);

        var result = await _adapterFactory.Current.CancelOrderAsync(exchangeOrderId, symbol, ct).ConfigureAwait(false);

        if (result.Success)
            _logger.LogInformation("Order {ExchangeOrderId} cancelled successfully", exchangeOrderId);
        else
            _logger.LogWarning("Failed to cancel order {ExchangeOrderId}: {Error}", exchangeOrderId, result.ErrorMessage);

        return result;
    }

    public async Task<OrderResult> GetOrderStatusAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeOrderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        _logger.LogDebug("Querying status of order {ExchangeOrderId} for {Symbol}", exchangeOrderId, symbol);

        var result = await _adapterFactory.Current.QueryOrderAsync(exchangeOrderId, symbol, ct).ConfigureAwait(false);

        if (result.Success)
            _logger.LogDebug("Order {ExchangeOrderId} status: {Status}", exchangeOrderId, result.ExecutedOrder?.Status);
        else
            _logger.LogWarning("Failed to query order {ExchangeOrderId}: {Error}", exchangeOrderId, result.ErrorMessage);

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
