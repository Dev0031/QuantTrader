export interface ExchangeSettings {
  exchange: string;
  apiKeyMasked: string;
  hasSecret: boolean;
  useTestnet: boolean;
  status: string;
  lastVerified: string | null;
}

export interface SaveExchangeSettingsRequest {
  exchange: string;
  apiKey: string;
  apiSecret: string | null;
  useTestnet: boolean;
}

export interface ApiKeyStatus {
  name: string;
  description: string;
  isConfigured: boolean;
  maskedKey: string | null;
  status: string;
}

export interface ApiProviderInfo {
  name: string;
  requiresApiKey: boolean;
  requiresApiSecret: boolean;
  supportsTestnet: boolean;
  isRequired: boolean;
  description: string;
  features: string[];
  isConfigured: boolean;
  maskedKey: string | null;
  status: string;
  lastVerified: string | null;
}

export interface VerificationResult {
  success: boolean;
  status: string;
  message: string;
  latencyMs: number;
  geoRestricted?: boolean;
}

export interface IntegrationStatus {
  provider: string;
  status: "Connected" | "Disconnected" | "Error" | "NotConfigured";
  lastDataAt: string | null;
  lastError: string | null;
  dataPointsLast5Min: number;
}

export interface SetupStatus {
  isReady: boolean;
  missingRequired: string[];
  errors: { provider: string; message: string; steps: string[] }[];
  warnings: { provider: string; message: string }[];
}

// --- Phase P3: Step-by-step verification types ---

export interface VerificationStep {
  step: number;
  name: string;
  status: 'pending' | 'running' | 'success' | 'error' | 'skipped' | 'warning';
  message: string;
  durationMs: number;
}

export interface DetailedVerificationResult {
  success: boolean;
  status: string;
  message: string;
  latencyMs: number;
  geoRestricted?: boolean;
  steps: VerificationStep[];
  permissions?: {
    canReadMarketData: boolean;
    canReadAccount: boolean;
    canTrade: boolean;
    canWithdraw: boolean;
  };
}

export interface ConnectionHealth {
  exchange: string;
  isConnected: boolean;
  restLatencyMs: number;
  webSocketActive: boolean;
  lastTickAt: string | null;
  ticksPerMinute: number;
  requestWeightUsed: number;
  requestWeightLimit: number;
}
