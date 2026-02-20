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
