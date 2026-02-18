export interface MarketTick {
  symbol: string;
  price: number;
  volume: number;
  bid: number;
  ask: number;
  timestamp: string;
}

export interface Candle {
  time: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface Trade {
  id: string;
  symbol: string;
  side: "Buy" | "Sell";
  entryPrice: number;
  exitPrice: number | null;
  quantity: number;
  realizedPnl: number;
  commission: number;
  strategy: string;
  entryTime: string;
  exitTime: string | null;
  status: "Open" | "Closed" | "Cancelled";
}

export interface Position {
  symbol: string;
  side: string;
  entryPrice: number;
  currentPrice: number;
  quantity: number;
  unrealizedPnl: number;
  realizedPnl: number;
  stopLoss: number | null;
  takeProfit: number | null;
  openedAt: string;
}

export interface PortfolioOverview {
  totalEquity: number;
  availableBalance: number;
  totalUnrealizedPnl: number;
  totalRealizedPnl: number;
  drawdownPercent: number;
  activePositionCount: number;
  todayPnl: number;
  timestamp: string;
}

export interface EquityPoint {
  timestamp: string;
  equity: number;
}

export interface RiskMetrics {
  currentDrawdownPercent: number;
  maxDrawdownPercent: number;
  totalExposure: number;
  openPositionCount: number;
  maxOpenPositions: number;
  dailyLoss: number;
  maxDailyLoss: number;
  killSwitchActive: boolean;
  timestamp: string;
}

export interface RiskAlert {
  id: string;
  level: "Info" | "Warning" | "Critical";
  message: string;
  timestamp: string;
  acknowledged: boolean;
}

export interface StrategyStatus {
  name: string;
  enabled: boolean;
  totalTrades: number;
  winningTrades: number;
  totalPnl: number;
  winRate: number;
  sharpeRatio: number;
  lastTradeAt: string | null;
}

export interface TradeFilter {
  symbol?: string;
  side?: "Buy" | "Sell";
  strategyName?: string;
  startDate?: string;
  endDate?: string;
  page: number;
  pageSize: number;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface TradeStats {
  totalTrades: number;
  winRate: number;
  profitFactor: number;
  averageRR: number;
  totalPnl: number;
  averageWin: number;
  averageLoss: number;
  largestWin: number;
  largestLoss: number;
}

// --- Settings Types ---

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
