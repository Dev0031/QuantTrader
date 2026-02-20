import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from "../client";
import type {
  ExchangeSettings,
  SaveExchangeSettingsRequest,
  ApiKeyStatus,
  ApiProviderInfo,
  VerificationResult,
  IntegrationStatus,
  SetupStatus,
} from "../types/settings";

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
  return useMutation<VerificationResult, Error, string>({
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

export function useApiProviders() {
  return useQuery<ApiProviderInfo[]>({
    queryKey: ["settings", "providers"],
    queryFn: async () => {
      const { data } = await apiClient.get("/settings/providers");
      return data;
    },
    staleTime: 30 * 1000,
    refetchInterval: 30 * 1000,
    retry: 1,
  });
}

export function useIntegrationStatus() {
  return useQuery<IntegrationStatus[]>({
    queryKey: ["dashboard", "integration-status"],
    queryFn: async () => {
      const { data } = await apiClient.get("/dashboard/integration-status");
      return data;
    },
    staleTime: 5 * 1000,
    refetchInterval: 5 * 1000,
    retry: 1,
  });
}

export function useSetupStatus(): SetupStatus {
  const { data: providers } = useApiProviders();
  const { data: integrations } = useIntegrationStatus();

  const missingRequired = (providers ?? [])
    .filter((p) => p.isRequired && !p.isConfigured)
    .map((p) => p.name);

  const errors: SetupStatus["errors"] = [];
  const warnings: SetupStatus["warnings"] = [];

  (integrations ?? []).forEach((i) => {
    if (i.status === "Error" && i.lastError) {
      errors.push({
        provider: i.provider,
        message: i.lastError,
        steps: [
          `Go to Settings and check your ${i.provider} API key`,
          "Verify the connection using the Verify button",
          "Check that API permissions are correct",
        ],
      });
    } else if (i.status === "Disconnected") {
      const provider = (providers ?? []).find((p) => p.name === i.provider);
      if (provider?.isConfigured) {
        warnings.push({
          provider: i.provider,
          message: `${i.provider} is configured but not receiving data`,
        });
      }
    }
  });

  return {
    isReady: missingRequired.length === 0,
    missingRequired,
    errors,
    warnings,
  };
}
