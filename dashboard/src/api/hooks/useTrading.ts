import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from "../client";
import type {
  Trade,
  Position,
  StrategyStatus,
  MarketTick,
  TradeFilter,
  PaginatedResult,
  TradeStats,
} from "../types/trading";

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
