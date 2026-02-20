using QuantTrader.Common.Models;

namespace QuantTrader.TestInfrastructure.Builders;

/// <summary>
/// Fluent builder for deterministic market tick/candle sequences.
/// Used by tests to create specific price scenarios without hitting a real exchange.
/// </summary>
public sealed class MarketScenarioBuilder
{
    private string _symbol = "BTCUSDT";
    private decimal _startPrice = 50_000m;
    private double _trendFactor = 0;
    private double _volatility = 0;
    private int _breakoutAtTick = -1;
    private double _breakoutMagnitude = 0;
    private int? _randomSeed;

    public MarketScenarioBuilder ForSymbol(string symbol)
    {
        _symbol = symbol;
        return this;
    }

    public MarketScenarioBuilder StartingAt(decimal price)
    {
        _startPrice = price;
        return this;
    }

    /// <summary>Steady uptrend or downtrend. factor=0.001 = +0.1% per tick.</summary>
    public MarketScenarioBuilder Trending(double factor)
    {
        _trendFactor = factor;
        return this;
    }

    /// <summary>Flat range with optional noise.</summary>
    public MarketScenarioBuilder Ranging(double noiseFraction = 0.001)
    {
        _volatility = noiseFraction;
        _trendFactor = 0;
        return this;
    }

    /// <summary>Random walk with the given seed for determinism.</summary>
    public MarketScenarioBuilder Volatile(int seed = 42)
    {
        _randomSeed = seed;
        _volatility = 0.005;
        return this;
    }

    /// <summary>Inserts a sharp price breakout at the specified tick index.</summary>
    public MarketScenarioBuilder WithBreakoutAt(int tickIndex, double magnitude)
    {
        _breakoutAtTick = tickIndex;
        _breakoutMagnitude = magnitude;
        return this;
    }

    /// <summary>Generates the tick sequence.</summary>
    public IReadOnlyList<MarketTick> BuildTicks(int count = 200)
    {
        var rng = _randomSeed.HasValue ? new Random(_randomSeed.Value) : new Random(0);
        var ticks = new List<MarketTick>(count);
        var price = _startPrice;
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < count; i++)
        {
            // Apply trend
            price *= (decimal)(1 + _trendFactor);

            // Apply noise / volatility
            if (_volatility > 0)
            {
                var noise = (rng.NextDouble() * 2 - 1) * _volatility;
                price *= (decimal)(1 + noise);
            }

            // Apply breakout
            if (i == _breakoutAtTick)
                price *= (decimal)(1 + _breakoutMagnitude);

            price = Math.Max(price, 0.01m); // Guard against negative prices

            ticks.Add(new MarketTick(
                Symbol: _symbol,
                Price: Math.Round(price, 2),
                Volume: 1m,
                BidPrice: Math.Round(price * 0.9999m, 2),
                AskPrice: Math.Round(price * 1.0001m, 2),
                Timestamp: baseTime.AddSeconds(i)));
        }

        return ticks;
    }
}
