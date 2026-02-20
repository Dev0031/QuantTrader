using Microsoft.Extensions.Logging;
using QuantTrader.Common.Models;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>
/// Replays a fixed sequence of <see cref="MarketTick"/> records at a configurable interval.
/// Used in Simulation mode (runtime) and tests (compile-time) — lives in production code so
/// Program.cs can register it without referencing a test project.
/// </summary>
public sealed class FakeMarketDataProvider : IMarketDataProvider
{
    private readonly IReadOnlyList<MarketTick> _ticks;
    private readonly TimeSpan _interval;
    private readonly bool _loop;
    private readonly ILogger<FakeMarketDataProvider> _logger;

    public string Name => "FakeMarketData";

    /// <param name="ticks">Tick sequence to replay. Must not be empty.</param>
    /// <param name="interval">Delay between ticks. Defaults to 100ms.</param>
    /// <param name="loop">If true, replays the sequence indefinitely.</param>
    public FakeMarketDataProvider(
        IReadOnlyList<MarketTick> ticks,
        ILogger<FakeMarketDataProvider> logger,
        TimeSpan? interval = null,
        bool loop = true)
    {
        _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
        _loop = loop;

        if (_ticks.Count == 0)
            throw new ArgumentException("Tick sequence must contain at least one tick.", nameof(ticks));
    }

    public async Task StreamAsync(
        IReadOnlyList<string> symbols,
        Func<MarketTick, CancellationToken, Task> onTick,
        CancellationToken ct)
    {
        _logger.LogInformation("{Provider}: Starting fake tick stream ({Count} ticks, loop={Loop})",
            Name, _ticks.Count, _loop);

        do
        {
            foreach (var tick in _ticks)
            {
                if (ct.IsCancellationRequested) return;

                await onTick(tick, ct);
                await Task.Delay(_interval, ct);
            }
        }
        while (_loop && !ct.IsCancellationRequested);

        _logger.LogInformation("{Provider}: Fake tick stream completed", Name);
    }
}
