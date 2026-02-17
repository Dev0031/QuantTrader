namespace QuantTrader.RiskManager.Services;

/// <summary>Calculates common risk and performance metrics for portfolio analysis.</summary>
public static class RiskMetricsCalculator
{
    /// <summary>
    /// Calculates the annualized Sharpe ratio assuming 0% risk-free rate.
    /// Uses daily returns and annualizes by sqrt(365) for crypto markets.
    /// </summary>
    public static double CalculateSharpeRatio(IReadOnlyList<decimal> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 2) return 0.0;

        var mean = (double)returns.Average();
        var variance = returns.Sum(r => ((double)r - mean) * ((double)r - mean)) / (returns.Count - 1);
        var stdDev = Math.Sqrt(variance);

        if (stdDev == 0) return 0.0;

        // Annualize: crypto trades 365 days
        return (mean / stdDev) * Math.Sqrt(365.0);
    }

    /// <summary>
    /// Calculates the maximum drawdown percentage from an equity curve.
    /// Returns a positive value representing the largest peak-to-trough decline.
    /// </summary>
    public static double CalculateMaxDrawdown(IReadOnlyList<decimal> equityCurve)
    {
        ArgumentNullException.ThrowIfNull(equityCurve);

        if (equityCurve.Count < 2) return 0.0;

        var peak = equityCurve[0];
        var maxDrawdown = 0.0;

        for (var i = 1; i < equityCurve.Count; i++)
        {
            if (equityCurve[i] > peak)
            {
                peak = equityCurve[i];
            }
            else if (peak > 0)
            {
                var drawdown = (double)((peak - equityCurve[i]) / peak * 100m);
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }

        return maxDrawdown;
    }

    /// <summary>Calculates win rate as a percentage (0-100).</summary>
    public static double CalculateWinRate(int wins, int total)
    {
        if (total <= 0) return 0.0;
        return (double)wins / total * 100.0;
    }

    /// <summary>
    /// Calculates profit factor (gross profit / gross loss).
    /// A value above 1.0 indicates a profitable system.
    /// </summary>
    public static decimal CalculateProfitFactor(decimal grossProfit, decimal grossLoss)
    {
        if (grossLoss == 0) return grossProfit > 0 ? decimal.MaxValue : 0m;
        return Math.Abs(grossProfit / grossLoss);
    }

    /// <summary>
    /// Calculates expectancy per trade.
    /// Formula: (winRate * avgWin) - ((1 - winRate) * avgLoss)
    /// </summary>
    public static decimal CalculateExpectancy(double winRate, decimal avgWin, decimal avgLoss)
    {
        var winRateDecimal = (decimal)(winRate / 100.0);
        var lossRate = 1m - winRateDecimal;
        return (winRateDecimal * avgWin) - (lossRate * Math.Abs(avgLoss));
    }
}
