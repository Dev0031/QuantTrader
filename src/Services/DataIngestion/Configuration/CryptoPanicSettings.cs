namespace QuantTrader.DataIngestion;

/// <summary>Configuration for CryptoPanic news API.</summary>
public sealed class CryptoPanicSettings
{
    public const string SectionName = "CryptoPanic";

    public string BaseUrl { get; set; } = "https://cryptopanic.com/api/v1";
    public string ApiKey { get; set; } = string.Empty;
}
