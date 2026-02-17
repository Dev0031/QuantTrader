namespace QuantTrader.Common.Constants;

/// <summary>Constants for Binance exchange endpoints, rate limits, and common trading pairs.</summary>
public static class ExchangeConstants
{
    public const string BinanceTestnetBaseUrl = "https://testnet.binance.vision";
    public const string BinanceTestnetWebSocketUrl = "wss://testnet.binance.vision/ws";
    public const string BinanceProductionBaseUrl = "https://api.binance.com";
    public const string BinanceProductionWebSocketUrl = "wss://stream.binance.com:9443/ws";

    public const int MaxWeightPerMinute = 1200;

    public static readonly IReadOnlyList<string> CommonTradingPairs =
    [
        "BTCUSDT",
        "ETHUSDT",
        "BNBUSDT",
        "SOLUSDT",
        "XRPUSDT",
        "ADAUSDT",
        "DOGEUSDT",
        "AVAXUSDT",
        "DOTUSDT",
        "LINKUSDT"
    ];
}
