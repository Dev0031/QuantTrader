import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from "./client";
import type {
  PortfolioOverview,
  Trade,
  Position,
  StrategyStatus,
  RiskMetrics,
  RiskAlert,
  EquityPoint,
  MarketTick,
  TradeFilter,
  PaginatedResult,
  TradeStats,
} from "./types";

// --- Portfolio ---

export function usePortfolioOverview() {
  return useQuery<PortfolioOverview>({
    queryKey: ["portfolio", "overview"],
    queryFn: async () => {
      const { data } = await apiClient.get("/portfolio/overview");
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
  });
}

export function useEquityCurve(days: number = 30) {
  return useQuery<EquityPoint[]>({
    queryKey: ["portfolio", "equity-curve", days],
    queryFn: async () => {
      const { data } = await apiClient.get(`/portfolio/equity-curve?days=${days}`);
      return data;
    },
    staleTime: 60 * 1000,
    refetchInterval: 60 * 1000,
  });
}

// --- Trades ---

export function useTrades(filters: TradeFilter) {
  return useQuery<PaginatedResult<Trade>>({
    queryKey: ["trades", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.symbol) params.set("symbol", filters.symbol);
      if (filters.side) params.set("side", filters.side);
      if (filters.strategyName) params.set("strategyName", filters.strategyName);
      if (filters.startDate) params.set("startDate", filters.startDate);
      if (filters.endDate) params.set("endDate", filters.endDate);
      params.set("page", filters.page.toString());
      params.set("pageSize", filters.pageSize.toString());
      const { data } = await apiClient.get(`/trades?${params.toString()}`);
      return data;
    },
    staleTime: 15 * 1000,
  });
}

export function useRecentTrades(count: number = 10) {
  return useQuery<Trade[]>({
    queryKey: ["trades", "recent", count],
    queryFn: async () => {
      const { data } = await apiClient.get(`/trades/recent?count=${count}`);
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
  });
}

export function useTradeStats(filters?: Partial<TradeFilter>) {
  return useQuery<TradeStats>({
    queryKey: ["trades", "stats", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters?.startDate) params.set("startDate", filters.startDate);
      if (filters?.endDate) params.set("endDate", filters.endDate);
      if (filters?.strategyName) params.set("strategyName", filters.strategyName);
      const { data } = await apiClient.get(`/trades/stats?${params.toString()}`);
      return data;
    },
    staleTime: 30 * 1000,
  });
}

// --- Positions ---

export function usePositions() {
  return useQuery<Position[]>({
    queryKey: ["positions"],
    queryFn: async () => {
      const { data } = await apiClient.get("/positions");
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
  });
}

export function useClosePosition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (positionId: string) => {
      const { data } = await apiClient.post(`/positions/${positionId}/close`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["positions"] });
      queryClient.invalidateQueries({ queryKey: ["portfolio"] });
    },
  });
}

// --- Strategies ---

export function useStrategies() {
  return useQuery<StrategyStatus[]>({
    queryKey: ["strategies"],
    queryFn: async () => {
      const { data } = await apiClient.get("/strategies");
      return data;
    },
    staleTime: 15 * 1000,
    refetchInterval: 15 * 1000,
  });
}

export function useToggleStrategy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, enabled }: { id: string; enabled: boolean }) => {
      const { data } = await apiClient.put(`/strategies/${id}/toggle`, { enabled });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["strategies"] });
    },
  });
}

// --- Risk ---

export function useRiskMetrics() {
  return useQuery<RiskMetrics>({
    queryKey: ["risk", "metrics"],
    queryFn: async () => {
      const { data } = await apiClient.get("/risk/metrics");
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
  });
}

export function useRiskAlerts() {
  return useQuery<RiskAlert[]>({
    queryKey: ["risk", "alerts"],
    queryFn: async () => {
      const { data } = await apiClient.get("/risk/alerts");
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
  });
}

export function useToggleKillSwitch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (active: boolean) => {
      const { data } = await apiClient.post("/risk/kill-switch", { active });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["risk"] });
      queryClient.invalidateQueries({ queryKey: ["strategies"] });
    },
  });
}

export function useUpdateRiskSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (settings: Partial<RiskMetrics>) => {
      const { data } = await apiClient.put("/risk/settings", settings);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["risk"] });
    },
  });
}

// --- Market Data ---

export function useMarketPrices(symbols: string[]) {
  return useQuery<MarketTick[]>({
    queryKey: ["market", "prices", symbols],
    queryFn: async () => {
      const { data } = await apiClient.get("/market/prices", {
        params: { symbols: symbols.join(",") },
      });
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
    enabled: symbols.length > 0,
  });
}
