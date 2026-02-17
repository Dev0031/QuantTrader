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
  ExchangeSettings,
  SaveExchangeSettingsRequest,
  ApiKeyStatus,
} from "./types";

// --- Portfolio ---

export function usePortfolioOverview() {
  return useQuery<PortfolioOverview>({
    queryKey: ["portfolio", "overview"],
    queryFn: async () => {
      const { data } = await apiClient.get("/dashboard/overview");
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
    retry: 1,
  });
}

export function useEquityCurve(days: number = 30) {
  return useQuery<EquityPoint[]>({
    queryKey: ["portfolio", "equity-curve", days],
    queryFn: async () => {
      const { data } = await apiClient.get("/trades/equity-curve");
      return data;
    },
    staleTime: 60 * 1000,
    refetchInterval: 60 * 1000,
    retry: 1,
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
    retry: 1,
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
    retry: 1,
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
    retry: 1,
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
    retry: 1,
  });
}

export function useClosePosition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (symbol: string) => {
      const { data } = await apiClient.post(`/positions/${symbol}/close`);
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
    retry: 1,
  });
}

export function useToggleStrategy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ name, enabled }: { name: string; enabled: boolean }) => {
      const { data } = await apiClient.put(`/strategies/${name}/toggle`, { enabled });
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
    retry: 1,
  });
}

export function useRiskAlerts() {
  return useQuery<RiskAlert[]>({
    queryKey: ["risk", "alerts"],
    queryFn: async () => {
      const { data } = await apiClient.get("/dashboard/alerts");
      return data;
    },
    staleTime: 10 * 1000,
    refetchInterval: 10 * 1000,
    retry: 1,
  });
}

export function useToggleKillSwitch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (activate: boolean) => {
      const endpoint = activate
        ? "/risk/killswitch/activate"
        : "/risk/killswitch/deactivate";
      const { data } = await apiClient.post(endpoint);
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
    mutationFn: async (settings: {
      maxRiskPerTradePercent: number;
      maxDrawdownPercent: number;
      maxDailyLoss: number;
    }) => {
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
      const { data } = await apiClient.get("/market/prices");
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
    enabled: symbols.length > 0,
    retry: 1,
  });
}

// --- Settings ---

export function useExchangeSettings() {
  return useQuery<ExchangeSettings[]>({
    queryKey: ["settings", "exchanges"],
    queryFn: async () => {
      const { data } = await apiClient.get("/settings/exchanges");
      return data;
    },
    staleTime: 30 * 1000,
    retry: 1,
  });
}

export function useSaveExchangeSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (settings: SaveExchangeSettingsRequest) => {
      const { data } = await apiClient.post("/settings/exchanges", settings);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings"] });
    },
  });
}

export function useDeleteExchangeSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (exchange: string) => {
      const { data } = await apiClient.delete(`/settings/exchanges/${exchange}`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings"] });
    },
  });
}

export function useVerifyExchangeSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (exchange: string) => {
      const { data } = await apiClient.post(`/settings/exchanges/${exchange}/verify`);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["settings"] });
    },
  });
}

export function useApiKeyStatus() {
  return useQuery<ApiKeyStatus[]>({
    queryKey: ["settings", "api-keys-status"],
    queryFn: async () => {
      const { data } = await apiClient.get("/settings/api-keys/status");
      return data;
    },
    staleTime: 30 * 1000,
    retry: 1,
  });
}
