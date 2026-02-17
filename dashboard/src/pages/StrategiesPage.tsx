import React from "react";
import { format } from "date-fns";
import { Power, TrendingUp, BarChart3, Target } from "lucide-react";
import { Switch } from "@headlessui/react";
import StatusBadge from "@/components/common/StatusBadge";
import { useStrategies, useToggleStrategy } from "@/api/hooks";

const StrategiesPage: React.FC = () => {
  const { data: strategies, isLoading } = useStrategies();
  const toggleStrategy = useToggleStrategy();

  const handleToggle = (name: string, currentEnabled: boolean) => {
    toggleStrategy.mutate({ name, enabled: !currentEnabled });
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
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Strategy Manager</h1>
        <span className="text-sm text-gray-400">
          {strategies?.filter((s) => s.enabled).length ?? 0} active /{" "}
          {strategies?.length ?? 0} total
        </span>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {(strategies ?? []).map((strategy) => {
          const pnlPositive = strategy.totalPnl >= 0;

          return (
            <div
              key={strategy.name}
              className={`card transition-all ${
                strategy.enabled
                  ? "border-panel-border"
                  : "border-gray-800 opacity-70"
              }`}
            >
              {/* Header */}
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div
                    className={`p-2 rounded-lg ${
                      strategy.enabled ? "bg-accent/10" : "bg-gray-800"
                    }`}
                  >
                    <Power
                      className={`w-5 h-5 ${
                        strategy.enabled ? "text-accent" : "text-gray-500"
                      }`}
                    />
                  </div>
                  <div>
                    <h3 className="text-base font-semibold text-white">
                      {strategy.name}
                    </h3>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <StatusBadge
                    label={strategy.enabled ? "Active" : "Disabled"}
                    variant={strategy.enabled ? "success" : "neutral"}
                    pulse={strategy.enabled}
                  />
                  <Switch
                    checked={strategy.enabled}
                    onChange={() => handleToggle(strategy.name, strategy.enabled)}
                    className={`${
                      strategy.enabled ? "bg-accent" : "bg-gray-700"
                    } relative inline-flex h-6 w-11 items-center rounded-full transition-colors`}
                  >
                    <span
                      className={`${
                        strategy.enabled ? "translate-x-6" : "translate-x-1"
                      } inline-block h-4 w-4 transform rounded-full bg-white transition-transform`}
                    />
                  </Switch>
                </div>
              </div>

              {/* Stats Grid */}
              <div className="grid grid-cols-2 gap-3">
                <div className="bg-gray-800/50 rounded-lg p-3">
                  <div className="flex items-center gap-1.5 mb-1">
                    <TrendingUp className="w-3.5 h-3.5 text-gray-500" />
                    <span className="text-xs text-gray-500">Total P&L</span>
                  </div>
                  <span
                    className={`text-lg font-mono font-semibold ${
                      pnlPositive ? "text-profit" : "text-loss"
                    }`}
                  >
                    {pnlPositive ? "+" : ""}${strategy.totalPnl.toFixed(2)}
                  </span>
                </div>
                <div className="bg-gray-800/50 rounded-lg p-3">
                  <div className="flex items-center gap-1.5 mb-1">
                    <Target className="w-3.5 h-3.5 text-gray-500" />
                    <span className="text-xs text-gray-500">Win Rate</span>
                  </div>
                  <span
                    className={`text-lg font-mono font-semibold ${
                      strategy.winRate >= 50 ? "text-profit" : "text-loss"
                    }`}
                  >
                    {strategy.winRate.toFixed(1)}%
                  </span>
                </div>
                <div className="bg-gray-800/50 rounded-lg p-3">
                  <div className="flex items-center gap-1.5 mb-1">
                    <BarChart3 className="w-3.5 h-3.5 text-gray-500" />
                    <span className="text-xs text-gray-500">Total Trades</span>
                  </div>
                  <span className="text-lg font-mono font-semibold text-white">
                    {strategy.totalTrades}
                  </span>
                </div>
                <div className="bg-gray-800/50 rounded-lg p-3">
                  <div className="flex items-center gap-1.5 mb-1">
                    <BarChart3 className="w-3.5 h-3.5 text-gray-500" />
                    <span className="text-xs text-gray-500">Winning</span>
                  </div>
                  <span className="text-lg font-mono font-semibold text-profit">
                    {strategy.winningTrades}
                  </span>
                </div>
              </div>

              {/* Footer */}
              <div className="mt-4 pt-3 border-t border-panel-border flex items-center justify-between">
                <span className="text-xs text-gray-500">
                  Sharpe: {strategy.sharpeRatio.toFixed(2)}
                </span>
                <span className="text-xs text-gray-500">
                  {strategy.lastTradeAt
                    ? `Last trade: ${format(new Date(strategy.lastTradeAt), "MMM dd HH:mm")}`
                    : "No trades yet"}
                </span>
              </div>
            </div>
          );
        })}

        {(!strategies || strategies.length === 0) && (
          <div className="col-span-2 card text-center py-12 text-gray-500">
            No strategies configured
          </div>
        )}
      </div>
    </div>
  );
};

export default StrategiesPage;
