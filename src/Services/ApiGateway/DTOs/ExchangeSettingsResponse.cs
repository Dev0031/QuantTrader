namespace QuantTrader.ApiGateway.DTOs;

/// <summary>Response DTO for saved exchange connection settings.</summary>
public sealed record ExchangeSettingsResponse(
    string Exchange,
    string ApiKeyMasked,
    bool HasSecret,
    bool UseTestnet,
    string Status,
    DateTimeOffset? LastVerified);

/// <summary>Request DTO for saving exchange connection settings.</summary>
public sealed record SaveExchangeSettingsRequest(
    string Exchange,
    string ApiKey,
    string ApiSecret,
    bool UseTestnet);

/// <summary>Response DTO showing required API keys and their status.</summary>
public sealed record ApiKeyStatusResponse(
    string Name,
    string Description,
    bool IsConfigured,
    string? MaskedKey,
    string Status);
