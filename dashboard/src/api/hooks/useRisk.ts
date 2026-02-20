import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from "../client";
import type { RiskMetrics, RiskAlert } from "../types/risk";

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
