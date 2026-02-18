import React from "react";
import PipelineVisualizer from "@/components/system/PipelineVisualizer";
import ActivityFeed from "@/components/system/ActivityFeed";
import SimulationControl from "@/components/system/SimulationControl";
import { useSignalR } from "@/hooks/useSignalR";
import { Terminal, Activity } from "lucide-react";

const SystemPage: React.FC = () => {
  const { lastActivity, isConnected } = useSignalR();

  return (
    <div className="space-y-5">
      {/* Page header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Terminal className="w-5 h-5 text-accent-light" />
            <h1 className="text-xl font-bold text-white">System Monitor</h1>
          </div>
          <p className="text-sm text-gray-500">
            Real-time visibility into the trading pipeline — what's running, what's flowing, and why.
          </p>
        </div>

        <div className="flex items-center gap-2 text-xs">
          <span className="relative flex h-2 w-2">
            {isConnected && (
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
            )}
            <span className={`relative inline-flex h-2 w-2 rounded-full ${isConnected ? "bg-emerald-400" : "bg-red-500"}`} />
          </span>
          <span className={isConnected ? "text-emerald-400" : "text-red-400"}>
            SignalR {isConnected ? "Connected" : "Disconnected"}
          </span>
        </div>
      </div>

      {/* Why is nothing showing banner */}
      {!isConnected && (
        <div className="bg-red-500/10 border border-red-500/30 rounded-xl p-4">
          <div className="flex items-start gap-3">
            <Activity className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
            <div className="space-y-1">
              <p className="text-sm font-semibold text-red-300">SignalR not connected</p>
              <p className="text-xs text-gray-400">
                The dashboard cannot receive real-time updates. Ensure the ApiGateway service is running
                on the expected URL and CORS is configured correctly.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Simulation + Diagnose */}
      <SimulationControl />

      {/* Pipeline */}
      <PipelineVisualizer />

      {/* Activity Feed */}
      <div className="bg-panel border border-panel-border rounded-xl overflow-hidden" style={{ height: "520px" }}>
        <ActivityFeed liveActivity={lastActivity} maxItems={200} />
      </div>

      {/* How it works explainer */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <ExplainerCard
          title="Why is the dashboard showing 0 data?"
          body="Several things must all be running: Redis (cache), DataIngestion (Binance WebSocket), and ApiGateway (this API). Use the Diagnose button above to check. Or enable Simulation Mode to bypass the real exchange entirely."
          accent="amber"
        />
        <ExplainerCard
          title="I added an API key — why is nothing happening?"
          body="API keys saved on the Settings page are stored in Redis. The DataIngestion service reads its Binance credentials from environment variables (BINANCE__APIKEY, BINANCE__APISECRET) or appsettings.json at startup — not from Redis. Restart DataIngestion after updating env vars."
          accent="blue"
        />
        <ExplainerCard
          title="How to test end-to-end without a real account?"
          body='Click "Start Simulation" above. The system will generate synthetic BTCUSDT/ETHUSDT ticks at ~2/s, write them to Redis, publish them via SignalR, and the price ticker on the Dashboard page will update in real-time. This proves the entire pipeline works.'
          accent="emerald"
        />
      </div>
    </div>
  );
};

const accentMap: Record<string, string> = {
  amber: "border-l-amber-500 bg-amber-500/5",
  blue: "border-l-blue-500 bg-blue-500/5",
  emerald: "border-l-emerald-500 bg-emerald-500/5",
};

const ExplainerCard: React.FC<{ title: string; body: string; accent: string }> = ({
  title,
  body,
  accent,
}) => (
  <div className={`border-l-2 rounded-xl p-4 border border-panel-border ${accentMap[accent] ?? ""}`}>
    <p className="text-xs font-semibold text-white mb-2">{title}</p>
    <p className="text-xs text-gray-400 leading-relaxed">{body}</p>
  </div>
);

export default SystemPage;
