import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  DollarSign,
  TrendingUp,
  Briefcase,
  Zap,
  Terminal,
  ExternalLink,
} from "lucide-react";
import StatCard from "@/components/common/StatCard";
import EquityChart from "@/components/charts/EquityChart";
import TradeTable from "@/components/common/TradeTable";
import PageGuide from "@/components/common/PageGuide";
import DataStatusBanner from "@/components/common/DataStatusBanner";
import {
  usePortfolioOverview,
  useEquityCurve,
  useRecentTrades,
  useMarketPrices,
  useSetupStatus,
  useSystemActivity,
} from "@/api/hooks";
import type { MarketTick, Trade, ActivityEntry, ActivityLevel } from "@/api/types";

interface DashboardPageProps {
  lastTick: MarketTick | null;
  lastTrade: Trade | null;
}

const WATCHED_SYMBOLS = ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT"];

const levelDot: Record<ActivityLevel, string> = {
  info: "bg-blue-400",
  success: "bg-emerald-400",
  warning: "bg-amber-400",
  error: "bg-red-400",
};

const levelText: Record<ActivityLevel, string> = {
  info: "text-blue-300",
  success: "text-emerald-300",
  warning: "text-amber-300",
  error: "text-red-300",
};

const DashboardPage: React.FC<DashboardPageProps> = ({ lastTick }) => {
  const { data: portfolio, isLoading: portfolioLoading } = usePortfolioOverview();
  const { data: equityCurve } = useEquityCurve(30);
  const { data: recentTrades } = useRecentTrades(10);
  const { data: marketPrices } = useMarketPrices(WATCHED_SYMBOLS);
  const { data: recentActivity } = useSystemActivity(6);
  const setupStatus = useSetupStatus();

  // Live prices that update in real-time via SignalR
  const [livePrices, setLivePrices] = useState<Record<string, number>>({});
  const [flashSymbol, setFlashSymbol] = useState<string | null>(null);

  useEffect(() => {
    if (!lastTick) return;
    setLivePrices((prev) => ({ ...prev, [lastTick.symbol]: lastTick.price }));
    setFlashSymbol(lastTick.symbol);
    const t = setTimeout(() => setFlashSymbol(null), 1200);
    return () => clearTimeout(t);
  }, [lastTick]);

  if (portfolioLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-accent" />
      </div>
    );
  }

  const todayPnl = portfolio?.todayPnl ?? 0;
  const totalEquity = portfolio?.totalEquity ?? 0;
  const todayPnlPercent = totalEquity > 0 ? (todayPnl / totalEquity) * 100 : 0;
  const totalPnlPercent = totalEquity > 0 ? ((portfolio?.totalRealizedPnl ?? 0) / totalEquity) * 100 : 0;

  return (
    <div className="space-y-6">
      <PageGuide pageId="dashboard">
        <p><strong>Portfolio Equity:</strong> Your total account value including all open positions. Updated every 15 seconds.</p>
        <p><strong>Today's P&L:</strong> Profit or loss from trades closed since midnight UTC.</p>
        <p><strong>Open Positions:</strong> Number of active trades currently held.</p>
        <p><strong>Drawdown:</strong> Current decline from your portfolio's peak value. Kill switch triggers if this exceeds your limit.</p>
        <p><strong>Equity Curve:</strong> Visual history of your portfolio value over 30 days. Flat line = no trading activity.</p>
        <p><strong>Live Prices:</strong> Real-time prices from Binance WebSocket. Yellow flash = price just updated. "--" = no data (check Binance connection in Settings).</p>
        <p><strong>Requires:</strong> Binance API key configured in Settings for trading data. Prices stream automatically from public WebSocket.</p>
      </PageGuide>

      {!setupStatus.isReady && (
        <DataStatusBanner
          type="error"
          title="Trading data unavailable"
          message="Configure your Binance API key in Settings to see portfolio, prices, and trades."
          action={{ label: "Go to Settings", href: "/settings" }}
          dismissKey="dashboard-binance"
        />
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title="Portfolio Equity"
          value={`$${totalEquity.toLocaleString(undefined, {
            minimumFractionDigits: 2,
          })}`}
          change={totalPnlPercent}
          changeLabel="all time"
          icon={DollarSign}
          iconColor="text-accent"
        />
        <StatCard
          title="Today's P&L"
          value={`${todayPnl >= 0 ? "+" : ""}$${todayPnl.toFixed(2)}`}
          change={todayPnlPercent}
          changeLabel="today"
          icon={TrendingUp}
          iconColor={todayPnl >= 0 ? "text-profit" : "text-loss"}
          valueColor={todayPnl >= 0 ? "text-profit" : "text-loss"}
        />
        <StatCard
          title="Open Positions"
          value={(portfolio?.activePositionCount ?? 0).toString()}
          icon={Briefcase}
          iconColor="text-yellow-400"
        />
        <StatCard
          title="Drawdown"
          value={`${(portfolio?.drawdownPercent ?? 0).toFixed(2)}%`}
          icon={TrendingUp}
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

        {/* Right column: prices + activity */}
        <div className="space-y-4">
          {/* Live Ticker */}
          <div className="card">
            <h2 className="card-header flex items-center gap-2">
              <Zap className={`w-4 h-4 ${flashSymbol ? "text-yellow-400" : "text-gray-500"}`} />
              Live Prices
              {flashSymbol && (
                <span className="ml-auto text-[10px] font-medium text-yellow-400 animate-pulse">
                  LIVE
                </span>
              )}
            </h2>
            <div className="space-y-0.5 mt-2">
              {(marketPrices ?? WATCHED_SYMBOLS.map((s) => ({ symbol: s, price: 0 }))).map((tick) => {
                const livePrice = livePrices[tick.symbol] ?? tick.price;
                const isFlashing = flashSymbol === tick.symbol;
                const hasData = livePrice > 0;
                return (
                  <div
                    key={tick.symbol}
                    className={`flex items-center justify-between px-2 py-2 rounded-lg transition-all duration-300 ${
                      isFlashing ? "bg-yellow-400/10" : "hover:bg-gray-800/50"
                    }`}
                  >
                    <span className="text-sm font-medium text-white">{tick.symbol}</span>
                    <span
                      className={`text-sm font-mono font-medium transition-colors ${
                        isFlashing ? "text-yellow-300" : hasData ? "text-gray-300" : "text-gray-600"
                      }`}
                    >
                      {hasData
                        ? `$${livePrice >= 1 ? livePrice.toFixed(2) : livePrice.toPrecision(6)}`
                        : "—"}
                    </span>
                  </div>
                );
              })}
            </div>
            {!flashSymbol && !lastTick && (
              <p className="text-[10px] text-gray-600 mt-2 text-center">
                No live data.{" "}
                <Link to="/system" className="text-accent-light hover:underline">
                  Start simulation
                </Link>{" "}
                to test.
              </p>
            )}
          </div>

          {/* System Activity Mini Feed */}
          <div className="card">
            <div className="flex items-center justify-between mb-3">
              <h2 className="card-header flex items-center gap-2 mb-0">
                <Terminal className="w-4 h-4 text-gray-500" />
                System Activity
              </h2>
              <Link
                to="/system"
                className="flex items-center gap-1 text-[10px] text-gray-500 hover:text-accent-light transition-colors"
              >
                View all <ExternalLink className="w-3 h-3" />
              </Link>
            </div>
            <div className="space-y-2">
              {(recentActivity ?? []).slice(0, 5).map((entry: ActivityEntry) => (
                <div key={entry.id} className="flex items-start gap-2">
                  <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 mt-1.5 ${levelDot[entry.level]}`} />
                  <div className="min-w-0">
                    <span className={`text-[10px] ${levelText[entry.level]} leading-relaxed line-clamp-2`}>
                      <span className="font-semibold text-gray-400">[{entry.service}]</span>{" "}
                      {entry.message}
                    </span>
                  </div>
                </div>
              ))}
              {(!recentActivity || recentActivity.length === 0) && (
                <div className="text-[11px] text-gray-600 text-center py-3">
                  No activity yet.{" "}
                  <Link to="/system" className="text-accent-light hover:underline">
                    Open System Monitor
                  </Link>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;
