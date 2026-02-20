import {
  createContext,
  useContext,
  useRef,
  useState,
  useCallback,
  useEffect,
  type ReactNode,
} from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { MarketTick, Trade, RiskAlert, ActivityEntry } from "@/api/types";

interface ConnectionState {
  isConnected: boolean;
  lastTick: MarketTick | null;
  lastTrade: Trade | null;
  lastAlert: RiskAlert | null;
  lastActivity: ActivityEntry | null;
  subscribe: (symbol: string) => void;
  unsubscribe: (symbol: string) => void;
}

const ConnectionContext = createContext<ConnectionState>({
  isConnected: false,
  lastTick: null,
  lastTrade: null,
  lastAlert: null,
  lastActivity: null,
  subscribe: () => {},
  unsubscribe: () => {},
});

export function ConnectionProvider({ children }: { children: ReactNode }) {
  const connectionRef = useRef<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastTick, setLastTick] = useState<MarketTick | null>(null);
  const [lastTrade, setLastTrade] = useState<Trade | null>(null);
  const [lastAlert, setLastAlert] = useState<RiskAlert | null>(null);
  const [lastActivity, setLastActivity] = useState<ActivityEntry | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/trading")
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.elapsedMilliseconds < 60000) {
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 15000);
          }
          return 30000;
        },
      })
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("OnTickUpdate", (tick: MarketTick) => setLastTick(tick));
    connection.on("OnTradeExecuted", (trade: Trade) => setLastTrade(trade));
    connection.on("OnRiskAlert", (alert: RiskAlert) => setLastAlert(alert));
    connection.on("OnSystemActivity", (activity: ActivityEntry) => setLastActivity(activity));
    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    connectionRef.current = connection;

    const start = async () => {
      try {
        await connection.start();
        setIsConnected(true);
      } catch {
        setTimeout(start, 5000);
      }
    };
    start();

    return () => {
      if (connection.state === HubConnectionState.Connected) {
        connection.stop();
      }
    };
  }, []);

  const subscribe = useCallback((symbol: string) => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      conn.invoke("SubscribeToSymbol", symbol).catch(console.error);
    }
  }, []);

  const unsubscribe = useCallback((symbol: string) => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      conn.invoke("UnsubscribeFromSymbol", symbol).catch(console.error);
    }
  }, []);

  return (
    <ConnectionContext.Provider
      value={{ isConnected, lastTick, lastTrade, lastAlert, lastActivity, subscribe, unsubscribe }}
    >
      {children}
    </ConnectionContext.Provider>
  );
}

export function useConnection() {
  return useContext(ConnectionContext);
}
