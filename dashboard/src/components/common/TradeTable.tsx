import React from "react";
import { format } from "date-fns";
import type { Trade } from "@/api/types";

interface TradeTableProps {
  trades: Trade[];
  compact?: boolean;
  showStrategy?: boolean;
}

const TradeTable: React.FC<TradeTableProps> = ({
  trades,
  compact = false,
  showStrategy = true,
}) => {
  const formatPrice = (price: number) => {
    return price >= 1 ? price.toFixed(2) : price.toPrecision(6);
  };

  const formatPnl = (pnl: number | null) => {
    if (pnl === null) return "-";
    const sign = pnl >= 0 ? "+" : "";
    return `${sign}$${pnl.toFixed(2)}`;
  };

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-gray-400 text-xs uppercase tracking-wider border-b border-panel-border">
            <th className="text-left py-3 px-3 font-medium">Time</th>
            <th className="text-left py-3 px-3 font-medium">Symbol</th>
            <th className="text-left py-3 px-3 font-medium">Side</th>
            <th className="text-right py-3 px-3 font-medium">Entry</th>
            {!compact && <th className="text-right py-3 px-3 font-medium">Exit</th>}
            <th className="text-right py-3 px-3 font-medium">Qty</th>
            <th className="text-right py-3 px-3 font-medium">P&L</th>
            {showStrategy && !compact && (
              <th className="text-left py-3 px-3 font-medium">Strategy</th>
            )}
          </tr>
        </thead>
        <tbody>
          {trades.map((trade) => (
            <tr key={trade.id} className="table-row">
              <td className="py-3 px-3 font-mono text-gray-300">
                {format(new Date(trade.entryTime), compact ? "HH:mm:ss" : "MMM dd HH:mm")}
              </td>
              <td className="py-3 px-3 font-medium text-white">{trade.symbol}</td>
              <td className="py-3 px-3">
                <span
                  className={`font-medium ${
                    trade.side === "Buy" ? "text-profit" : "text-loss"
                  }`}
                >
                  {trade.side}
                </span>
              </td>
              <td className="py-3 px-3 text-right font-mono text-gray-300">
                {formatPrice(trade.entryPrice)}
              </td>
              {!compact && (
                <td className="py-3 px-3 text-right font-mono text-gray-300">
                  {trade.exitPrice !== null ? formatPrice(trade.exitPrice) : "-"}
                </td>
              )}
              <td className="py-3 px-3 text-right font-mono text-gray-300">
                {trade.quantity}
              </td>
              <td
                className={`py-3 px-3 text-right font-mono font-medium ${
                  trade.realizedPnl !== null
                    ? trade.realizedPnl >= 0
                      ? "text-profit"
                      : "text-loss"
                    : "text-gray-500"
                }`}
              >
                {formatPnl(trade.realizedPnl)}
              </td>
              {showStrategy && !compact && (
                <td className="py-3 px-3 text-gray-400">{trade.strategy}</td>
              )}
            </tr>
          ))}
          {trades.length === 0 && (
            <tr>
              <td
                colSpan={compact ? 6 : 8}
                className="py-8 text-center text-gray-500"
              >
                No trades found
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
};

export default TradeTable;
