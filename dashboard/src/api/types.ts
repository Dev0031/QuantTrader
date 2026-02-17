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
  realizedPnl: number | null;
  fee: number;
  strategyName: string;
  entryTime: string;
  exitTime: string | null;
  status: "Open" | "Closed" | "Cancelled";
}

export interface Position {
  id: string;
  symbol: string;
  side: "Long" | "Short";
  entryPrice: number;
  currentPrice: number;
  quantity: number;
  unrealizedPnl: number;
  unrealizedPnlPercent: number;
  stopLoss: number | null;
  takeProfit: number | null;
  strategyName: string;
  openedAt: string;
}

export interface PortfolioOverview {
  totalEquity: number;
  availableBalance: number;
  todayPnl: number;
  todayPnlPercent: number;
  totalPnl: number;
  totalPnlPercent: number;
  openPositionsCount: number;
  activeStrategiesCount: number;
  todayTradesCount: number;
  winRate: number;
}

export interface EquityPoint {
  date: string;
  equity: number;
  drawdown: number;
}

export interface RiskMetrics {
  currentDrawdown: number;
  currentDrawdownPercent: number;
  maxDrawdown: number;
  maxDrawdownPercent: number;
  sharpeRatio: number;
  sortinoRatio: number;
  winRate: number;
  profitFactor: number;
  averageRR: number;
  maxRiskPerTrade: number;
  maxDrawdownLimit: number;
  dailyLossLimit: number;
  killSwitchActive: boolean;
}

export interface RiskAlert {
  id: string;
  level: "Info" | "Warning" | "Critical";
  message: string;
  timestamp: string;
  acknowledged: boolean;
}

export interface StrategyStatus {
  id: string;
  name: string;
  enabled: boolean;
  symbol: string;
  winRate: number;
  totalTrades: number;
  totalPnl: number;
  todayPnl: number;
  maxDrawdown: number;
  lastTradeTime: string | null;
  description: string;
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
