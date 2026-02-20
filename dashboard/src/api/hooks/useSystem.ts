import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from "../client";
import type {
  ActivityEntry,
  PipelineStage,
  SimulationStatus,
  DiagnosticResult,
} from "../types/system";

export function useSystemActivity(limit = 100, service?: string, level?: string) {
  const params = new URLSearchParams({ limit: limit.toString() });
  if (service) params.set("service", service);
  if (level) params.set("level", level);

  return useQuery<ActivityEntry[]>({
    queryKey: ["system", "activity", limit, service, level],
    queryFn: async () => {
      const { data } = await apiClient.get(`/system/activity?${params}`);
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
    retry: 1,
  });
}

export function useSystemPipeline() {
  return useQuery<PipelineStage[]>({
    queryKey: ["system", "pipeline"],
    queryFn: async () => {
      const { data } = await apiClient.get("/system/pipeline");
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
    retry: 1,
  });
}

export function useSimulationStatus() {
  return useQuery<SimulationStatus>({
    queryKey: ["system", "simulation"],
    queryFn: async () => {
      const { data } = await apiClient.get("/system/simulation");
      return data;
    },
    staleTime: 3 * 1000,
    refetchInterval: 3 * 1000,
    retry: 1,
  });
}

export function useStartSimulation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post("/system/simulation/start");
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["system"] });
    },
  });
}

export function useStopSimulation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post("/system/simulation/stop");
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["system"] });
    },
  });
}

export function useDiagnose() {
  const queryClient = useQueryClient();
  return useMutation<DiagnosticResult>({
    mutationFn: async () => {
      const { data } = await apiClient.post("/system/diagnose");
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["system"] });
    },
  });
}
