import type { ConnectionHealth } from '@/api/types/settings';

interface Props {
  health: ConnectionHealth | null;
  isLoading: boolean;
}

function LatencyBadge({ ms }: { ms: number }) {
  const color = ms === 0 ? 'text-gray-500' : ms < 100 ? 'text-green-400' : ms < 300 ? 'text-amber-400' : 'text-red-400';
  return <span className={`font-mono font-semibold ${color}`}>{ms > 0 ? `${ms} ms` : '—'}</span>;
}

function RateLimitBar({ used, limit }: { used: number; limit: number }) {
  const pct = limit > 0 ? Math.min(100, (used / limit) * 100) : 0;
  const barColor = pct < 50 ? 'bg-green-500' : pct < 80 ? 'bg-amber-500' : 'bg-red-500';
  return (
    <div>
      <div className="flex items-center justify-between text-xs text-gray-400 mb-1">
        <span>Rate Limit</span>
        <span>{used} / {limit}</span>
      </div>
      <div className="h-1.5 w-full rounded-full bg-gray-700">
        <div
          className={`h-1.5 rounded-full transition-all ${barColor}`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

export function ConnectionHealthCard({ health, isLoading }: Props) {
  if (isLoading) {
    return (
      <div className="rounded-lg border border-gray-700 bg-gray-800/50 p-4 text-sm text-gray-400 flex items-center gap-2">
        <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-blue-400 border-t-transparent" />
        Loading connection health…
      </div>
    );
  }

  if (!health) {
    return (
      <div className="rounded-lg border border-gray-700 bg-gray-800/50 p-4 text-sm text-gray-500">
        No connection data
      </div>
    );
  }

  const lastTick = health.lastTickAt ? new Date(health.lastTickAt).toLocaleTimeString() : null;

  return (
    <div className="rounded-lg border border-gray-700 bg-gray-800/50 p-4 space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-semibold text-gray-200">{health.exchange} Health</span>
        <span
          className={`text-xs font-medium px-2 py-0.5 rounded-full ${
            health.isConnected ? 'bg-green-900/40 text-green-400' : 'bg-red-900/40 text-red-400'
          }`}
        >
          {health.isConnected ? 'Connected' : 'Disconnected'}
        </span>
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <div>
          <p className="text-xs text-gray-500 mb-0.5">REST Latency</p>
          <LatencyBadge ms={health.restLatencyMs} />
        </div>
        <div>
          <p className="text-xs text-gray-500 mb-0.5">WebSocket</p>
          <span
            className={`text-sm font-medium ${
              health.webSocketActive ? 'text-green-400' : 'text-gray-500'
            }`}
          >
            {health.webSocketActive ? 'Active' : 'Inactive'}
          </span>
        </div>
        <div>
          <p className="text-xs text-gray-500 mb-0.5">Ticks / min</p>
          <span className="font-mono font-semibold text-gray-200">{health.ticksPerMinute}</span>
        </div>
        <div>
          <p className="text-xs text-gray-500 mb-0.5">Last Tick</p>
          <span className="text-gray-300 text-xs">{lastTick ?? '—'}</span>
        </div>
      </div>

      <RateLimitBar used={health.requestWeightUsed} limit={health.requestWeightLimit} />
    </div>
  );
}
