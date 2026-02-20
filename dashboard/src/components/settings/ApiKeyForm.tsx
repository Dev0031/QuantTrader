interface Props {
  exchange: string;
  useTestnet: boolean;
  onUseTestnetChange: (v: boolean) => void;
  apiKey: string;
  onApiKeyChange: (v: string) => void;
  apiSecret: string;
  onApiSecretChange: (v: string) => void;
  onSave: () => void;
  isSaving: boolean;
}

export function ApiKeyForm({
  exchange,
  useTestnet,
  onUseTestnetChange,
  apiKey,
  onApiKeyChange,
  apiSecret,
  onApiSecretChange,
  onSave,
  isSaving,
}: Props) {
  return (
    <div className="space-y-4">
      {/* Testnet toggle */}
      <label className="flex cursor-pointer items-center justify-between rounded-lg border border-gray-700 bg-gray-800/50 px-4 py-3">
        <div>
          <p className="text-sm font-medium text-gray-200">Use Testnet</p>
          <p className="text-xs text-gray-500">
            Connect to {exchange} sandbox environment. Safe for development.
          </p>
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={useTestnet}
          onClick={() => onUseTestnetChange(!useTestnet)}
          className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 focus:ring-offset-gray-900 ${
            useTestnet ? 'bg-blue-600' : 'bg-gray-600'
          }`}
        >
          <span
            className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition-transform ${
              useTestnet ? 'translate-x-5' : 'translate-x-0'
            }`}
          />
        </button>
      </label>

      {/* API Key */}
      <div>
        <label htmlFor="api-key" className="mb-1.5 block text-sm font-medium text-gray-300">
          API Key
        </label>
        <input
          id="api-key"
          type="text"
          value={apiKey}
          onChange={(e) => onApiKeyChange(e.target.value)}
          placeholder={`Enter your ${exchange} API key`}
          spellCheck={false}
          autoComplete="off"
          className="w-full rounded-lg border border-gray-600 bg-gray-900 px-3 py-2 font-mono text-sm text-gray-100 placeholder-gray-600 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>

      {/* API Secret */}
      <div>
        <label htmlFor="api-secret" className="mb-1.5 block text-sm font-medium text-gray-300">
          API Secret
        </label>
        <input
          id="api-secret"
          type="password"
          value={apiSecret}
          onChange={(e) => onApiSecretChange(e.target.value)}
          placeholder={`Enter your ${exchange} API secret`}
          autoComplete="new-password"
          className="w-full rounded-lg border border-gray-600 bg-gray-900 px-3 py-2 font-mono text-sm text-gray-100 placeholder-gray-600 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>

      {/* Save button */}
      <button
        type="button"
        onClick={onSave}
        disabled={isSaving || !apiKey.trim()}
        className="w-full rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-500 disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 focus:ring-offset-gray-900"
      >
        {isSaving ? (
          <span className="flex items-center justify-center gap-2">
            <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            Saving…
          </span>
        ) : (
          `Save ${exchange} Settings`
        )}
      </button>
    </div>
  );
}
