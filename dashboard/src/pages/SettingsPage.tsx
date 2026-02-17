import React, { useState } from "react";
import {
  Save,
  Key,
  Bell,
  Monitor,
  Database,
  Trash2,
  CheckCircle,
  XCircle,
  AlertCircle,
  RefreshCw,
  Plus,
  Shield,
  BookOpen,
  ExternalLink,
} from "lucide-react";
import {
  useExchangeSettings,
  useSaveExchangeSettings,
  useDeleteExchangeSettings,
  useVerifyExchangeSettings,
  useApiKeyStatus,
} from "@/api/hooks";

const SettingsPage: React.FC = () => {
  const { data: exchanges, isLoading: exchangesLoading } = useExchangeSettings();
  const { data: apiKeyStatuses } = useApiKeyStatus();
  const saveSettings = useSaveExchangeSettings();
  const deleteSettings = useDeleteExchangeSettings();
  const verifySettings = useVerifyExchangeSettings();

  const [showAddForm, setShowAddForm] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const [apiSecret, setApiSecret] = useState("");
  const [exchange, setExchange] = useState("Binance");
  const [testnet, setTestnet] = useState(true);
  const [saveMessage, setSaveMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  const [notifications, setNotifications] = useState({
    tradeExecuted: true,
    riskAlert: true,
    dailySummary: true,
    killSwitchTriggered: true,
  });

  const handleSave = async () => {
    setSaveMessage(null);
    try {
      await saveSettings.mutateAsync({
        exchange,
        apiKey,
        apiSecret,
        useTestnet: testnet,
      });
      setSaveMessage({ type: "success", text: `${exchange} API key saved successfully` });
      setApiKey("");
      setApiSecret("");
      setShowAddForm(false);
    } catch {
      setSaveMessage({ type: "error", text: "Failed to save settings. Please try again." });
    }
  };

  const handleDelete = async (exchangeName: string) => {
    if (!window.confirm(`Remove ${exchangeName} API key? This cannot be undone.`)) return;
    try {
      await deleteSettings.mutateAsync(exchangeName);
      setSaveMessage({ type: "success", text: `${exchangeName} API key removed` });
    } catch {
      setSaveMessage({ type: "error", text: `Failed to remove ${exchangeName} key` });
    }
  };

  const handleVerify = async (exchangeName: string) => {
    try {
      await verifySettings.mutateAsync(exchangeName);
      setSaveMessage({ type: "success", text: `${exchangeName} connection verified` });
    } catch {
      setSaveMessage({ type: "error", text: `${exchangeName} verification failed` });
    }
  };

  const statusIcon = (status: string) => {
    switch (status) {
      case "Verified":
        return <CheckCircle className="w-4 h-4 text-green-400" />;
      case "Configured":
        return <AlertCircle className="w-4 h-4 text-yellow-400" />;
      default:
        return <XCircle className="w-4 h-4 text-gray-500" />;
    }
  };

  const statusColor = (status: string) => {
    switch (status) {
      case "Verified":
        return "text-green-400 bg-green-400/10 border-green-400/20";
      case "Configured":
        return "text-yellow-400 bg-yellow-400/10 border-yellow-400/20";
      default:
        return "text-gray-500 bg-gray-800 border-gray-700";
    }
  };

  return (
    <div className="space-y-6 max-w-4xl">
      <h1 className="text-xl font-semibold text-white">Settings</h1>

      {/* Status Message */}
      {saveMessage && (
        <div
          className={`p-3 rounded-lg text-sm flex items-center gap-2 ${
            saveMessage.type === "success"
              ? "bg-green-400/10 text-green-400 border border-green-400/20"
              : "bg-red-400/10 text-red-400 border border-red-400/20"
          }`}
        >
          {saveMessage.type === "success" ? (
            <CheckCircle className="w-4 h-4" />
          ) : (
            <XCircle className="w-4 h-4" />
          )}
          {saveMessage.text}
          <button
            onClick={() => setSaveMessage(null)}
            className="ml-auto text-current opacity-50 hover:opacity-100"
          >
            <XCircle className="w-3 h-3" />
          </button>
        </div>
      )}

      {/* API Key Status Grid */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <Shield className="w-4 h-4" />
          API Key Status Overview
        </h2>
        <p className="text-xs text-gray-500 mt-1 mb-4">
          Overview of all required API keys and their current configuration status.
        </p>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {(apiKeyStatuses ?? []).map((key) => (
            <div
              key={key.name}
              className={`p-4 rounded-lg border ${statusColor(key.status)}`}
            >
              <div className="flex items-center justify-between mb-2">
                <span className="font-medium text-sm text-white">{key.name}</span>
                <div className="flex items-center gap-1.5">
                  {statusIcon(key.status)}
                  <span className="text-xs">{key.status}</span>
                </div>
              </div>
              <p className="text-xs text-gray-400 mb-2">{key.description}</p>
              {key.maskedKey && (
                <div className="flex items-center gap-1.5">
                  <Key className="w-3 h-3 text-gray-500" />
                  <code className="text-xs font-mono text-gray-400">{key.maskedKey}</code>
                </div>
              )}
              {!key.isConfigured && (
                <button
                  onClick={() => {
                    setExchange(key.name);
                    setShowAddForm(true);
                    window.scrollTo({ top: 0, behavior: "smooth" });
                  }}
                  className="mt-2 text-xs text-accent hover:text-accent/80 flex items-center gap-1"
                >
                  <Plus className="w-3 h-3" />
                  Configure
                </button>
              )}
            </div>
          ))}
          {(!apiKeyStatuses || apiKeyStatuses.length === 0) && !exchangesLoading && (
            <div className="col-span-full text-center py-6 text-gray-500 text-sm">
              Loading API key status...
            </div>
          )}
        </div>
      </div>

      {/* Configured Exchange Connections */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <h2 className="card-header flex items-center gap-2">
            <Key className="w-4 h-4" />
            Exchange Connections
          </h2>
          <button
            onClick={() => setShowAddForm(!showAddForm)}
            className="btn-primary text-sm flex items-center gap-1.5"
          >
            <Plus className="w-3.5 h-3.5" />
            Add Key
          </button>
        </div>

        {/* Existing connections */}
        {exchangesLoading ? (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-accent" />
          </div>
        ) : (exchanges ?? []).length > 0 ? (
          <div className="space-y-3">
            {(exchanges ?? []).map((ex) => (
              <div
                key={ex.exchange}
                className="flex items-center justify-between p-4 bg-gray-800/50 rounded-lg border border-panel-border"
              >
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-2">
                    {statusIcon(ex.status)}
                    <span className="font-medium text-white">{ex.exchange}</span>
                  </div>
                  <code className="text-xs font-mono text-gray-400 bg-gray-900 px-2 py-1 rounded">
                    {ex.apiKeyMasked}
                  </code>
                  {ex.useTestnet && (
                    <span className="text-xs px-2 py-0.5 rounded bg-yellow-400/10 text-yellow-400 border border-yellow-400/20">
                      Testnet
                    </span>
                  )}
                  <span className={`text-xs px-2 py-0.5 rounded border ${statusColor(ex.status)}`}>
                    {ex.status}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => handleVerify(ex.exchange)}
                    disabled={verifySettings.isPending}
                    className="p-2 rounded-lg hover:bg-gray-700 text-gray-400 hover:text-white transition-colors"
                    title="Verify Connection"
                  >
                    <RefreshCw className={`w-4 h-4 ${verifySettings.isPending ? "animate-spin" : ""}`} />
                  </button>
                  <button
                    onClick={() => handleDelete(ex.exchange)}
                    disabled={deleteSettings.isPending}
                    className="p-2 rounded-lg hover:bg-red-500/10 text-gray-400 hover:text-red-400 transition-colors"
                    title="Remove Key"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="text-center py-8 text-gray-500 text-sm">
            No exchange connections configured. Click "Add Key" to get started.
          </div>
        )}

        {/* Add new connection form */}
        {showAddForm && (
          <div className="mt-4 pt-4 border-t border-panel-border">
            <h3 className="text-sm font-medium text-white mb-3">Add Exchange Connection</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-xs text-gray-400 mb-1">Exchange</label>
                <select
                  value={exchange}
                  onChange={(e) => setExchange(e.target.value)}
                  className="input-field w-full"
                >
                  <option value="Binance">Binance</option>
                  <option value="Bybit">Bybit</option>
                  <option value="OKX">OKX</option>
                  <option value="CoinGecko">CoinGecko</option>
                  <option value="CryptoPanic">CryptoPanic</option>
                </select>
              </div>
              <div>
                <label className="block text-xs text-gray-400 mb-1">API Key</label>
                <input
                  type="text"
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder="Enter API key"
                  className="input-field w-full font-mono"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-400 mb-1">API Secret</label>
                <input
                  type="password"
                  value={apiSecret}
                  onChange={(e) => setApiSecret(e.target.value)}
                  placeholder="Enter API secret"
                  className="input-field w-full font-mono"
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
              <div className="flex gap-3">
                <button
                  onClick={handleSave}
                  disabled={saveSettings.isPending || !apiKey || !apiSecret}
                  className="btn-primary text-sm flex items-center gap-2 disabled:opacity-50"
                >
                  <Save className="w-4 h-4" />
                  {saveSettings.isPending ? "Saving..." : "Save Connection"}
                </button>
                <button
                  onClick={() => {
                    setShowAddForm(false);
                    setApiKey("");
                    setApiSecret("");
                  }}
                  className="text-sm text-gray-400 hover:text-white transition-colors px-4 py-2"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Setup Guide */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <BookOpen className="w-4 h-4" />
          API Key Setup Guide
        </h2>
        <div className="space-y-4 mt-4">
          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">1</span>
              Binance API Key (Required for Trading)
            </h3>
            <ol className="text-xs text-gray-400 space-y-1.5 ml-8 list-decimal">
              <li>Go to Binance &gt; Account &gt; API Management</li>
              <li>Create a new API key with a label (e.g., "QuantTrader")</li>
              <li>Enable "Spot & Margin Trading" permission</li>
              <li>Restrict IP access to your server IP for security</li>
              <li>Copy the API Key and Secret Key</li>
              <li>For testing, use Binance Testnet at testnet.binance.vision</li>
            </ol>
            <a
              href="https://testnet.binance.vision/"
              target="_blank"
              rel="noopener noreferrer"
              className="mt-2 text-xs text-accent hover:text-accent/80 flex items-center gap-1 inline-flex"
            >
              <ExternalLink className="w-3 h-3" />
              Binance Testnet
            </a>
          </div>

          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">2</span>
              CoinGecko API Key (Market Data)
            </h3>
            <ol className="text-xs text-gray-400 space-y-1.5 ml-8 list-decimal">
              <li>Sign up at coingecko.com/en/api</li>
              <li>Free tier provides 10,000 requests/month</li>
              <li>Copy your API key from the dashboard</li>
            </ol>
          </div>

          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">3</span>
              CryptoPanic API Key (News/Sentiment)
            </h3>
            <ol className="text-xs text-gray-400 space-y-1.5 ml-8 list-decimal">
              <li>Register at cryptopanic.com/developers/api</li>
              <li>Free tier available for basic news feed</li>
              <li>Copy your auth token from the settings page</li>
            </ol>
          </div>

          <div className="p-3 bg-yellow-400/5 border border-yellow-400/20 rounded-lg">
            <p className="text-xs text-yellow-400 flex items-start gap-2">
              <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
              <span>
                <strong>Security Note:</strong> API keys are stored encrypted in the system.
                Never share your API secret with anyone. Always use IP restrictions and
                minimal permissions. Start with Testnet keys for development and testing.
              </span>
            </p>
          </div>
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
