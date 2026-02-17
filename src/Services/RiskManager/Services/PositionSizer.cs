using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;

namespace QuantTrader.RiskManager.Services;

/// <summary>Calculates position sizes using fixed-fractional risk management.</summary>
public sealed class PositionSizer : IPositionSizer
{
    private const decimal DefaultMinOrderSize = 0.001m;
    private const decimal DefaultMaxOrderSize = 100_000m;

    private readonly RiskSettings _settings;
    private readonly ILogger<PositionSizer> _logger;

    public PositionSizer(IOptions<RiskSettings> settings, ILogger<PositionSizer> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public decimal CalculatePositionSize(
        decimal accountEquity,
        decimal entryPrice,
        decimal stopLossPrice,
        double riskPercent)
    {
        if (accountEquity <= 0)
        {
            _logger.LogWarning("Account equity is {Equity}, returning zero position size", accountEquity);
            return 0m;
        }

        if (entryPrice <= 0 || stopLossPrice <= 0)
        {
            _logger.LogWarning(
                "Invalid prices: entry={Entry}, stopLoss={StopLoss}", entryPrice, stopLossPrice);
            return 0m;
        }

        // Never risk more than MaxRiskPerTradePercent regardless of input
        var effectiveRiskPercent = Math.Min(riskPercent, _settings.MaxRiskPerTradePercent);
        if (effectiveRiskPercent <= 0)
        {
            _logger.LogWarning("Effective risk percent is {RiskPct}, returning zero", effectiveRiskPercent);
            return 0m;
        }

        var riskPerUnit = Math.Abs(entryPrice - stopLossPrice);
        if (riskPerUnit == 0)
        {
            _logger.LogWarning("Entry price equals stop-loss price, cannot calculate position size");
            return 0m;
        }

        // quantity = (equity * riskPercent / 100) / |entryPrice - stopLossPrice|
        var riskAmount = accountEquity * (decimal)(effectiveRiskPercent / 100.0);
        var quantity = riskAmount / riskPerUnit;

        // Clamp to min/max order size
        quantity = Math.Clamp(quantity, DefaultMinOrderSize, DefaultMaxOrderSize);

        _logger.LogDebug(
            "Position size: equity={Equity}, risk%={RiskPct}, riskPerUnit={RiskPerUnit}, quantity={Quantity}",
            accountEquity, effectiveRiskPercent, riskPerUnit, quantity);

        return Math.Round(quantity, 8);
    }
}
