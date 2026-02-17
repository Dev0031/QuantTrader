using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.ExecutionEngine.Models;
using QuantTrader.Infrastructure.KeyVault;

namespace QuantTrader.ExecutionEngine.Clients;

/// <summary>
/// Binance REST API client that signs all requests with HMAC-SHA256.
/// Adds timestamp and signature query parameters per Binance API specification.
/// Supports testnet and production endpoints. Respects rate limits.
/// </summary>
public sealed class BinanceTradeClient : IBinanceTradeClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<BinanceTradeClient> _logger;
    private readonly BinanceTradeSettings _settings;

    private string? _cachedApiKey;
    private string? _cachedApiSecret;
    private readonly SemaphoreSlim _secretLock = new(1, 1);
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    private int _requestWeight;
    private DateTimeOffset _weightWindowStart = DateTimeOffset.UtcNow;
    private const int MaxWeightPerMinute = 1200;

    public BinanceTradeClient(
        HttpClient httpClient,
        ISecretProvider secretProvider,
        IOptions<BinanceTradeSettings> settings,
        ILogger<BinanceTradeClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        _httpClient.BaseAddress = new Uri(_settings.RestBaseUrl);
    }

    public async Task<OrderResult> PlaceMarketOrderAsync(string symbol, OrderSide side, decimal quantity, CancellationToken ct = default)
    {
        _logger.LogInformation("Placing MARKET {Side} order for {Quantity} {Symbol}", side, quantity, symbol);

        var queryParams = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture)
        };

        return await SendSignedOrderRequestAsync(queryParams, ct).ConfigureAwait(false);
    }

    public async Task<OrderResult> PlaceLimitOrderAsync(string symbol, OrderSide side, decimal quantity, decimal price, CancellationToken ct = default)
    {
        _logger.LogInformation("Placing LIMIT {Side} order for {Quantity} {Symbol} @ {Price}", side, quantity, symbol, price);

        var queryParams = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "LIMIT",
            ["timeInForce"] = "GTC",
            ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
            ["price"] = price.ToString(CultureInfo.InvariantCulture)
        };

        return await SendSignedOrderRequestAsync(queryParams, ct).ConfigureAwait(false);
    }

    public async Task<OrderResult> PlaceStopLossOrderAsync(string symbol, OrderSide side, decimal quantity, decimal stopPrice, CancellationToken ct = default)
    {
        _logger.LogInformation("Placing STOP_LOSS {Side} order for {Quantity} {Symbol} @ stop {StopPrice}", side, quantity, symbol, stopPrice);

        var queryParams = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "STOP_LOSS",
            ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture)
        };

        return await SendSignedOrderRequestAsync(queryParams, ct).ConfigureAwait(false);
    }

    public async Task<OrderResult> CancelOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        _logger.LogInformation("Cancelling order {OrderId} for {Symbol}", orderId, symbol);

        var queryParams = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["orderId"] = orderId
        };

        try
        {
            await EnforceRateLimitAsync(ct).ConfigureAwait(false);
            var (apiKey, _) = await GetCredentialsAsync(ct).ConfigureAwait(false);
            var signedQuery = await SignQueryStringAsync(queryParams, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v3/order?{signedQuery}");
            request.Headers.Add("X-MBX-APIKEY", apiKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cancel order failed: {StatusCode} - {Body}", response.StatusCode, body);
                return new OrderResult(false, null, $"Cancel failed: {response.StatusCode} - {body}", orderId);
            }

            _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);
            return new OrderResult(true, null, null, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception cancelling order {OrderId} for {Symbol}", orderId, symbol);
            return new OrderResult(false, null, ex.Message, orderId);
        }
    }

    public async Task<OrderResult> QueryOrderAsync(string orderId, string symbol, CancellationToken ct = default)
    {
        _logger.LogDebug("Querying order {OrderId} for {Symbol}", orderId, symbol);

        var queryParams = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["orderId"] = orderId
        };

        try
        {
            await EnforceRateLimitAsync(ct).ConfigureAwait(false);
            var (apiKey, _) = await GetCredentialsAsync(ct).ConfigureAwait(false);
            var signedQuery = await SignQueryStringAsync(queryParams, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v3/order?{signedQuery}");
            request.Headers.Add("X-MBX-APIKEY", apiKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Query order failed: {StatusCode} - {Body}", response.StatusCode, body);
                return new OrderResult(false, null, $"Query failed: {response.StatusCode} - {body}", orderId);
            }

            var order = ParseOrderResponse(body);
            return new OrderResult(true, order, null, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying order {OrderId} for {Symbol}", orderId, symbol);
            return new OrderResult(false, null, ex.Message, orderId);
        }
    }

    public async Task<Dictionary<string, decimal>> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching account balances");

        var queryParams = new Dictionary<string, string>();

        try
        {
            await EnforceRateLimitAsync(ct).ConfigureAwait(false);
            var (apiKey, _) = await GetCredentialsAsync(ct).ConfigureAwait(false);
            var signedQuery = await SignQueryStringAsync(queryParams, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v3/account?{signedQuery}");
            request.Headers.Add("X-MBX-APIKEY", apiKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            var balances = new Dictionary<string, decimal>();

            foreach (var balance in doc.RootElement.GetProperty("balances").EnumerateArray())
            {
                var asset = balance.GetProperty("asset").GetString()!;
                var free = decimal.Parse(balance.GetProperty("free").GetString()!, CultureInfo.InvariantCulture);
                if (free > 0)
                {
                    balances[asset] = free;
                }
            }

            _logger.LogInformation("Retrieved {Count} non-zero balances", balances.Count);
            return balances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching account balances");
            throw;
        }
    }

    private async Task<OrderResult> SendSignedOrderRequestAsync(Dictionary<string, string> queryParams, CancellationToken ct)
    {
        try
        {
            await EnforceRateLimitAsync(ct).ConfigureAwait(false);
            var (apiKey, _) = await GetCredentialsAsync(ct).ConfigureAwait(false);
            var signedQuery = await SignQueryStringAsync(queryParams, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v3/order?{signedQuery}");
            request.Headers.Add("X-MBX-APIKEY", apiKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order request failed: {StatusCode} - {Body}", response.StatusCode, body);
                return new OrderResult(false, null, $"Order failed: {response.StatusCode} - {body}", null);
            }

            var order = ParseOrderResponse(body);
            var exchangeOrderId = ExtractExchangeOrderId(body);

            _logger.LogInformation("Order placed successfully. ExchangeOrderId: {ExchangeOrderId}", exchangeOrderId);
            return new OrderResult(true, order, null, exchangeOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception placing order");
            return new OrderResult(false, null, ex.Message, null);
        }
    }

    private async Task<string> SignQueryStringAsync(Dictionary<string, string> queryParams, CancellationToken ct)
    {
        var (_, apiSecret) = await GetCredentialsAsync(ct).ConfigureAwait(false);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        queryParams["timestamp"] = timestamp;

        var queryString = string.Join("&", queryParams.Select(
            kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return $"{queryString}&signature={signature}";
    }

    private async Task<(string ApiKey, string ApiSecret)> GetCredentialsAsync(CancellationToken ct)
    {
        if (_cachedApiKey is not null && _cachedApiSecret is not null)
        {
            return (_cachedApiKey, _cachedApiSecret);
        }

        await _secretLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedApiKey is not null && _cachedApiSecret is not null)
            {
                return (_cachedApiKey, _cachedApiSecret);
            }

            _cachedApiKey = await _secretProvider.GetSecretAsync(_settings.ApiKeyName, ct).ConfigureAwait(false);
            _cachedApiSecret = await _secretProvider.GetSecretAsync(_settings.ApiSecretName, ct).ConfigureAwait(false);

            _logger.LogInformation("Binance API credentials loaded from secret provider");
            return (_cachedApiKey, _cachedApiSecret);
        }
        finally
        {
            _secretLock.Release();
        }
    }

    private async Task EnforceRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _weightWindowStart).TotalMinutes >= 1)
            {
                _requestWeight = 0;
                _weightWindowStart = now;
            }

            _requestWeight++;

            if (_requestWeight >= MaxWeightPerMinute)
            {
                var waitTime = _weightWindowStart.AddMinutes(1) - now;
                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogWarning("Rate limit approaching. Waiting {WaitMs}ms before next request", waitTime.TotalMilliseconds);
                    await Task.Delay(waitTime, ct).ConfigureAwait(false);
                    _requestWeight = 0;
                    _weightWindowStart = DateTimeOffset.UtcNow;
                }
            }
        }
        finally
        {
            _rateLimitLock.Release();
        }
    }

    private static Order? ParseOrderResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var symbol = root.GetProperty("symbol").GetString() ?? string.Empty;
            var exchangeOrderId = root.GetProperty("orderId").GetInt64().ToString(CultureInfo.InvariantCulture);
            var side = root.GetProperty("side").GetString() == "BUY" ? OrderSide.Buy : OrderSide.Sell;
            var type = ParseOrderType(root.GetProperty("type").GetString());
            var status = ParseOrderStatus(root.GetProperty("status").GetString());

            var quantity = ParseDecimalProperty(root, "origQty");
            var filledQty = ParseDecimalProperty(root, "executedQty");
            var price = ParseDecimalProperty(root, "price");
            var stopPrice = ParseDecimalProperty(root, "stopPrice");

            // Binance returns cummulativeQuoteQty; compute average fill price.
            var cumulativeQuoteQty = ParseDecimalProperty(root, "cummulativeQuoteQty");
            var avgFillPrice = filledQty > 0 ? cumulativeQuoteQty / filledQty : 0m;

            return new Order(
                Id: Guid.NewGuid(),
                ExchangeOrderId: exchangeOrderId,
                Symbol: symbol,
                Side: side,
                Type: type,
                Quantity: quantity,
                Price: price > 0 ? price : null,
                StopPrice: stopPrice > 0 ? stopPrice : null,
                Status: status,
                FilledQuantity: filledQty,
                FilledPrice: avgFillPrice,
                Commission: 0m,
                Timestamp: DateTimeOffset.UtcNow,
                UpdatedAt: null);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractExchangeOrderId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("orderId").GetInt64().ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static decimal ParseDecimalProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            var str = prop.GetString();
            if (str is not null && decimal.TryParse(str, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }
        return 0m;
    }

    private static OrderType ParseOrderType(string? type) => type switch
    {
        "MARKET" => OrderType.Market,
        "LIMIT" => OrderType.Limit,
        "STOP_LOSS" => OrderType.StopLoss,
        "STOP_LOSS_LIMIT" => OrderType.StopLossLimit,
        "TAKE_PROFIT" => OrderType.TakeProfit,
        "TAKE_PROFIT_LIMIT" => OrderType.TakeProfitLimit,
        _ => OrderType.Market
    };

    private static OrderStatus ParseOrderStatus(string? status) => status switch
    {
        "NEW" => OrderStatus.New,
        "PARTIALLY_FILLED" => OrderStatus.PartiallyFilled,
        "FILLED" => OrderStatus.Filled,
        "CANCELED" => OrderStatus.Canceled,
        "REJECTED" => OrderStatus.Rejected,
        "EXPIRED" => OrderStatus.Expired,
        _ => OrderStatus.New
    };

    public void Dispose()
    {
        _secretLock.Dispose();
        _rateLimitLock.Dispose();
    }
}

/// <summary>Configuration settings for the Binance trade client.</summary>
public sealed class BinanceTradeSettings
{
    public const string SectionName = "Binance";

    public bool UseTestnet { get; set; } = true;
    public string RestBaseUrl { get; set; } = "https://testnet.binance.vision";
    public string ApiKeyName { get; set; } = "BinanceApiKey";
    public string ApiSecretName { get; set; } = "BinanceApiSecret";
}
