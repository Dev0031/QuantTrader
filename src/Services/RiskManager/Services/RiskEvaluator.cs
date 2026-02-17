using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantTrader.Common.Configuration;
using QuantTrader.Common.Enums;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Redis;
using QuantTrader.RiskManager.Models;

namespace QuantTrader.RiskManager.Services;

/// <summary>Evaluates trade signals against all configured risk rules.</summary>
public sealed class RiskEvaluator : IRiskEvaluator
{
    private readonly IPositionSizer _positionSizer;
    private readonly IDrawdownMonitor _drawdownMonitor;
    private readonly IKillSwitchManager _killSwitchManager;
    private readonly IRedisCacheService _cache;
    private readonly RiskSettings _settings;
    private readonly ILogger<RiskEvaluator> _logger;

    public RiskEvaluator(
        IPositionSizer positionSizer,
        IDrawdownMonitor drawdownMonitor,
        IKillSwitchManager killSwitchManager,
        IRedisCacheService cache,
        IOptions<RiskSettings> settings,
        ILogger<RiskEvaluator> logger)
    {
        _positionSizer = positionSizer ?? throw new ArgumentNullException(nameof(positionSizer));
        _drawdownMonitor = drawdownMonitor ?? throw new ArgumentNullException(nameof(drawdownMonitor));
        _killSwitchManager = killSwitchManager ?? throw new ArgumentNullException(nameof(killSwitchManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RiskEvaluationResult> EvaluateSignalAsync(TradeSignal signal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var metadata = new Dictionary<string, object>
        {
            ["SignalId"] = signal.Id,
            ["Symbol"] = signal.Symbol,
            ["Strategy"] = signal.Strategy
        };

        _logger.LogInformation(
            "Evaluating signal {SignalId} for {Symbol} ({Action}) from strategy {Strategy}",
            signal.Id, signal.Symbol, signal.Action, signal.Strategy);

        // Check 1: Kill switch
        if (_killSwitchManager.IsActive)
        {
            return Reject("Kill switch is active. All trading is halted.", metadata);
        }

        // Check 2: Drawdown limit
        if (_drawdownMonitor.IsKillSwitchTriggered)
        {
            return Reject("Drawdown limit exceeded. Trading suspended.", metadata);
        }

        // Check 3: Stop-loss presence
        if (signal.StopLoss is null)
        {
            return Reject("Signal rejected: stop-loss is required for all trades.", metadata);
        }

        // Check 4: Get portfolio state
        var portfolio = await _cache.GetPortfolioSnapshotAsync(ct).ConfigureAwait(false);
        if (portfolio is null)
        {
            return Reject("Unable to retrieve portfolio snapshot from cache.", metadata);
        }

        // Check 5: Max open positions
        if (signal.Action is TradeAction.Buy or TradeAction.Sell)
        {
            if (portfolio.Positions.Count >= _settings.MaxOpenPositions)
            {
                return Reject(
                    $"Max open positions limit reached ({_settings.MaxOpenPositions}).", metadata);
            }
        }

        // Check 6: Risk-reward ratio
        if (signal.TakeProfit is not null && signal.Price is not null)
        {
            var riskPerUnit = Math.Abs(signal.Price.Value - signal.StopLoss.Value);
            var rewardPerUnit = Math.Abs(signal.TakeProfit.Value - signal.Price.Value);

            if (riskPerUnit > 0)
            {
                var riskRewardRatio = (double)(rewardPerUnit / riskPerUnit);
                metadata["RiskRewardRatio"] = riskRewardRatio;

                if (riskRewardRatio < _settings.MinRiskRewardRatio)
                {
                    return Reject(
                        $"Risk-reward ratio {riskRewardRatio:F2} is below minimum {_settings.MinRiskRewardRatio:F2}.",
                        metadata);
                }
            }
        }

        // Check 7: Position sizing
        var entryPrice = signal.Price ?? 0m;
        if (entryPrice <= 0)
        {
            return Reject("Signal must have a valid entry price for risk evaluation.", metadata);
        }

        var quantity = _positionSizer.CalculatePositionSize(
            portfolio.TotalEquity,
            entryPrice,
            signal.StopLoss.Value,
            _settings.MaxRiskPerTradePercent);

        if (quantity <= 0)
        {
            return Reject("Calculated position size is zero or negative.", metadata);
        }

        metadata["CalculatedQuantity"] = quantity;
        metadata["AccountEquity"] = portfolio.TotalEquity;

        // Build approved order
        var side = signal.Action switch
        {
            TradeAction.Buy => OrderSide.Buy,
            TradeAction.Sell => OrderSide.Sell,
            TradeAction.CloseLong => OrderSide.Sell,
            TradeAction.CloseShort => OrderSide.Buy,
            _ => OrderSide.Buy
        };

        var order = new Order(
            Id: Guid.NewGuid(),
            ExchangeOrderId: null,
            Symbol: signal.Symbol,
            Side: side,
            Type: signal.Price is not null ? OrderType.Limit : OrderType.Market,
            Quantity: quantity,
            Price: signal.Price,
            StopPrice: signal.StopLoss,
            Status: OrderStatus.New,
            FilledQuantity: 0m,
            FilledPrice: 0m,
            Commission: 0m,
            Timestamp: DateTimeOffset.UtcNow,
            UpdatedAt: null);

        _logger.LogInformation(
            "Signal {SignalId} approved: {Side} {Quantity} {Symbol} @ {Price}",
            signal.Id, side, quantity, signal.Symbol, entryPrice);

        return new RiskEvaluationResult(
            Approved: true,
            ApprovedOrder: order,
            RejectionReason: null,
            Metadata: metadata);
    }

    private static RiskEvaluationResult Reject(string reason, Dictionary<string, object> metadata)
    {
        metadata["RejectionReason"] = reason;
        return new RiskEvaluationResult(
            Approved: false,
            ApprovedOrder: null,
            RejectionReason: reason,
            Metadata: metadata);
    }
}
