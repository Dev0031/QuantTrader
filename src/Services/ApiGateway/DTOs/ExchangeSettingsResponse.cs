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
    string? ApiSecret,
    bool UseTestnet);

/// <summary>Response DTO showing required API keys and their status.</summary>
public sealed record ApiKeyStatusResponse(
    string Name,
    string Description,
    bool IsConfigured,
    string? MaskedKey,
    string Status);

/// <summary>Metadata about a supported API provider.</summary>
public sealed record ApiProviderInfoResponse(
    string Name,
    bool RequiresApiKey,
    bool RequiresApiSecret,
    bool SupportsTestnet,
    bool IsRequired,
    string Description,
    string[] Features,
    bool IsConfigured,
    string? MaskedKey,
    string Status,
    DateTimeOffset? LastVerified);

/// <summary>Result of verifying an API provider connection.</summary>
public sealed record VerificationResultResponse(
    bool Success,
    string Status,
    string Message,
    long LatencyMs,
    bool GeoRestricted = false);

/// <summary>Health status of a data integration.</summary>
public sealed record IntegrationStatusResponse(
    string Provider,
    string Status,
    DateTimeOffset? LastDataAt,
    string? LastError,
    int DataPointsLast5Min);

/// <summary>One step in the API key verification process.</summary>
public sealed record VerificationStepResult(
    int Step,
    string Name,
    string Status,   // "success" | "error" | "skipped" | "warning" | "running"
    string Message,
    int DurationMs);

/// <summary>Detected permissions for a verified API key.</summary>
public sealed record ApiKeyPermissions(
    bool CanReadMarketData,
    bool CanReadAccount,
    bool CanTrade,
    bool CanWithdraw);

/// <summary>Detailed step-by-step verification response.</summary>
public sealed record DetailedVerificationResponse(
    bool Success,
    string Status,
    string Message,
    int LatencyMs,
    bool GeoRestricted,
    IReadOnlyList<VerificationStepResult> Steps,
    ApiKeyPermissions? Permissions);

/// <summary>Live connection health metrics for an exchange.</summary>
public sealed record ConnectionHealthResponse(
    string Exchange,
    bool IsConnected,
    int RestLatencyMs,
    bool WebSocketActive,
    DateTimeOffset? LastTickAt,
    int TicksPerMinute,
    int RequestWeightUsed,
    int RequestWeightLimit);
