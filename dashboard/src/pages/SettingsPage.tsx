import React, { useState } from "react";
import { Save, Key, Bell, Monitor, Database } from "lucide-react";

const SettingsPage: React.FC = () => {
  const [apiKey, setApiKey] = useState("");
  const [apiSecret, setApiSecret] = useState("");
  const [exchange, setExchange] = useState("binance");
  const [testnet, setTestnet] = useState(true);
  const [notifications, setNotifications] = useState({
    tradeExecuted: true,
    riskAlert: true,
    dailySummary: true,
    killSwitchTriggered: true,
  });

  return (
    <div className="space-y-6 max-w-3xl">
      <h1 className="text-xl font-semibold text-white">Settings</h1>

      {/* Exchange Connection */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <Key className="w-4 h-4" />
          Exchange Connection
        </h2>
        <div className="space-y-4 mt-4">
          <div>
            <label className="block text-xs text-gray-400 mb-1">Exchange</label>
            <select
              value={exchange}
              onChange={(e) => setExchange(e.target.value)}
              className="input-field w-full"
            >
              <option value="binance">Binance</option>
              <option value="bybit">Bybit</option>
              <option value="okx">OKX</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-400 mb-1">API Key</label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="Enter API key"
              className="input-field w-full"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-400 mb-1">API Secret</label>
            <input
              type="password"
              value={apiSecret}
              onChange={(e) => setApiSecret(e.target.value)}
              placeholder="Enter API secret"
              className="input-field w-full"
            />
          </div>
          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              id="testnet"
              checked={testnet}
              onChange={(e) => setTestnet(e.target.checked)}
              className="rounded border-gray-600 bg-gray-800 text-accent focus:ring-accent"
            />
            <label htmlFor="testnet" className="text-sm text-gray-300">
              Use Testnet (Paper Trading)
            </label>
          </div>
          <button className="btn-primary text-sm flex items-center gap-2">
            <Save className="w-4 h-4" />
            Save Connection
          </button>
        </div>
      </div>

      {/* Notifications */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <Bell className="w-4 h-4" />
          Notifications
        </h2>
        <div className="space-y-3 mt-4">
          {(
            [
              { key: "tradeExecuted", label: "Trade Executed" },
              { key: "riskAlert", label: "Risk Alerts" },
              { key: "dailySummary", label: "Daily Summary" },
              { key: "killSwitchTriggered", label: "Kill Switch Triggered" },
            ] as const
          ).map(({ key, label }) => (
            <div key={key} className="flex items-center justify-between py-2">
              <span className="text-sm text-gray-300">{label}</span>
              <input
                type="checkbox"
                checked={notifications[key]}
                onChange={(e) =>
                  setNotifications({ ...notifications, [key]: e.target.checked })
                }
                className="rounded border-gray-600 bg-gray-800 text-accent focus:ring-accent"
              />
            </div>
          ))}
        </div>
      </div>

      {/* Display */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <Monitor className="w-4 h-4" />
          Display
        </h2>
        <div className="space-y-4 mt-4">
          <div>
            <label className="block text-xs text-gray-400 mb-1">
              Refresh Interval (seconds)
            </label>
            <input
              type="number"
              defaultValue={10}
              min={1}
              max={60}
              className="input-field w-32"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-400 mb-1">
              Default Chart Timeframe
            </label>
            <select className="input-field w-full" defaultValue="1h">
              <option value="1m">1 Minute</option>
              <option value="5m">5 Minutes</option>
              <option value="15m">15 Minutes</option>
              <option value="1h">1 Hour</option>
              <option value="4h">4 Hours</option>
              <option value="1d">1 Day</option>
            </select>
          </div>
        </div>
      </div>

      {/* Data Management */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <Database className="w-4 h-4" />
          Data Management
        </h2>
        <div className="space-y-3 mt-4">
          <button className="btn-primary text-sm">Export Trade History (CSV)</button>
          <button className="btn-primary text-sm ml-3">Export Equity Curve (CSV)</button>
        </div>
      </div>
    </div>
  );
};

export default SettingsPage;
