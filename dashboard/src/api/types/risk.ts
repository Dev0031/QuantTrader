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
