using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuantTrader.Common.Events;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Messaging;

namespace QuantTrader.StrategyEngine.Services;

/// <summary>
/// Aggregates market ticks into OHLCV candles for configurable time intervals.
/// Emits a <see cref="CandleClosedEvent"/> when each candle completes.
/// Maintains open candle state per symbol per interval.
/// </summary>
public sealed class CandleAggregator
{
    private readonly ILogger<CandleAggregator> _logger;
    private readonly IEventBus _eventBus;

    /// <summary>Key: "SYMBOL|INTERVAL", Value: in-progress candle builder.</summary>
    private readonly ConcurrentDictionary<string, CandleBuilder> _openCandles = new();

    public CandleAggregator(ILogger<CandleAggregator> logger, IEventBus eventBus)
    {
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Processes an incoming tick, updating the open candle for the given interval.
    /// If the tick falls outside the current candle window, the open candle is closed and emitted.
    /// </summary>
    public async Task ProcessTickAsync(MarketTick tick, TimeSpan interval, CancellationToken ct = default)
    {
        string intervalLabel = FormatInterval(interval);
        string key = $"{tick.Symbol}|{intervalLabel}";

        var builder = _openCandles.GetOrAdd(key, _ =>
        {
            var windowStart = AlignToInterval(tick.Timestamp, interval);
            return new CandleBuilder(tick.Symbol, intervalLabel, windowStart, interval);
        });

        // If this tick falls in a new candle window, close the previous one
        var currentWindowStart = AlignToInterval(tick.Timestamp, interval);

        if (currentWindowStart > builder.OpenTime)
        {
            // Close and emit the completed candle
            var closedCandle = builder.Build();
            if (closedCandle is not null)
            {
                var evt = new CandleClosedEvent(
                    Candle: closedCandle,
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: "CandleAggregator");

                try
                {
                    await _eventBus.PublishAsync(evt, "candle.closed", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish CandleClosedEvent for {Symbol} [{Interval}]",
                        tick.Symbol, intervalLabel);
                }

                _logger.LogDebug(
                    "Candle closed: {Symbol} [{Interval}] O={Open} H={High} L={Low} C={Close} V={Volume}",
                    closedCandle.Symbol, closedCandle.Interval,
                    closedCandle.Open, closedCandle.High, closedCandle.Low, closedCandle.Close, closedCandle.Volume);
            }

            // Start a new candle
            var newBuilder = new CandleBuilder(tick.Symbol, intervalLabel, currentWindowStart, interval);
            newBuilder.Update(tick);
            _openCandles[key] = newBuilder;
        }
        else
        {
            builder.Update(tick);
        }
    }

    private static DateTimeOffset AlignToInterval(DateTimeOffset timestamp, TimeSpan interval)
    {
        long ticks = timestamp.UtcTicks;
        long intervalTicks = interval.Ticks;
        long aligned = ticks - (ticks % intervalTicks);
        return new DateTimeOffset(aligned, TimeSpan.Zero);
    }

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalDays >= 1)
            return $"{(int)interval.TotalDays}d";
        if (interval.TotalHours >= 1)
            return $"{(int)interval.TotalHours}h";
        return $"{(int)interval.TotalMinutes}m";
    }

    /// <summary>Builds an OHLCV candle from incoming ticks.</summary>
    private sealed class CandleBuilder
    {
        private readonly string _symbol;
        private readonly string _interval;
        private readonly TimeSpan _duration;
        private decimal _open;
        private decimal _high;
        private decimal _low;
        private decimal _close;
        private decimal _volume;
        private bool _hasData;

        public DateTimeOffset OpenTime { get; }

        public CandleBuilder(string symbol, string interval, DateTimeOffset openTime, TimeSpan duration)
        {
            _symbol = symbol;
            _interval = interval;
            OpenTime = openTime;
            _duration = duration;
        }

        public void Update(MarketTick tick)
        {
            if (!_hasData)
            {
                _open = tick.Price;
                _high = tick.Price;
                _low = tick.Price;
                _hasData = true;
            }
            else
            {
                if (tick.Price > _high) _high = tick.Price;
                if (tick.Price < _low) _low = tick.Price;
            }

            _close = tick.Price;
            _volume += tick.Volume;
        }

        public Candle? Build()
        {
            if (!_hasData)
                return null;

            return new Candle(
                Symbol: _symbol,
                Open: _open,
                High: _high,
                Low: _low,
                Close: _close,
                Volume: _volume,
                OpenTime: OpenTime,
                CloseTime: OpenTime + _duration,
                Interval: _interval);
        }
    }
}
