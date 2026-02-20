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
  AlertTriangle,
  RefreshCw,
  Plus,
  Shield,
  BookOpen,
  ExternalLink,
  Wifi,
} from "lucide-react";
import {
  useSaveExchangeSettings,
  useDeleteExchangeSettings,
  useVerifyExchangeSettings,
  useExchangeSettings,
  useApiProviders,
  useIntegrationStatus,
} from "@/api/hooks";
import type { VerificationResult, ApiProviderInfo } from "@/api/types";
import { useToast } from "@/components/common/Toast";
import PageGuide from "@/components/common/PageGuide";

const RATE_LIMITS: Record<string, string> = {
  Binance: "1200 weight/min",
  Bybit: "120 req/min",
  OKX: "60 req/2s",
  CoinGecko: "10-30 req/min (free), 500/min (Pro)",
  CryptoPanic: "5 req/min",
};

const SettingsPage: React.FC = () => {
  const { data: providers, isLoading: providersLoading } = useApiProviders();
  useExchangeSettings(); // keep query warm for provider cards
  const { data: integrations } = useIntegrationStatus();
  const saveSettings = useSaveExchangeSettings();
  const deleteSettings = useDeleteExchangeSettings();
  const verifySettings = useVerifyExchangeSettings();
  const { toast } = useToast();

  const [showAddForm, setShowAddForm] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const [apiSecret, setApiSecret] = useState("");
  const [exchange, setExchange] = useState("Binance");
  const [testnet, setTestnet] = useState(true);
  const [verifyResult, setVerifyResult] = useState<VerificationResult | null>(null);

  const [notifications, setNotifications] = useState({
    tradeExecuted: true,
    riskAlert: true,
    dailySummary: true,
    killSwitchTriggered: true,
  });

  const selectedProvider = (providers ?? []).find(
    (p) => p.name.toLowerCase() === exchange.toLowerCase()
  );

  const handleSave = async () => {
    setVerifyResult(null);
    try {
      await saveSettings.mutateAsync({
        exchange,
        apiKey,
        apiSecret: selectedProvider?.requiresApiSecret ? apiSecret : null,
        useTestnet: testnet,
      });
      toast("success", `${exchange} API key saved successfully`);
      setApiKey("");
      setApiSecret("");
      setShowAddForm(false);
    } catch {
      toast("error", `Failed to save ${exchange} settings. Check required fields.`);
    }
  };

  const handleDelete = async (exchangeName: string) => {
    if (!window.confirm(`Remove ${exchangeName} API key? This cannot be undone.`)) return;
    try {
      await deleteSettings.mutateAsync(exchangeName);
      toast("success", `${exchangeName} API key removed`);
    } catch {
      toast("error", `Failed to remove ${exchangeName} key`);
    }
  };

  const handleVerify = async (exchangeName: string) => {
    try {
      const result = await verifySettings.mutateAsync(exchangeName);
      if (result.success) {
        toast("success", `${exchangeName} verified (${result.latencyMs}ms)`);
      } else if (result.geoRestricted) {
        toast("warning", `${exchangeName}: Key saved. Geo-restricted — switch to Testnet for local testing.`);
      } else {
        toast("error", `${exchangeName}: ${result.message}`);
      }
      setVerifyResult(result);
    } catch {
      toast("error", `${exchangeName} verification failed`);
    }
  };

  const statusDot = (status: string) => {
    switch (status) {
      case "Connected": return "bg-green-400";
      case "Verified": return "bg-green-400";
      case "Configured": return "bg-yellow-400";
      case "Disconnected": return "bg-red-400";
      default: return "bg-gray-500";
    }
  };

  return (
    <div className="space-y-6 max-w-4xl">
      <h1 className="text-xl font-semibold text-white">Settings</h1>

      {/* Page Guide */}
      <PageGuide pageId="settings">
        <p><strong>What this page does:</strong> Configure API keys that connect QuantTrader to exchanges and data providers. Keys are saved securely and used by all services.</p>
        <p><strong>How it works:</strong> Save a key, then verify the connection. The system uses the key for trading, price streaming, and data collection.</p>
        <p><strong>Required:</strong> Binance API key (for trading and live prices). Optional: CoinGecko (free, no key needed), CryptoPanic (news, free key).</p>
        <p><strong>Tip:</strong> Start with Binance Testnet keys for safe testing before using real funds.</p>
      </PageGuide>

      {/* Integration Health Monitor */}
      {integrations && integrations.length > 0 && (
        <div className="card">
          <h2 className="card-header flex items-center gap-2">
            <Wifi className="w-4 h-4" />
            Integration Health
          </h2>
          <div className="mt-3 space-y-2">
            {integrations.map((i) => (
              <div key={i.provider} className="flex items-center gap-3 py-1.5">
                <span className={`w-2 h-2 rounded-full ${statusDot(i.status)}`} />
                <span className="text-sm text-gray-300 w-24">{i.provider}</span>
                <span className="text-xs text-gray-500">
                  {i.status === "Connected"
                    ? `Connected${i.lastDataAt ? "" : ""}`
                    : i.status === "NotConfigured"
                    ? "Not configured"
                    : i.status}
                </span>
                {i.lastError && (
                  <span className="text-xs text-red-400 ml-auto">{i.lastError}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* API Providers Grid */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <h2 className="card-header flex items-center gap-2">
            <Shield className="w-4 h-4" />
            API Providers
          </h2>
          <button
            onClick={() => setShowAddForm(!showAddForm)}
            className="btn-primary text-sm flex items-center gap-1.5"
          >
            <Plus className="w-3.5 h-3.5" />
            Configure
          </button>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {(providers ?? []).map((provider) => (
            <ProviderCard
              key={provider.name}
              provider={provider}
              rateLimit={RATE_LIMITS[provider.name]}
              onConfigure={() => {
                setExchange(provider.name);
                setShowAddForm(true);
              }}
              onVerify={() => handleVerify(provider.name)}
              onDelete={() => handleDelete(provider.name)}
              verifying={verifySettings.isPending}
            />
          ))}
          {providersLoading && (
            <div className="col-span-full text-center py-6 text-gray-500 text-sm">
              Loading providers...
            </div>
          )}
        </div>
      </div>

      {/* Add/Edit Form */}
      {showAddForm && (
        <div className="card">
          <h3 className="text-sm font-medium text-white mb-4">
            Configure {exchange} Connection
          </h3>

          {/* Feature impact */}
          {selectedProvider && (
            <div className="p-3 bg-accent/5 border border-accent/20 rounded-lg mb-4">
              <p className="text-xs text-accent mb-1.5">Adding this key enables:</p>
              <ul className="text-xs text-gray-400 space-y-1">
                {selectedProvider.features.map((f) => (
                  <li key={f} className="flex items-center gap-1.5">
                    <CheckCircle className="w-3 h-3 text-accent/60" />
                    {f}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="space-y-4">
            <div>
              <label className="block text-xs text-gray-400 mb-1">Provider</label>
              <select
                value={exchange}
                onChange={(e) => {
                  setExchange(e.target.value);
                  setApiKey("");
                  setApiSecret("");
                  setVerifyResult(null);
                }}
                className="input-field w-full"
              >
                {(providers ?? []).map((p) => (
                  <option key={p.name} value={p.name}>{p.name}</option>
                ))}
              </select>
            </div>

            {selectedProvider?.requiresApiKey && (
              <div>
                <label className="block text-xs text-gray-400 mb-1">API Key</label>
                <input
                  type="text"
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder={`Enter ${exchange} API key`}
                  className="input-field w-full font-mono"
                />
              </div>
            )}

            {selectedProvider?.requiresApiSecret && (
              <div>
                <label className="block text-xs text-gray-400 mb-1">API Secret</label>
                <input
                  type="password"
                  value={apiSecret}
                  onChange={(e) => setApiSecret(e.target.value)}
                  placeholder={`Enter ${exchange} API secret`}
                  className="input-field w-full font-mono"
                />
              </div>
            )}

            {!selectedProvider?.requiresApiKey && (
              <p className="text-xs text-green-400 bg-green-400/5 border border-green-400/20 rounded-lg p-3">
                {exchange} works without an API key (free tier). Saving enables tracking.
              </p>
            )}

            {selectedProvider?.supportsTestnet && (
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
            )}

            {/* Verify result inline */}
            {verifyResult && (
              <div
                className={`p-3 rounded-lg flex items-start gap-2 text-sm ${
                  verifyResult.success
                    ? "bg-green-400/10 text-green-400"
                    : verifyResult.geoRestricted
                    ? "bg-amber-400/10 text-amber-400"
                    : "bg-red-400/10 text-red-400"
                }`}
              >
                {verifyResult.success ? (
                  <CheckCircle className="w-4 h-4 mt-0.5 shrink-0" />
                ) : verifyResult.geoRestricted ? (
                  <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
                ) : (
                  <XCircle className="w-4 h-4 mt-0.5 shrink-0" />
                )}
                <span className="flex-1">{verifyResult.message}</span>
                {verifyResult.latencyMs > 0 && (
                  <span className="text-xs opacity-60 shrink-0">{verifyResult.latencyMs}ms</span>
                )}
              </div>
            )}

            <div className="flex gap-3">
              <button
                onClick={handleSave}
                disabled={
                  saveSettings.isPending ||
                  (selectedProvider?.requiresApiKey && !apiKey) ||
                  (selectedProvider?.requiresApiSecret && !apiSecret)
                }
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
                  setVerifyResult(null);
                }}
                className="text-sm text-gray-400 hover:text-white transition-colors px-4 py-2"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Setup Guides */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <BookOpen className="w-4 h-4" />
          Setup Guides
        </h2>
        <div className="space-y-4 mt-4">
          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">1</span>
              Binance (Required for Trading)
            </h3>
            <ol className="text-xs text-gray-400 space-y-1.5 ml-8 list-decimal">
              <li>Go to Binance &gt; Account &gt; API Management (or use Testnet)</li>
              <li>Create API key with "Spot & Margin Trading" permission</li>
              <li>Restrict IP access to your server IP for security</li>
              <li>Copy both the API Key and Secret Key</li>
              <li>Enable Testnet toggle for safe testing</li>
            </ol>
            <div className="flex gap-3 mt-2">
              <a href="https://testnet.binance.vision/" target="_blank" rel="noopener noreferrer"
                className="text-xs text-accent hover:text-accent/80 flex items-center gap-1">
                <ExternalLink className="w-3 h-3" /> Binance Testnet
              </a>
            </div>
          </div>

          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">2</span>
              CoinGecko (Free - No Key Needed)
            </h3>
            <p className="text-xs text-gray-400 ml-8">
              Free tier works automatically with 10-30 req/min (2.5s built-in throttle).
              Pro key ($129/mo) increases to 500 req/min. No configuration needed for basic usage.
            </p>
          </div>

          <div className="p-4 bg-gray-800/50 rounded-lg border border-panel-border">
            <h3 className="text-sm font-medium text-white mb-2 flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-accent/20 text-accent text-xs flex items-center justify-center font-bold">3</span>
              CryptoPanic (Free Key Available)
            </h3>
            <ol className="text-xs text-gray-400 space-y-1.5 ml-8 list-decimal">
              <li>Register at cryptopanic.com/developers/api</li>
              <li>Copy your auth token from the settings page</li>
              <li>Paste it as the API Key (no secret needed)</li>
            </ol>
            <p className="text-xs text-gray-500 ml-8 mt-1">Free tier: 5 requests/minute.</p>
          </div>

          <div className="p-3 bg-yellow-400/5 border border-yellow-400/20 rounded-lg">
            <p className="text-xs text-yellow-400 flex items-start gap-2">
              <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
              <span>
                <strong>Security:</strong> API keys are stored encrypted. Never share secrets.
                Always use IP restrictions and minimal permissions.
              </span>
            </p>
          </div>
        </div>
      </div>

      {/* Troubleshooting */}
      <div className="card">
        <h2 className="card-header flex items-center gap-2">
          <AlertCircle className="w-4 h-4" />
          Troubleshooting
        </h2>
        <div className="space-y-3 mt-4 text-xs text-gray-400">
          <div className="p-3 bg-gray-800/50 rounded-lg border border-panel-border">
            <p className="text-white text-sm mb-1">Dashboard shows $0?</p>
            <p>Check that Binance API key is configured and verified. The portfolio requires active price data from Binance WebSocket.</p>
          </div>
          <div className="p-3 bg-gray-800/50 rounded-lg border border-panel-border">
            <p className="text-white text-sm mb-1">No live prices?</p>
            <p>Binance WebSocket streams public data automatically. If prices show "--", check integration status above. The DataIngestion service must be running.</p>
          </div>
          <div className="p-3 bg-gray-800/50 rounded-lg border border-panel-border">
            <p className="text-white text-sm mb-1">News not loading?</p>
            <p>CryptoPanic requires a free API key. Get one at cryptopanic.com/developers/api and add it above.</p>
          </div>
          <div className="p-3 bg-gray-800/50 rounded-lg border border-panel-border">
            <p className="text-white text-sm mb-1">Verification says "timed out"?</p>
            <p>Check your network connection. If using Testnet, the service may be temporarily down -- check status.binance.com.</p>
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
            <label className="block text-xs text-gray-400 mb-1">Refresh Interval (seconds)</label>
            <input type="number" defaultValue={10} min={1} max={60} className="input-field w-32" />
          </div>
          <div>
            <label className="block text-xs text-gray-400 mb-1">Default Chart Timeframe</label>
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

// --- Provider Card Sub-component ---

interface ProviderCardProps {
  provider: ApiProviderInfo;
  rateLimit?: string;
  onConfigure: () => void;
  onVerify: () => void;
  onDelete: () => void;
  verifying: boolean;
}

const ProviderCard: React.FC<ProviderCardProps> = ({
  provider,
  rateLimit,
  onConfigure,
  onVerify,
  onDelete,
  verifying,
}) => {
  const statusColor =
    provider.status === "Verified"
      ? "text-green-400 bg-green-400/10 border-green-400/20"
      : provider.status === "Configured"
      ? "text-yellow-400 bg-yellow-400/10 border-yellow-400/20"
      : "text-gray-500 bg-gray-800 border-gray-700";

  return (
    <div className={`p-4 rounded-lg border ${statusColor}`}>
      <div className="flex items-center justify-between mb-2">
        <span className="font-medium text-sm text-white">{provider.name}</span>
        <div className="flex items-center gap-1.5">
          {provider.isRequired ? (
            <span className="text-[10px] px-1.5 py-0.5 rounded bg-red-400/10 text-red-400 border border-red-400/20">
              Required
            </span>
          ) : (
            <span className="text-[10px] px-1.5 py-0.5 rounded bg-gray-700 text-gray-400 border border-gray-600">
              Optional
            </span>
          )}
        </div>
      </div>

      <p className="text-xs text-gray-400 mb-2">{provider.description}</p>

      {provider.maskedKey && (
        <div className="flex items-center gap-1.5 mb-2">
          <Key className="w-3 h-3 text-gray-500" />
          <code className="text-xs font-mono text-gray-400">{provider.maskedKey}</code>
        </div>
      )}

      <ul className="text-[10px] text-gray-500 space-y-0.5 mb-3">
        {provider.features.slice(0, 3).map((f) => (
          <li key={f} className="flex items-center gap-1">
            <CheckCircle className="w-2.5 h-2.5 text-gray-600" />
            {f}
          </li>
        ))}
      </ul>

      {rateLimit && (
        <p className="text-[10px] text-gray-500 mb-2">Rate limit: {rateLimit}</p>
      )}

      {provider.lastVerified && (
        <p className="text-[10px] text-gray-500 mb-2">
          Verified: {new Date(provider.lastVerified).toLocaleString()}
        </p>
      )}

      <div className="flex items-center gap-2">
        {!provider.isConfigured ? (
          <button onClick={onConfigure} className="text-xs text-accent hover:text-accent/80 flex items-center gap-1">
            <Plus className="w-3 h-3" /> Configure
          </button>
        ) : (
          <>
            <button
              onClick={onVerify}
              disabled={verifying}
              className="p-1.5 rounded-lg hover:bg-gray-700 text-gray-400 hover:text-white transition-colors"
              title="Verify"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${verifying ? "animate-spin" : ""}`} />
            </button>
            <button
              onClick={onDelete}
              className="p-1.5 rounded-lg hover:bg-red-500/10 text-gray-400 hover:text-red-400 transition-colors"
              title="Remove"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </>
        )}
      </div>
    </div>
  );
};

export default SettingsPage;
