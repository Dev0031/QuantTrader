import React from "react";
import {
  DollarSign,
  TrendingUp,
  Briefcase,
  Brain,
  Zap,
} from "lucide-react";
import StatCard from "@/components/common/StatCard";
import EquityChart from "@/components/charts/EquityChart";
import TradeTable from "@/components/common/TradeTable";
import {
  usePortfolioOverview,
  useEquityCurve,
  useRecentTrades,
  useMarketPrices,
} from "@/api/hooks";
import type { MarketTick, Trade } from "@/api/types";

interface DashboardPageProps {
  lastTick: MarketTick | null;
  lastTrade: Trade | null;
}

const WATCHED_SYMBOLS = ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT"];

const DashboardPage: React.FC<DashboardPageProps> = ({ lastTick }) => {
  const { data: portfolio, isLoading: portfolioLoading } = usePortfolioOverview();
  const { data: equityCurve } = useEquityCurve(30);
  const { data: recentTrades } = useRecentTrades(10);
  const { data: marketPrices } = useMarketPrices(WATCHED_SYMBOLS);

  if (portfolioLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-accent" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title="Portfolio Equity"
          value={`$${(portfolio?.totalEquity ?? 0).toLocaleString(undefined, {
            minimumFractionDigits: 2,
          })}`}
          change={portfolio?.totalPnlPercent}
          changeLabel="all time"
          icon={DollarSign}
          iconColor="text-accent"
        />
        <StatCard
          title="Today's P&L"
          value={`${(portfolio?.todayPnl ?? 0) >= 0 ? "+" : ""}$${(
            portfolio?.todayPnl ?? 0
          ).toFixed(2)}`}
          change={portfolio?.todayPnlPercent}
          changeLabel="today"
          icon={TrendingUp}
          iconColor={(portfolio?.todayPnl ?? 0) >= 0 ? "text-profit" : "text-loss"}
          valueColor={(portfolio?.todayPnl ?? 0) >= 0 ? "text-profit" : "text-loss"}
        />
        <StatCard
          title="Open Positions"
          value={(portfolio?.openPositionsCount ?? 0).toString()}
          icon={Briefcase}
          iconColor="text-yellow-400"
        />
        <StatCard
          title="Active Strategies"
          value={(portfolio?.activeStrategiesCount ?? 0).toString()}
          icon={Brain}
          iconColor="text-purple-400"
        />
      </div>

      {/* Equity Curve */}
      <div className="card">
        <h2 className="card-header">Equity Curve (30 Days)</h2>
        {equityCurve && equityCurve.length > 0 ? (
          <EquityChart data={equityCurve} height={320} />
        ) : (
          <div className="h-80 flex items-center justify-center text-gray-500">
            No equity data available
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Recent Trades */}
        <div className="lg:col-span-2 card">
          <h2 className="card-header">Recent Trades</h2>
          <TradeTable trades={recentTrades ?? []} compact showStrategy={false} />
        </div>

        {/* Live Ticker */}
        <div className="card">
          <h2 className="card-header flex items-center gap-2">
            <Zap className="w-4 h-4 text-yellow-400" />
            Live Prices
          </h2>
          <div className="space-y-3 mt-3">
            {(marketPrices ?? []).map((tick) => (
              <div
                key={tick.symbol}
                className="flex items-center justify-between py-2 border-b border-panel-border last:border-0"
              >
                <span className="text-sm font-medium text-white">{tick.symbol}</span>
                <span
                  className={`text-sm font-mono font-medium ${
                    lastTick?.symbol === tick.symbol
                      ? "text-yellow-300"
                      : "text-gray-300"
                  }`}
                >
                  ${tick.price >= 1 ? tick.price.toFixed(2) : tick.price.toPrecision(6)}
                </span>
              </div>
            ))}
            {(!marketPrices || marketPrices.length === 0) &&
              WATCHED_SYMBOLS.map((sym) => (
                <div
                  key={sym}
                  className="flex items-center justify-between py-2 border-b border-panel-border last:border-0"
                >
                  <span className="text-sm font-medium text-white">{sym}</span>
                  <span className="text-sm font-mono text-gray-500">--</span>
                </div>
              ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;
