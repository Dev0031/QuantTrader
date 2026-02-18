import React, { useEffect, useRef, useState } from "react";
import { useSystemActivity } from "@/api/hooks";
import type { ActivityEntry, ActivityLevel } from "@/api/types";
import { formatDistanceToNow } from "date-fns";

interface ActivityFeedProps {
  liveActivity?: ActivityEntry | null;
  maxItems?: number;
  compact?: boolean;
}

const levelConfig: Record<ActivityLevel, { dot: string; text: string; bg: string }> = {
  info:    { dot: "bg-blue-400",   text: "text-blue-300",   bg: "border-l-blue-500/40" },
  success: { dot: "bg-emerald-400",text: "text-emerald-300",bg: "border-l-emerald-500/40" },
  warning: { dot: "bg-amber-400",  text: "text-amber-300",  bg: "border-l-amber-500/40" },
  error:   { dot: "bg-red-400",    text: "text-red-300",    bg: "border-l-red-500/40" },
};

const serviceColors: Record<string, string> = {
  DataIngestion: "text-violet-300",
  StrategyEngine: "text-cyan-300",
  RiskManager: "text-rose-300",
  ExecutionEngine: "text-amber-300",
  ApiGateway: "text-slate-300",
  Simulation: "text-emerald-300",
  System: "text-gray-400",
};

const ActivityRow: React.FC<{ entry: ActivityEntry; isNew?: boolean }> = ({ entry, isNew }) => {
  const cfg = levelConfig[entry.level] ?? levelConfig.info;
  const svcColor = serviceColors[entry.service] ?? "text-gray-400";

  return (
    <div
      className={`flex items-start gap-3 px-3 py-2.5 border-l-2 ${cfg.bg} transition-all duration-300 ${
        isNew ? "bg-white/5" : "bg-transparent"
      } hover:bg-white/5`}
    >
      <div className={`w-1.5 h-1.5 rounded-full mt-1.5 flex-shrink-0 ${cfg.dot}`} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className={`text-[10px] font-semibold uppercase tracking-wider ${svcColor}`}>
            {entry.service}
          </span>
          {entry.symbol && (
            <span className="text-[10px] font-mono bg-gray-700/60 text-gray-300 px-1.5 py-0.5 rounded">
              {entry.symbol}
            </span>
          )}
          <span className="text-[10px] text-gray-600 ml-auto flex-shrink-0">
            {formatDistanceToNow(new Date(entry.timestamp), { addSuffix: true })}
          </span>
        </div>
        <p className={`text-xs mt-0.5 ${cfg.text} leading-relaxed`}>{entry.message}</p>
      </div>
    </div>
  );
};

const ActivityFeed: React.FC<ActivityFeedProps> = ({
  liveActivity,
  maxItems = 80,
  compact = false,
}) => {
  const { data: initialEntries = [] } = useSystemActivity(maxItems);
  const [entries, setEntries] = useState<ActivityEntry[]>([]);
  const [newIds, setNewIds] = useState<Set<string>>(new Set());
  const [serviceFilter, setServiceFilter] = useState<string>("all");
  const [levelFilter, setLevelFilter] = useState<string>("all");
  const [autoScroll, setAutoScroll] = useState(true);
  const bottomRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // Seed from REST fetch
  useEffect(() => {
    if (initialEntries.length > 0 && entries.length === 0) {
      setEntries(initialEntries);
    }
  }, [initialEntries, entries.length]);

  // Prepend real-time entries from SignalR
  useEffect(() => {
    if (!liveActivity) return;
    setEntries((prev) => {
      if (prev.some((e) => e.id === liveActivity.id)) return prev;
      const next = [liveActivity, ...prev].slice(0, maxItems);
      return next;
    });
    setNewIds((prev) => {
      const next = new Set(prev);
      next.add(liveActivity.id);
      setTimeout(() => setNewIds((s) => { const c = new Set(s); c.delete(liveActivity.id); return c; }), 2000);
      return next;
    });
  }, [liveActivity, maxItems]);

  // Auto-scroll to top (newest is at top)
  useEffect(() => {
    if (autoScroll && containerRef.current) {
      containerRef.current.scrollTop = 0;
    }
  }, [entries, autoScroll]);

  const services = ["all", ...Array.from(new Set(entries.map((e) => e.service))).sort()];
  const levels = ["all", "info", "success", "warning", "error"];

  const filtered = entries.filter((e) => {
    if (serviceFilter !== "all" && e.service !== serviceFilter) return false;
    if (levelFilter !== "all" && e.level !== levelFilter) return false;
    return true;
  });

  return (
    <div className="flex flex-col h-full">
      {!compact && (
        <div className="flex items-center gap-3 px-4 py-3 border-b border-panel-border flex-shrink-0">
          <div className="flex items-center gap-1.5">
            <span className="relative flex h-2 w-2">
              {entries.length > 0 && (
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
              )}
              <span className={`relative inline-flex h-2 w-2 rounded-full ${entries.length > 0 ? "bg-emerald-400" : "bg-gray-600"}`} />
            </span>
            <span className="text-xs font-medium text-gray-300">
              Live Activity Feed
            </span>
            <span className="text-xs text-gray-500">({filtered.length} entries)</span>
          </div>

          <div className="flex items-center gap-2 ml-auto">
            <select
              value={serviceFilter}
              onChange={(e) => setServiceFilter(e.target.value)}
              className="text-xs bg-gray-800 border border-gray-700 text-gray-300 rounded px-2 py-1"
            >
              {services.map((s) => (
                <option key={s} value={s}>{s === "all" ? "All services" : s}</option>
              ))}
            </select>

            <select
              value={levelFilter}
              onChange={(e) => setLevelFilter(e.target.value)}
              className="text-xs bg-gray-800 border border-gray-700 text-gray-300 rounded px-2 py-1"
            >
              {levels.map((l) => (
                <option key={l} value={l}>{l === "all" ? "All levels" : l}</option>
              ))}
            </select>

            <button
              onClick={() => setAutoScroll((v) => !v)}
              className={`text-xs px-2 py-1 rounded border transition-colors ${
                autoScroll
                  ? "bg-accent/20 border-accent text-accent-light"
                  : "border-gray-700 text-gray-500 hover:text-gray-300"
              }`}
            >
              {autoScroll ? "Auto" : "Manual"}
            </button>
          </div>
        </div>
      )}

      <div
        ref={containerRef}
        className="flex-1 overflow-y-auto divide-y divide-white/5 min-h-0"
      >
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-gray-600 gap-3 py-12">
            <div className="w-8 h-8 rounded-full border-2 border-gray-700 flex items-center justify-center">
              <span className="text-lg">—</span>
            </div>
            <p className="text-sm">No activity yet.</p>
            <p className="text-xs text-center max-w-xs">
              Start simulation mode or connect DataIngestion to Binance to see real-time events here.
            </p>
          </div>
        ) : (
          filtered.map((entry) => (
            <ActivityRow key={entry.id} entry={entry} isNew={newIds.has(entry.id)} />
          ))
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
};

export default ActivityFeed;
