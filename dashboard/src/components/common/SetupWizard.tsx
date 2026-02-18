import React, { useState } from "react";
import {
  CheckCircle,
  ChevronRight,
  ExternalLink,
  Loader2,
  XCircle,
} from "lucide-react";
import {
  useApiProviders,
  useSaveExchangeSettings,
  useVerifyExchangeSettings,
} from "@/api/hooks";
import type { VerificationResult } from "@/api/types";

interface SetupWizardProps {
  onComplete: () => void;
}

const SetupWizard: React.FC<SetupWizardProps> = ({ onComplete }) => {
  const { data: providers } = useApiProviders();
  const saveSettings = useSaveExchangeSettings();
  const verifySettings = useVerifyExchangeSettings();

  const [step, setStep] = useState(0);
  const [apiKey, setApiKey] = useState("");
  const [apiSecret, setApiSecret] = useState("");
  const [testnet, setTestnet] = useState(true);
  const [verifyResult, setVerifyResult] = useState<VerificationResult | null>(null);
  const [saving, setSaving] = useState(false);

  // CryptoPanic
  const [cpKey, setCpKey] = useState("");

  const binanceConfigured = providers?.find((p) => p.name === "Binance")?.isConfigured;

  const handleSaveBinance = async () => {
    setSaving(true);
    try {
      await saveSettings.mutateAsync({
        exchange: "Binance",
        apiKey,
        apiSecret,
        useTestnet: testnet,
      });
    } catch {
      // handled by verify
    }
    setSaving(false);
  };

  const handleVerifyBinance = async () => {
    setVerifyResult(null);
    try {
      await handleSaveBinance();
      const result = await verifySettings.mutateAsync("Binance");
      setVerifyResult(result);
    } catch {
      setVerifyResult({ success: false, status: "Failed", message: "Connection failed", latencyMs: 0 });
    }
  };

  const handleSaveCryptoPanic = async () => {
    if (!cpKey) return;
    await saveSettings.mutateAsync({
      exchange: "CryptoPanic",
      apiKey: cpKey,
      apiSecret: null,
      useTestnet: false,
    });
  };

  // Step 0: Welcome
  if (step === 0) {
    return (
      <div className="fixed inset-0 z-50 bg-gray-900/95 flex items-center justify-center p-4">
        <div className="bg-gray-800 rounded-xl border border-gray-700 max-w-lg w-full p-8">
          <h1 className="text-2xl font-bold text-white mb-2">Welcome to QuantTrader</h1>
          <p className="text-gray-400 mb-6">
            Before you start trading, connect your exchange API keys. This takes about 2 minutes.
          </p>
          <div className="space-y-3 mb-8">
            <div className="flex items-center gap-3 text-sm">
              <span className="w-2 h-2 rounded-full bg-red-400" />
              <span className="text-gray-300">Binance - <span className="text-red-400">Required</span></span>
            </div>
            <div className="flex items-center gap-3 text-sm">
              <span className="w-2 h-2 rounded-full bg-gray-500" />
              <span className="text-gray-400">CoinGecko - Free, no key needed</span>
            </div>
            <div className="flex items-center gap-3 text-sm">
              <span className="w-2 h-2 rounded-full bg-gray-500" />
              <span className="text-gray-400">CryptoPanic - Optional, free key available</span>
            </div>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => setStep(1)}
              className="btn-primary flex items-center gap-2"
            >
              Get Started <ChevronRight className="w-4 h-4" />
            </button>
            <button
              onClick={onComplete}
              className="text-sm text-gray-500 hover:text-gray-300 px-4 py-2"
            >
              Skip for now
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Step 1: Binance
  if (step === 1) {
    return (
      <div className="fixed inset-0 z-50 bg-gray-900/95 flex items-center justify-center p-4">
        <div className="bg-gray-800 rounded-xl border border-gray-700 max-w-lg w-full p-8">
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs text-gray-500">Step 1 of 3</span>
          </div>
          <h2 className="text-xl font-bold text-white mb-1">Connect Binance</h2>
          <p className="text-sm text-red-400 mb-4">Required for trading</p>

          <ol className="text-xs text-gray-400 space-y-1.5 mb-5 list-decimal ml-4">
            <li>Go to Binance Testnet or production Binance</li>
            <li>Create API key with "Spot & Margin Trading" permission</li>
            <li>Copy API Key and API Secret</li>
            <li>Paste them below and verify</li>
          </ol>

          <a
            href="https://testnet.binance.vision/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-xs text-accent hover:text-accent/80 flex items-center gap-1 mb-4"
          >
            <ExternalLink className="w-3 h-3" />
            Open Binance Testnet
          </a>

          <div className="space-y-3 mb-4">
            <div>
              <label className="block text-xs text-gray-400 mb-1">API Key</label>
              <input
                type="text"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="Enter Binance API key"
                className="input-field w-full font-mono text-sm"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">API Secret</label>
              <input
                type="password"
                value={apiSecret}
                onChange={(e) => setApiSecret(e.target.value)}
                placeholder="Enter API secret"
                className="input-field w-full font-mono text-sm"
              />
            </div>
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="wiz-testnet"
                checked={testnet}
                onChange={(e) => setTestnet(e.target.checked)}
                className="rounded border-gray-600 bg-gray-700 text-accent focus:ring-accent"
              />
              <label htmlFor="wiz-testnet" className="text-sm text-gray-300">
                Use Testnet (recommended for testing)
              </label>
            </div>
          </div>

          <p className="text-xs text-gray-500 mb-4">
            Rate limit: 1200 API weight/min. Our system uses ~50 weight/min.
          </p>

          {verifyResult && (
            <div
              className={`p-3 rounded-lg mb-4 flex items-center gap-2 text-sm ${
                verifyResult.success
                  ? "bg-green-400/10 text-green-400"
                  : "bg-red-400/10 text-red-400"
              }`}
            >
              {verifyResult.success ? (
                <CheckCircle className="w-4 h-4" />
              ) : (
                <XCircle className="w-4 h-4" />
              )}
              {verifyResult.message}
              {verifyResult.success && (
                <span className="text-xs opacity-60 ml-auto">{verifyResult.latencyMs}ms</span>
              )}
            </div>
          )}

          <div className="flex gap-3">
            <button
              onClick={handleVerifyBinance}
              disabled={!apiKey || !apiSecret || saving || verifySettings.isPending}
              className="btn-primary flex items-center gap-2 disabled:opacity-50"
            >
              {verifySettings.isPending || saving ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <CheckCircle className="w-4 h-4" />
              )}
              Verify Connection
            </button>
            {verifyResult?.success && (
              <button onClick={() => setStep(2)} className="btn-primary flex items-center gap-2">
                Continue <ChevronRight className="w-4 h-4" />
              </button>
            )}
            {binanceConfigured && (
              <button
                onClick={() => setStep(2)}
                className="text-sm text-gray-400 hover:text-white px-4 py-2"
              >
                Already configured, skip
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }

  // Step 2: Optional APIs
  if (step === 2) {
    return (
      <div className="fixed inset-0 z-50 bg-gray-900/95 flex items-center justify-center p-4">
        <div className="bg-gray-800 rounded-xl border border-gray-700 max-w-lg w-full p-8">
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs text-gray-500">Step 2 of 3</span>
          </div>
          <h2 className="text-xl font-bold text-white mb-4">Optional Integrations</h2>

          <div className="space-y-4 mb-6">
            {/* CoinGecko */}
            <div className="p-4 rounded-lg border border-gray-700 bg-gray-800/50">
              <h3 className="text-sm font-medium text-white mb-1">CoinGecko</h3>
              <p className="text-xs text-gray-400 mb-2">
                Free tier works automatically -- no key needed! Pro key ($129/mo) increases limits to 500 req/min.
              </p>
              <span className="text-xs text-green-400">No setup needed</span>
            </div>

            {/* CryptoPanic */}
            <div className="p-4 rounded-lg border border-gray-700 bg-gray-800/50">
              <h3 className="text-sm font-medium text-white mb-1">CryptoPanic</h3>
              <p className="text-xs text-gray-400 mb-2">
                Enables news sentiment analysis. Free API key at cryptopanic.com. 5 req/min free tier.
              </p>
              <div className="flex gap-2 mt-2">
                <input
                  type="text"
                  value={cpKey}
                  onChange={(e) => setCpKey(e.target.value)}
                  placeholder="Auth token (optional)"
                  className="input-field flex-1 font-mono text-sm"
                />
                <button
                  onClick={handleSaveCryptoPanic}
                  disabled={!cpKey}
                  className="btn-primary text-sm disabled:opacity-50"
                >
                  Save
                </button>
              </div>
            </div>
          </div>

          <div className="flex gap-3">
            <button
              onClick={() => setStep(3)}
              className="btn-primary flex items-center gap-2"
            >
              Continue <ChevronRight className="w-4 h-4" />
            </button>
            <button
              onClick={() => setStep(1)}
              className="text-sm text-gray-400 hover:text-white px-4 py-2"
            >
              Back
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Step 3: Complete
  return (
    <div className="fixed inset-0 z-50 bg-gray-900/95 flex items-center justify-center p-4">
      <div className="bg-gray-800 rounded-xl border border-gray-700 max-w-lg w-full p-8 text-center">
        <div className="flex items-center gap-2 mb-1 justify-center">
          <span className="text-xs text-gray-500">Step 3 of 3</span>
        </div>
        <CheckCircle className="w-12 h-12 text-green-400 mx-auto mb-4" />
        <h2 className="text-xl font-bold text-white mb-2">Setup Complete</h2>
        <p className="text-sm text-gray-400 mb-6">
          Your API keys are configured. You can manage them anytime in Settings.
        </p>
        <div className="space-y-2 mb-6 text-left">
          {(providers ?? [])
            .filter((p) => p.isConfigured || !p.requiresApiKey)
            .map((p) => (
              <div key={p.name} className="flex items-center gap-2 text-sm">
                <CheckCircle className="w-4 h-4 text-green-400" />
                <span className="text-gray-300">{p.name}</span>
                <span className="text-xs text-gray-500 ml-auto">{p.status}</span>
              </div>
            ))}
        </div>
        <button onClick={onComplete} className="btn-primary">
          Start Trading
        </button>
      </div>
    </div>
  );
};

export default SetupWizard;
