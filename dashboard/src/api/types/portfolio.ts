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
