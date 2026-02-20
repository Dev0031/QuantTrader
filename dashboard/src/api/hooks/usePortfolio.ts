import { useQuery } from "@tanstack/react-query";
import apiClient from "../client";
import type { PortfolioOverview, EquityPoint } from "../types/portfolio";

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
