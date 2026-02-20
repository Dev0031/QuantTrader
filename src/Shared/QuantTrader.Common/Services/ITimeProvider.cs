namespace QuantTrader.Common.Services;

/// <summary>
/// Abstraction over the system clock. Allows tests to control time deterministically
/// without Thread.Sleep or real clock dependencies.
/// </summary>
public interface ITimeProvider
{
    /// <summary>Returns the current UTC time.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production implementation that delegates to <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Test implementation with a manually controllable clock.
/// Start time defaults to a fixed point in the past so tests are deterministic.
/// </summary>
public sealed class FakeTimeProvider : ITimeProvider
{
    private DateTimeOffset _current;
    private readonly object _lock = new();

    public FakeTimeProvider(DateTimeOffset? startTime = null)
    {
        _current = startTime ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow
    {
        get { lock (_lock) { return _current; } }
    }

    /// <summary>Advances the fake clock by the given duration.</summary>
    public void Advance(TimeSpan by)
    {
        lock (_lock) { _current = _current.Add(by); }
    }

    /// <summary>Sets the fake clock to a specific point in time.</summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_lock) { _current = value; }
    }
}
