using QuantTrader.Common.Models;

namespace QuantTrader.RiskManager.Models;

/// <summary>Result of a risk evaluation for a trade signal.</summary>
public sealed record RiskEvaluationResult(
    bool Approved,
    Order? ApprovedOrder,
    string? RejectionReason,
    Dictionary<string, object> Metadata);
