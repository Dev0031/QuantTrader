import { useEffect, useRef, useState, useCallback } from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { MarketTick, Trade, RiskAlert, ActivityEntry } from "@/api/types";

interface SignalRState {
  isConnected: boolean;
  lastTick: MarketTick | null;
  lastTrade: Trade | null;
  lastAlert: RiskAlert | null;
  lastActivity: ActivityEntry | null;
}

interface UseSignalRReturn extends SignalRState {
  subscribe: (symbol: string) => void;
  unsubscribe: (symbol: string) => void;
}

export function useSignalR(): UseSignalRReturn {
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

    // NOTE: Event names must match exactly what the backend sends via SendAsync().
    // Backend sends: "OnTickUpdate", "OnTradeExecuted", "OnRiskAlert", "OnSystemActivity"
    connection.on("OnTickUpdate", (tick: MarketTick) => {
      setLastTick(tick);
    });

    connection.on("OnTradeExecuted", (trade: Trade) => {
      setLastTrade(trade);
    });

    connection.on("OnRiskAlert", (alert: RiskAlert) => {
      setLastAlert(alert);
    });

    connection.on("OnSystemActivity", (activity: ActivityEntry) => {
      setLastActivity(activity);
    });

    connection.onreconnecting(() => {
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
    });

    connection.onclose(() => {
      setIsConnected(false);
    });

    const startConnection = async () => {
      try {
        await connection.start();
        setIsConnected(true);
      } catch (err) {
        console.error("SignalR connection error:", err);
        setTimeout(startConnection, 5000);
      }
    };

    connectionRef.current = connection;
    startConnection();

    return () => {
      if (connection.state === HubConnectionState.Connected) {
        connection.stop();
      }
    };
  }, []);

  const subscribe = useCallback((symbol: string) => {
    const conn = connectionRef.current;
    if (conn && conn.state === HubConnectionState.Connected) {
      conn.invoke("SubscribeToSymbol", symbol).catch(console.error);
    }
  }, []);

  const unsubscribe = useCallback((symbol: string) => {
    const conn = connectionRef.current;
    if (conn && conn.state === HubConnectionState.Connected) {
      conn.invoke("UnsubscribeFromSymbol", symbol).catch(console.error);
    }
  }, []);

  return { isConnected, lastTick, lastTrade, lastAlert, lastActivity, subscribe, unsubscribe };
}
