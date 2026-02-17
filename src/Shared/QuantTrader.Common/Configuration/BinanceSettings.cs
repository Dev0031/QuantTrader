namespace QuantTrader.Common.Configuration;

/// <summary>Configuration settings for the Binance exchange connection.</summary>
public sealed class BinanceSettings
{
    public const string SectionName = "Binance";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public bool UseTestnet { get; set; }
}
