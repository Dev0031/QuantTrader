import React, { useState } from "react";
import {
  useSimulationStatus,
  useStartSimulation,
  useStopSimulation,
  useDiagnose,
} from "@/api/hooks";
import type { DiagnosticResult } from "@/api/types";
import { FlaskConical, Play, Square, AlertTriangle, CheckCircle2, Info, Wrench } from "lucide-react";
import { formatDistanceToNow } from "date-fns";

const SimulationControl: React.FC = () => {
  const { data: sim } = useSimulationStatus();
  const startSim = useStartSimulation();
  const stopSim = useStopSimulation();
  const diagnose = useDiagnose();
  const [diagnostic, setDiagnostic] = useState<DiagnosticResult | null>(null);
  const [showDiag, setShowDiag] = useState(false);

  const isRunning = sim?.enabled ?? false;

  const handleDiagnose = async () => {
    const result = await diagnose.mutateAsync(undefined as void);
    setDiagnostic(result);
    setShowDiag(true);
  };

  const severityIcon = {
    error: <AlertTriangle className="w-4 h-4 text-red-400 flex-shrink-0 mt-0.5" />,
    warning: <AlertTriangle className="w-4 h-4 text-amber-400 flex-shrink-0 mt-0.5" />,
    info: <Info className="w-4 h-4 text-blue-400 flex-shrink-0 mt-0.5" />,
  };

  return (
    <div className="bg-panel border border-panel-border rounded-xl p-4 space-y-4">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div className={`w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0 ${
            isRunning ? "bg-emerald-500/20" : "bg-gray-700/40"
          }`}>
            <FlaskConical className={`w-5 h-5 ${isRunning ? "text-emerald-400" : "text-gray-500"}`} />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="text-sm font-semibold text-white">Simulation Mode</h3>
              {isRunning && (
                <span className="inline-flex items-center gap-1 text-[10px] font-medium text-emerald-300 bg-emerald-500/15 px-2 py-0.5 rounded-full">
                  <span className="w-1.5 h-1.5 bg-emerald-400 rounded-full animate-pulse" />
                  Running
                </span>
              )}
            </div>
            <p className="text-xs text-gray-500 mt-0.5">
              {isRunning
                ? `Generating synthetic market ticks — ${(sim?.ticksGenerated ?? 0).toLocaleString()} ticks since ${
                    sim?.startedAt
                      ? formatDistanceToNow(new Date(sim.startedAt), { addSuffix: true })
                      : "start"
                  }`
                : "Start to generate fake Binance data and test the entire UI pipeline without a real connection."}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0">
          {!isRunning ? (
            <button
              onClick={() => startSim.mutate(undefined)}
              disabled={startSim.isPending}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium rounded-lg transition-colors disabled:opacity-50"
            >
              <Play className="w-3.5 h-3.5" />
              Start Simulation
            </button>
          ) : (
            <button
              onClick={() => stopSim.mutate(undefined)}
              disabled={stopSim.isPending}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-gray-700 hover:bg-gray-600 text-white text-xs font-medium rounded-lg transition-colors disabled:opacity-50"
            >
              <Square className="w-3.5 h-3.5" />
              Stop
            </button>
          )}

          <button
            onClick={handleDiagnose}
            disabled={diagnose.isPending}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-gray-700 hover:bg-gray-600 text-gray-200 text-xs font-medium rounded-lg transition-colors disabled:opacity-50"
          >
            <Wrench className="w-3.5 h-3.5" />
            {diagnose.isPending ? "Checking..." : "Diagnose"}
          </button>
        </div>
      </div>

      {/* What simulation covers */}
      {!isRunning && (
        <div className="bg-gray-800/50 rounded-lg p-3 text-xs text-gray-400 space-y-1">
          <p className="font-medium text-gray-300">What simulation tests:</p>
          <ul className="space-y-0.5 list-disc list-inside ml-1">
            <li>Generates realistic BTCUSDT, ETHUSDT, SOLUSDT price ticks (±0.05%/tick)</li>
            <li>Writes prices to Redis — REST price endpoints return live values</li>
            <li>Publishes events to event bus → RealTimeNotifier → SignalR → dashboard</li>
            <li>Shows ticks in live price ticker, confirms SignalR connection is working</li>
          </ul>
          <p className="text-gray-500 mt-2 italic">
            This lets you verify the entire frontend pipeline without a real Binance API key.
          </p>
        </div>
      )}

      {/* Diagnostic results */}
      {showDiag && diagnostic && (
        <div className="border border-panel-border rounded-lg overflow-hidden">
          <div className="flex items-center justify-between px-3 py-2 bg-gray-800/50 border-b border-panel-border">
            <div className="flex items-center gap-2">
              {diagnostic.issues.length === 0 ? (
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
              ) : (
                <AlertTriangle className="w-4 h-4 text-amber-400" />
              )}
              <span className="text-xs font-medium text-white">
                {diagnostic.issues.length === 0
                  ? "All systems operational"
                  : `${diagnostic.issues.length} issue(s) found`}
              </span>
            </div>
            <button
              onClick={() => setShowDiag(false)}
              className="text-gray-500 hover:text-gray-300 text-xs"
            >
              Close
            </button>
          </div>

          <div className="divide-y divide-white/5">
            {diagnostic.issues.map((issue, i) => (
              <div key={i} className="p-3 space-y-2">
                <div className="flex items-start gap-2">
                  {severityIcon[issue.severity]}
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-semibold text-gray-300">{issue.component}</span>
                      <span className={`text-[10px] uppercase font-medium ${
                        issue.severity === "error" ? "text-red-400" :
                        issue.severity === "warning" ? "text-amber-400" : "text-blue-400"
                      }`}>{issue.severity}</span>
                    </div>
                    <p className="text-xs text-gray-400 mt-0.5">{issue.message}</p>
                  </div>
                </div>
                {issue.steps.length > 0 && (
                  <ul className="ml-6 space-y-1">
                    {issue.steps.map((step, si) => (
                      <li key={si} className="text-[11px] text-gray-500 flex items-start gap-1.5">
                        <span className="text-gray-600 flex-shrink-0">{si + 1}.</span>
                        <span>{step}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}

            {diagnostic.tips.map((tip, i) => (
              <div key={`tip-${i}`} className="p-3 flex items-start gap-2">
                <Info className="w-4 h-4 text-blue-400 flex-shrink-0 mt-0.5" />
                <p className="text-xs text-gray-400">{tip}</p>
              </div>
            ))}

            {diagnostic.issues.length === 0 && (
              <div className="p-3 text-xs text-gray-400 text-center">
                No issues detected. If data still isn't flowing, try enabling Simulation Mode.
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default SimulationControl;
