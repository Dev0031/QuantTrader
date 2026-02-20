using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuantTrader.Infrastructure.Resilience;

namespace QuantTrader.Infrastructure.HealthChecks;

/// <summary>
/// Reports Degraded health when any circuit breaker is open.
/// Registered as part of <c>AddPollyPolicies()</c>.
/// </summary>
public sealed class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly CircuitBreakerState _state;

    public CircuitBreakerHealthCheck(CircuitBreakerState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_state.AnyOpen)
            return Task.FromResult(HealthCheckResult.Healthy("All circuit breakers closed"));

        var open = new List<string>();
        if (_state.IsBinanceOpen) open.Add("Binance");
        if (_state.IsRedisOpen) open.Add("Redis");
        if (_state.IsEventBusOpen) open.Add("EventBus");

        return Task.FromResult(HealthCheckResult.Degraded(
            $"Open circuit breakers: {string.Join(", ", open)}. System running in degraded mode."));
    }
}
