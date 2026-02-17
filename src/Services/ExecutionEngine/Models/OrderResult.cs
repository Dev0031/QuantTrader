using QuantTrader.Common.Models;

namespace QuantTrader.ExecutionEngine.Models;

/// <summary>Represents the result of an order execution attempt against the exchange.</summary>
public sealed record OrderResult(
    bool Success,
    Order? ExecutedOrder,
    string? ErrorMessage,
    string? ExchangeOrderId);
