using QuantTrader.Common.Models;
using QuantTrader.RiskManager.Models;

namespace QuantTrader.RiskManager.Services;

/// <summary>Evaluates trade signals against risk rules before allowing order execution.</summary>
public interface IRiskEvaluator
{
    /// <summary>Evaluates a trade signal against all configured risk rules.</summary>
    Task<RiskEvaluationResult> EvaluateSignalAsync(TradeSignal signal, CancellationToken ct = default);
}
