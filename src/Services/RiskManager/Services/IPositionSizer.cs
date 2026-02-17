namespace QuantTrader.RiskManager.Services;

/// <summary>Calculates position sizes based on risk parameters and account equity.</summary>
public interface IPositionSizer
{
    /// <summary>
    /// Calculates the position size (quantity) for a trade based on fixed-fractional risk.
    /// Formula: quantity = (equity * riskPercent / 100) / |entryPrice - stopLossPrice|
    /// Result is clamped to min/max order size constraints.
    /// </summary>
    decimal CalculatePositionSize(
        decimal accountEquity,
        decimal entryPrice,
        decimal stopLossPrice,
        double riskPercent);
}
