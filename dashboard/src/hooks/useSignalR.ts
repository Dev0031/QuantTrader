// useSignalR now delegates to ConnectionContext.
// All SignalR state is managed centrally in ConnectionProvider (App.tsx).
// This hook remains for backward compatibility with existing page components.
import { useConnection } from "@/contexts/ConnectionContext";

export function useSignalR() {
  return useConnection();
}
