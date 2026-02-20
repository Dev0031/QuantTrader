using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;

namespace QuantTrader.Infrastructure.Resilience;

/// <summary>
/// Pre-built Polly v8 resilience pipelines for the three main external boundaries.
/// Each pipeline is created once and stored as a singleton, then used by services.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Pipeline for Binance order placement:
    /// Timeout(10s) → Retry(3, exponential+jitter) → CircuitBreaker(breaks 60s after failures).
    /// When the circuit opens, calls <paramref name="onCircuitOpen"/> to switch to Paper mode.
    /// </summary>
    public static ResiliencePipeline CreateBinanceOrderPipeline(
        ILogger logger,
        CircuitBreakerState state,
        ITradingModeProvider? modeProvider = null,
        int breakDurationSeconds = 60,
        int maxRetries = 3)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                OnRetry = args =>
                {
                    logger.LogWarning("Binance order retry {Attempt}/{Max} after {Delay}ms",
                        args.AttemptNumber + 1, maxRetries, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds),
                OnOpened = args =>
                {
                    logger.LogCritical("Binance circuit OPENED. Breaking for {Duration}s. Auto-switching to Paper mode.",
                        breakDurationSeconds);
                    state.SetBinanceOpen(true);
                    modeProvider?.SetMode(TradingMode.Paper);
                    return default;
                },
                OnClosed = args =>
                {
                    logger.LogWarning("Binance circuit CLOSED. Resuming normal operation.");
                    state.SetBinanceOpen(false);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Binance circuit HALF-OPEN. Testing connectivity.");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Pipeline for Redis cache operations:
    /// Timeout(2s) → Retry(2, 200ms linear) → CircuitBreaker(breaks 30s).
    /// </summary>
    public static ResiliencePipeline CreateRedisOperationPipeline(
        ILogger logger,
        CircuitBreakerState state,
        int breakDurationSeconds = 30)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(2)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    logger.LogWarning("Redis operation retry {Attempt}/2", args.AttemptNumber + 1);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(20),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds),
                OnOpened = args =>
                {
                    logger.LogCritical("Redis circuit OPENED. Breaking for {Duration}s.", breakDurationSeconds);
                    state.SetRedisOpen(true);
                    return default;
                },
                OnClosed = args =>
                {
                    logger.LogWarning("Redis circuit CLOSED. Cache resuming.");
                    state.SetRedisOpen(false);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Pipeline for event bus publish operations:
    /// Retry(3, 500ms exponential).
    /// </summary>
    public static ResiliencePipeline CreateEventBusPublishPipeline(
        ILogger logger,
        CircuitBreakerState state)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                OnRetry = args =>
                {
                    logger.LogWarning("EventBus publish retry {Attempt}/3", args.AttemptNumber + 1);
                    return default;
                }
            })
            .Build();
    }
}
