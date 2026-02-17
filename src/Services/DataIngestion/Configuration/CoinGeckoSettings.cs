namespace QuantTrader.DataIngestion;

/// <summary>Configuration for CoinGecko API access.</summary>
public sealed class CoinGeckoSettings
{
    public const string SectionName = "CoinGecko";

    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3";
}
