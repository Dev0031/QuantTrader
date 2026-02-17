using Microsoft.AspNetCore.SignalR;

namespace QuantTrader.ApiGateway.Hubs;

/// <summary>
/// SignalR hub for real-time trading updates.
/// Pushes tick updates, trade executions, position changes, risk alerts, and kill switch events.
/// </summary>
public sealed class TradingHub : Hub
{
    private readonly ILogger<TradingHub> _logger;

    public TradingHub(ILogger<TradingHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, "trades");
        await Groups.AddToGroupAsync(Context.ConnectionId, "prices");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Reason: {Reason}",
            Context.ConnectionId, exception?.Message ?? "graceful");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Subscribes the caller to real-time updates for a specific symbol.</summary>
    public async Task SubscribeToSymbol(string symbol)
    {
        var group = $"symbol:{symbol.ToUpperInvariant()}";
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {ConnectionId} subscribed to {Symbol}", Context.ConnectionId, symbol);
    }

    /// <summary>Unsubscribes the caller from real-time updates for a specific symbol.</summary>
    public async Task UnsubscribeFromSymbol(string symbol)
    {
        var group = $"symbol:{symbol.ToUpperInvariant()}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Client {ConnectionId} unsubscribed from {Symbol}", Context.ConnectionId, symbol);
    }

    // Server-to-client push methods (invoked via IHubContext<TradingHub>):
    // - OnTickUpdate(TickData data)
    // - OnTradeExecuted(TradeData data)
    // - OnPositionUpdate(PositionData data)
    // - OnRiskAlert(AlertData data)
    // - OnKillSwitch(KillSwitchData data)
}
