export type ActivityLevel = "info" | "success" | "warning" | "error";

export interface ActivityEntry {
  id: string;
  service: string;
  level: ActivityLevel;
  message: string;
  symbol: string | null;
  timestamp: string;
}

export type PipelineStatus = "active" | "idle" | "error" | "unknown";

export interface PipelineStage {
  name: string;
  service: string;
  status: PipelineStatus;
  lastActivityAt: string | null;
  lastMessage: string | null;
  icon: string;
}

export interface SimulationStatus {
  enabled: boolean;
  startedAt: string | null;
  ticksGenerated: number;
  symbols: string[];
}

export interface DiagnosticIssue {
  severity: "info" | "warning" | "error";
  component: string;
  message: string;
  steps: string[];
}

export interface DiagnosticResult {
  issues: DiagnosticIssue[];
  tips: string[];
  timestamp: string;
}
