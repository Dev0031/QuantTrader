import React from "react";
import { useSystemPipeline } from "@/api/hooks";
import type { PipelineStage, PipelineStatus } from "@/api/types";
import { formatDistanceToNow } from "date-fns";
import {
  Wifi,
  Database,
  Zap,
  Brain,
  Shield,
  Send,
  Monitor,
  ChevronRight,
  RefreshCw,
} from "lucide-react";

const iconMap: Record<string, React.FC<{ className?: string }>> = {
  exchange: Wifi,
  database: Database,
  zap: Zap,
  brain: Brain,
  shield: Shield,
  send: Send,
  monitor: Monitor,
};

const statusConfig: Record<PipelineStatus, { ring: string; dot: string; label: string; labelColor: string }> = {
  active:  { ring: "ring-emerald-500/50", dot: "bg-emerald-400", label: "Active",  labelColor: "text-emerald-400" },
  idle:    { ring: "ring-amber-500/30",   dot: "bg-amber-400",   label: "Idle",    labelColor: "text-amber-400" },
  error:   { ring: "ring-red-500/50",     dot: "bg-red-400",     label: "Error",   labelColor: "text-red-400" },
  unknown: { ring: "ring-gray-600/30",    dot: "bg-gray-600",    label: "Unknown", labelColor: "text-gray-500" },
};

const PipelineCard: React.FC<{ stage: PipelineStage; isLast: boolean }> = ({ stage, isLast }) => {
  const cfg = statusConfig[stage.status];
  const Icon = iconMap[stage.icon] ?? Monitor;

  return (
    <div className="flex items-center gap-2">
      <div className={`relative flex flex-col items-center bg-panel border border-panel-border rounded-xl p-3 w-36 ring-1 ${cfg.ring} transition-all duration-300`}>
        {/* Pulse for active */}
        {stage.status === "active" && (
          <span className="absolute -top-1 -right-1">
            <span className="relative flex h-2.5 w-2.5">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
              <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-400" />
            </span>
          </span>
        )}

        <div className={`w-8 h-8 rounded-lg flex items-center justify-center mb-2 ${
          stage.status === "active" ? "bg-emerald-500/20" :
          stage.status === "idle"   ? "bg-amber-500/20" :
          stage.status === "error"  ? "bg-red-500/20" :
          "bg-gray-700/40"
        }`}>
          <Icon className={`w-4 h-4 ${
            stage.status === "active" ? "text-emerald-400" :
            stage.status === "idle"   ? "text-amber-400" :
            stage.status === "error"  ? "text-red-400" :
            "text-gray-500"
          }`} />
        </div>

        <p className="text-xs font-semibold text-white text-center leading-tight mb-1">
          {stage.name}
        </p>

        <div className="flex items-center gap-1">
          <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot}`} />
          <span className={`text-[10px] font-medium ${cfg.labelColor}`}>{cfg.label}</span>
        </div>

        {stage.lastActivityAt && (
          <p className="text-[9px] text-gray-600 mt-1 text-center">
            {formatDistanceToNow(new Date(stage.lastActivityAt), { addSuffix: true })}
          </p>
        )}

        {stage.lastMessage && (
          <p className="text-[9px] text-gray-500 mt-1 text-center leading-tight line-clamp-2">
            {stage.lastMessage}
          </p>
        )}
      </div>

      {!isLast && (
        <div className="flex flex-col items-center gap-0.5">
          <div className="h-px w-4 bg-gray-700" />
          <ChevronRight className="w-3 h-3 text-gray-600" />
        </div>
      )}
    </div>
  );
};

const PipelineVisualizer: React.FC = () => {
  const { data: stages, isLoading, refetch } = useSystemPipeline();

  const activeCount = stages?.filter((s) => s.status === "active").length ?? 0;
  const total = stages?.length ?? 0;

  return (
    <div className="bg-panel border border-panel-border rounded-xl p-4">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="text-sm font-semibold text-white">Data Pipeline</h3>
          <p className="text-xs text-gray-500 mt-0.5">
            {isLoading ? "Loading..." : `${activeCount}/${total} stages active`}
          </p>
        </div>
        <button
          onClick={() => refetch()}
          className="p-1.5 rounded-lg hover:bg-gray-700 text-gray-500 hover:text-gray-300 transition-colors"
          title="Refresh pipeline status"
        >
          <RefreshCw className="w-3.5 h-3.5" />
        </button>
      </div>

      {isLoading ? (
        <div className="flex gap-2">
          {Array.from({ length: 7 }).map((_, i) => (
            <div key={i} className="flex items-center gap-2">
              <div className="w-36 h-28 bg-gray-800 rounded-xl animate-pulse" />
              {i < 6 && <div className="w-4 h-px bg-gray-700" />}
            </div>
          ))}
        </div>
      ) : (
        <div className="flex items-start gap-1 overflow-x-auto pb-2">
          {(stages ?? []).map((stage, i) => (
            <PipelineCard
              key={stage.name}
              stage={stage}
              isLast={i === (stages?.length ?? 1) - 1}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default PipelineVisualizer;
