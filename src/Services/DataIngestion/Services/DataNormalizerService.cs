using System.Globalization;
using System.Text.Json;
using QuantTrader.Common.Models;

namespace QuantTrader.DataIngestion.Services;

/// <summary>Normalizes raw API responses from various exchanges into common domain models.</summary>
public interface IDataNormalizerService
{
    /// <summary>Parse a Binance WebSocket trade message into a MarketTick.</summary>
    MarketTick? NormalizeBinanceTrade(string json);

    /// <summary>Parse a Binance kline array element into a Candle.</summary>
    Candle? NormalizeBinanceKline(string symbol, string interval, JsonElement kline);
}

public sealed class DataNormalizerService : IDataNormalizerService
{
    private readonly ILogger<DataNormalizerService> _logger;

    public DataNormalizerService(ILogger<DataNormalizerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a Binance trade stream message.
    /// Expected JSON shape:
    /// {
    ///   "e": "trade", "s": "BTCUSDT", "p": "43210.50", "q": "0.001",
    ///   "b": 123456, "a": 789012, "T": 1704067200000
    /// }
    /// </summary>
    public MarketTick? NormalizeBinanceTrade(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Only process trade events
            if (root.TryGetProperty("e", out var eventType) && eventType.GetString() != "trade")
                return null;

            var symbol = root.GetProperty("s").GetString() ?? string.Empty;
            var priceStr = root.GetProperty("p").GetString() ?? "0";
            var volumeStr = root.GetProperty("q").GetString() ?? "0";

            var price = decimal.Parse(priceStr, CultureInfo.InvariantCulture);
            var volume = decimal.Parse(volumeStr, CultureInfo.InvariantCulture);

            var timestampMs = root.GetProperty("T").GetInt64();
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

            // Binance trade stream does not include bid/ask; use price as approximation
            return new MarketTick(
                Symbol: symbol,
                Price: price,
                Volume: volume,
                BidPrice: price,
                AskPrice: price,
                Timestamp: timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize Binance trade message");
            return null;
        }
    }

    /// <summary>
    /// Parses a Binance kline array element.
    /// Binance kline array format:
    /// [0] Open time, [1] Open, [2] High, [3] Low, [4] Close, [5] Volume,
    /// [6] Close time, [7] Quote volume, ...
    /// </summary>
    public Candle? NormalizeBinanceKline(string symbol, string interval, JsonElement kline)
    {
        try
        {
            if (kline.ValueKind != JsonValueKind.Array || kline.GetArrayLength() < 7)
            {
                _logger.LogWarning("Invalid kline data format for {Symbol}", symbol);
                return null;
            }

            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(kline[0].GetInt64());
            var open = ParseDecimal(kline[1]);
            var high = ParseDecimal(kline[2]);
            var low = ParseDecimal(kline[3]);
            var close = ParseDecimal(kline[4]);
            var volume = ParseDecimal(kline[5]);
            var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(kline[6].GetInt64());

            return new Candle(
                Symbol: symbol.ToUpperInvariant(),
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: volume,
                OpenTime: openTime,
                CloseTime: closeTime,
                Interval: interval);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize Binance kline for {Symbol}", symbol);
            return null;
        }
    }

    private static decimal ParseDecimal(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
            : element.GetDecimal();
    }
}
