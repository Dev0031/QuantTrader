import React from "react";
import { Wifi, WifiOff, TrendingUp, TrendingDown } from "lucide-react";
import type { PortfolioOverview } from "@/api/types";

interface HeaderProps {
  isConnected: boolean;
  portfolio: PortfolioOverview | undefined;
}

const Header: React.FC<HeaderProps> = ({ isConnected, portfolio }) => {
  const pnlPositive = (portfolio?.todayPnl ?? 0) >= 0;

  return (
    <header className="h-14 bg-panel border-b border-panel-border flex items-center justify-between px-6">
      {/* Left: Page info */}
      <div className="flex items-center gap-4">
        <span className="text-xs text-gray-500 font-mono">
          {new Date().toLocaleDateString("en-US", {
            weekday: "short",
            month: "short",
            day: "numeric",
          })}
        </span>
      </div>

      {/* Right: Key metrics + connection */}
      <div className="flex items-center gap-6">
        {/* Portfolio Equity */}
        {portfolio && (
          <>
            <div className="flex items-center gap-2">
              <span className="text-xs text-gray-500">Equity</span>
              <span className="text-sm font-mono font-semibold text-white">
                ${portfolio.totalEquity.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </span>
            </div>

            {/* Daily P&L */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">Day P&L</span>
              <div className={`flex items-center gap-1 ${pnlPositive ? "text-profit" : "text-loss"}`}>
                {pnlPositive ? (
                  <TrendingUp className="w-3.5 h-3.5" />
                ) : (
                  <TrendingDown className="w-3.5 h-3.5" />
                )}
                <span className="text-sm font-mono font-semibold">
                  {pnlPositive ? "+" : ""}${portfolio.todayPnl.toFixed(2)}
                </span>
                <span className="text-xs">
                  ({pnlPositive ? "+" : ""}{portfolio.todayPnlPercent.toFixed(2)}%)
                </span>
              </div>
            </div>
          </>
        )}

        {/* Connection Status */}
        <div
          className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${
            isConnected
              ? "bg-profit/10 text-profit"
              : "bg-loss/10 text-loss"
          }`}
        >
          {isConnected ? (
            <Wifi className="w-3.5 h-3.5" />
          ) : (
            <WifiOff className="w-3.5 h-3.5" />
          )}
          {isConnected ? "Live" : "Offline"}
        </div>
      </div>
    </header>
  );
};

export default Header;
