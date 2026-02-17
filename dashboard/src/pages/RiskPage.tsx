import React, { useState } from "react";
import { format } from "date-fns";
import {
  ShieldAlert,
  Octagon,
  TrendingDown,
  Target,
  BarChart3,
  AlertTriangle,
  Info,
  AlertCircle,
} from "lucide-react";
import StatusBadge from "@/components/common/StatusBadge";
import {
  useRiskMetrics,
  useRiskAlerts,
  useToggleKillSwitch,
  useUpdateRiskSettings,
} from "@/api/hooks";

const RiskPage: React.FC = () => {
  const { data: metrics, isLoading } = useRiskMetrics();
  const { data: alerts } = useRiskAlerts();
  const toggleKillSwitch = useToggleKillSwitch();
  const updateSettings = useUpdateRiskSettings();

  const [maxRisk, setMaxRisk] = useState("2");
  const [maxDrawdown, setMaxDrawdown] = useState("5");
  const [dailyLoss, setDailyLoss] = useState("500");

  const handleKillSwitch = () => {
    const newState = !metrics?.killSwitchActive;
    const msg = newState
      ? "ACTIVATE kill switch? This will stop ALL trading immediately."
      : "Deactivate kill switch and resume trading?";
    if (window.confirm(msg)) {
      toggleKillSwitch.mutate(newState);
    }
  };

  const handleSaveSettings = () => {
    updateSettings.mutate({
      maxRiskPerTradePercent: parseFloat(maxRisk),
      maxDrawdownPercent: parseFloat(maxDrawdown),
      maxDailyLoss: parseFloat(dailyLoss),
    });
  };

  const alertIcon = (level: string) => {
    switch (level) {
      case "Critical":
        return <AlertCircle className="w-4 h-4 text-loss" />;
      case "Warning":
        return <AlertTriangle className="w-4 h-4 text-yellow-400" />;
      default:
        return <Info className="w-4 h-4 text-accent" />;
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-accent" />
      </div>
    );
  }

  const drawdownPercent = metrics?.currentDrawdownPercent ?? 0;
  const maxDDLimit = metrics?.maxDrawdownPercent ?? 5;
  const drawdownRatio = maxDDLimit > 0 ? Math.min((drawdownPercent / maxDDLimit) * 100, 100) : 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Risk Controls</h1>
        <StatusBadge
          label={metrics?.killSwitchActive ? "KILL SWITCH ACTIVE" : "Trading Active"}
          variant={metrics?.killSwitchActive ? "danger" : "success"}
          pulse
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column: Drawdown + Kill Switch */}
        <div className="space-y-4">
          {/* Current Drawdown Gauge */}
          <div className="card">
            <h2 className="card-header flex items-center gap-2">
              <TrendingDown className="w-4 h-4" />
              Current Drawdown
            </h2>
            <div className="mt-4">
              <div className="flex items-end justify-between mb-2">
                <span
                  className={`text-3xl font-mono font-bold ${
                    drawdownPercent > maxDDLimit * 0.75
                      ? "text-loss"
                      : drawdownPercent > maxDDLimit * 0.5
                      ? "text-yellow-400"
                      : "text-white"
                  }`}
                >
                  {drawdownPercent.toFixed(2)}%
                </span>
                <span className="text-sm text-gray-500">
                  Limit: {maxDDLimit.toFixed(1)}%
                </span>
              </div>
              <div className="w-full bg-gray-800 rounded-full h-4 overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all duration-500 ${
                    drawdownRatio > 75
                      ? "bg-loss"
                      : drawdownRatio > 50
                      ? "bg-yellow-500"
                      : "bg-profit"
                  }`}
                  style={{ width: `${drawdownRatio}%` }}
                />
              </div>
              <div className="flex justify-between mt-1">
                <span className="text-xs text-gray-500">0%</span>
                <span className="text-xs text-gray-500">{maxDDLimit}%</span>
              </div>
            </div>
          </div>

          {/* Kill Switch */}
          <div
            className={`card border-2 ${
              metrics?.killSwitchActive
                ? "border-loss/50 bg-loss/5"
                : "border-panel-border"
            }`}
          >
            <h2 className="card-header flex items-center gap-2">
              <Octagon className="w-4 h-4" />
              Emergency Kill Switch
            </h2>
            <p className="text-sm text-gray-400 mt-2 mb-4">
              Immediately stops all trading activity and cancels pending orders.
            </p>
            <button
              onClick={handleKillSwitch}
              disabled={toggleKillSwitch.isPending}
              className={`w-full py-3 rounded-lg font-bold text-sm uppercase tracking-wider transition-all ${
                metrics?.killSwitchActive
                  ? "bg-gray-700 text-gray-300 hover:bg-gray-600"
                  : "bg-loss hover:bg-loss-dark text-white shadow-lg shadow-loss/25"
              }`}
            >
              {metrics?.killSwitchActive
                ? "Deactivate Kill Switch"
                : "ACTIVATE KILL SWITCH"}
            </button>
          </div>
        </div>

        {/* Middle Column: Risk Metrics */}
        <div className="space-y-4">
          <div className="card">
            <h2 className="card-header">Risk Metrics</h2>
            <div className="space-y-4 mt-3">
              {[
                {
                  label: "Total Exposure",
                  value: `$${(metrics?.totalExposure ?? 0).toFixed(2)}`,
                  icon: BarChart3,
                  good: true,
                },
                {
                  label: "Open Positions",
                  value: `${metrics?.openPositionCount ?? 0} / ${metrics?.maxOpenPositions ?? 5}`,
                  icon: Target,
                  good: (metrics?.openPositionCount ?? 0) < (metrics?.maxOpenPositions ?? 5),
                },
                {
                  label: "Daily Loss",
                  value: `$${Math.abs(metrics?.dailyLoss ?? 0).toFixed(2)}`,
                  icon: ShieldAlert,
                  good: Math.abs(metrics?.dailyLoss ?? 0) < (metrics?.maxDailyLoss ?? 1000),
                },
                {
                  label: "Max Daily Loss",
                  value: `$${(metrics?.maxDailyLoss ?? 0).toFixed(2)}`,
                  icon: ShieldAlert,
                  good: true,
                },
                {
                  label: "Max Drawdown Limit",
                  value: `${(metrics?.maxDrawdownPercent ?? 0).toFixed(1)}%`,
                  icon: TrendingDown,
                  good: true,
                },
              ].map(({ label, value, icon: Icon, good }) => (
                <div
                  key={label}
                  className="flex items-center justify-between py-2 border-b border-panel-border last:border-0"
                >
                  <div className="flex items-center gap-2">
                    <Icon className="w-4 h-4 text-gray-500" />
                    <span className="text-sm text-gray-300">{label}</span>
                  </div>
                  <span
                    className={`font-mono font-medium ${
                      good ? "text-profit" : "text-loss"
                    }`}
                  >
                    {value}
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* Risk Settings Form */}
          <div className="card">
            <h2 className="card-header">Risk Settings</h2>
            <div className="space-y-3 mt-3">
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Max Risk Per Trade (%)
                </label>
                <input
                  type="number"
                  step="0.1"
                  value={maxRisk}
                  onChange={(e) => setMaxRisk(e.target.value)}
                  className="input-field w-full"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Max Drawdown Limit (%)
                </label>
                <input
                  type="number"
                  step="0.5"
                  value={maxDrawdown}
                  onChange={(e) => setMaxDrawdown(e.target.value)}
                  className="input-field w-full"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-400 mb-1">
                  Max Daily Loss ($)
                </label>
                <input
                  type="number"
                  step="50"
                  value={dailyLoss}
                  onChange={(e) => setDailyLoss(e.target.value)}
                  className="input-field w-full"
                />
              </div>
              <button
                onClick={handleSaveSettings}
                disabled={updateSettings.isPending}
                className="btn-primary w-full text-sm mt-2"
              >
                {updateSettings.isPending ? "Saving..." : "Save Settings"}
              </button>
            </div>
          </div>
        </div>

        {/* Right Column: Alerts */}
        <div className="card">
          <h2 className="card-header flex items-center gap-2">
            <AlertTriangle className="w-4 h-4" />
            Recent Alerts
          </h2>
          <div className="space-y-2 mt-3 max-h-[500px] overflow-y-auto">
            {(alerts ?? []).map((alert) => (
              <div
                key={alert.id}
                className={`p-3 rounded-lg border ${
                  alert.level === "Critical"
                    ? "bg-loss/5 border-loss/20"
                    : alert.level === "Warning"
                    ? "bg-yellow-500/5 border-yellow-500/20"
                    : "bg-gray-800/50 border-panel-border"
                }`}
              >
                <div className="flex items-start gap-2">
                  {alertIcon(alert.level)}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm text-gray-200">{alert.message}</p>
                    <p className="text-xs text-gray-500 mt-1">
                      {format(new Date(alert.timestamp), "MMM dd HH:mm:ss")}
                    </p>
                  </div>
                  <StatusBadge
                    label={alert.level}
                    variant={
                      alert.level === "Critical"
                        ? "danger"
                        : alert.level === "Warning"
                        ? "warning"
                        : "info"
                    }
                  />
                </div>
              </div>
            ))}
            {(!alerts || alerts.length === 0) && (
              <div className="py-8 text-center text-gray-500 text-sm">
                No recent alerts
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default RiskPage;
