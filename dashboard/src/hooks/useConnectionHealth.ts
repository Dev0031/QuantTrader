import { useState, useEffect } from "react";
import apiClient from "@/api/client";
import type { ConnectionHealth } from "@/api/types/settings";

export function useConnectionHealth(exchange: string | null) {
  const [health, setHealth] = useState<ConnectionHealth | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!exchange) return;

    let cancelled = false;

    const fetchHealth = async () => {
      setIsLoading(true);
      try {
        const { data } = await apiClient.get(
          `/settings/exchanges/${exchange}/health`
        );
        if (!cancelled) setHealth(data);
      } catch {
        // Health endpoint may not be available yet
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    fetchHealth();
    const interval = setInterval(fetchHealth, 30_000);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [exchange]);

  return { health, isLoading };
}
