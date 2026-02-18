import React from "react";
import { X } from "lucide-react";
import StatusBadge from "@/components/common/StatusBadge";
import PageGuide from "@/components/common/PageGuide";
import DataStatusBanner from "@/components/common/DataStatusBanner";
import { usePositions, useClosePosition, useSetupStatus } from "@/api/hooks";
import type { MarketTick } from "@/api/types";

interface PositionsPageProps {
  lastTick: MarketTick | null;
}

const PositionsPage: React.FC<PositionsPageProps> = ({ lastTick }) => {
  const { data: positions, isLoading } = usePositions();
  const closePosition = useClosePosition();
  const setupStatus = useSetupStatus();

  const handleClose = (symbol: string) => {
    if (window.confirm(`Close position on ${symbol}?`)) {
      closePosition.mutate(symbol);
    }
  };

  const formatPrice = (price: number) =>
    price >= 1 ? price.toFixed(2) : price.toPrecision(6);

  const formatPnl = (pnl: number) => {
    const sign = pnl >= 0 ? "+" : "";
    return `${sign}$${pnl.toFixed(2)}`;
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-accent" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageGuide pageId="positions">
        <p><strong>What you see:</strong> All currently open trading positions with real-time pricing.</p>
        <p><strong>Side:</strong> Long = bought, profiting when price rises. Short = sold, profiting when price falls.</p>
        <p><strong>Current Price:</strong> Yellow highlight = real-time WebSocket update. White = last known price.</p>
        <p><strong>Unrealized P&L:</strong> What you'd gain/lose if this position closed right now. Updates with price.</p>
        <p><strong>Stop Loss:</strong> Price at which position auto-closes to limit losses. Every trade MUST have one (risk rule).</p>
        <p><strong>Take Profit:</strong> Price target for automatic profit-taking.</p>
        <p><strong>Close button:</strong> Manually close at current market price. Requires confirmation.</p>
      </PageGuide>

      {!setupStatus.isReady && (
        <DataStatusBanner
          type="warning"
          title="Cannot display positions"
          message="Binance API key required to view and manage positions."
          action={{ label: "Configure Now", href: "/settings" }}
          dismissKey="positions-binance"
        />
      )}

      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Open Positions</h1>
        <span className="text-sm text-gray-400">
          {positions?.length ?? 0} position{(positions?.length ?? 0) !== 1 ? "s" : ""}
        </span>
      </div>

      <div className="card">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-gray-400 text-xs uppercase tracking-wider border-b border-panel-border">
                <th className="text-left py-3 px-3 font-medium">Symbol</th>
                <th className="text-left py-3 px-3 font-medium">Side</th>
                <th className="text-right py-3 px-3 font-medium">Entry Price</th>
                <th className="text-right py-3 px-3 font-medium">Current Price</th>
                <th className="text-right py-3 px-3 font-medium">Qty</th>
                <th className="text-right py-3 px-3 font-medium">Unrealized P&L</th>
                <th className="text-right py-3 px-3 font-medium">Stop Loss</th>
                <th className="text-right py-3 px-3 font-medium">Take Profit</th>
                <th className="text-center py-3 px-3 font-medium">Action</th>
              </tr>
            </thead>
            <tbody>
              {(positions ?? []).map((pos) => {
                const isRealtime = lastTick?.symbol === pos.symbol;
                const currentPrice = isRealtime ? lastTick!.price : pos.currentPrice;
                const pnlColor = pos.unrealizedPnl >= 0 ? "text-profit" : "text-loss";
                const pnlPercent =
                  pos.entryPrice > 0
                    ? ((currentPrice - pos.entryPrice) / pos.entryPrice) * 100 *
                      (pos.side === "Short" ? -1 : 1)
                    : 0;

                return (
                  <tr key={pos.symbol} className="table-row">
                    <td className="py-3 px-3">
                      <span className="font-medium text-white">{pos.symbol}</span>
                    </td>
                    <td className="py-3 px-3">
                      <StatusBadge
                        label={pos.side}
                        variant={pos.side === "Long" ? "success" : "danger"}
                      />
                    </td>
                    <td className="py-3 px-3 text-right font-mono text-gray-300">
                      {formatPrice(pos.entryPrice)}
                    </td>
                    <td className={`py-3 px-3 text-right font-mono ${isRealtime ? "text-yellow-300" : "text-gray-300"}`}>
                      {formatPrice(currentPrice)}
                    </td>
                    <td className="py-3 px-3 text-right font-mono text-gray-300">
                      {pos.quantity}
                    </td>
                    <td className={`py-3 px-3 text-right font-mono font-medium ${pnlColor}`}>
                      <div>{formatPnl(pos.unrealizedPnl)}</div>
                      <div className="text-xs opacity-75">
                        {pnlPercent >= 0 ? "+" : ""}
                        {pnlPercent.toFixed(2)}%
                      </div>
                    </td>
                    <td className="py-3 px-3 text-right font-mono text-gray-400">
                      {pos.stopLoss ? formatPrice(pos.stopLoss) : "-"}
                    </td>
                    <td className="py-3 px-3 text-right font-mono text-gray-400">
                      {pos.takeProfit ? formatPrice(pos.takeProfit) : "-"}
                    </td>
                    <td className="py-3 px-3 text-center">
                      <button
                        onClick={() => handleClose(pos.symbol)}
                        disabled={closePosition.isPending}
                        className="p-1.5 rounded-lg bg-loss/10 text-loss hover:bg-loss/20 transition-colors disabled:opacity-50"
                        title="Close Position"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                );
              })}
              {(!positions || positions.length === 0) && (
                <tr>
                  <td colSpan={9} className="py-12 text-center text-gray-500">
                    No open positions
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default PositionsPage;
