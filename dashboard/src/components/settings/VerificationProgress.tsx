import type { VerificationStep } from '@/api/types/settings';

interface Props {
  steps: VerificationStep[];
  isRunning: boolean;
}

function StepIcon({ status }: { status: VerificationStep['status'] }) {
  if (status === 'running') {
    return (
      <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-blue-400 border-t-transparent" />
    );
  }
  if (status === 'success') return <span className="text-green-400 font-bold">&#10003;</span>;
  if (status === 'error') return <span className="text-red-400 font-bold">&#10007;</span>;
  if (status === 'warning') return <span className="text-amber-400 font-bold">&#9888;</span>;
  return <span className="text-gray-500">&#9675;</span>;
}

function stepColor(status: VerificationStep['status']): string {
  switch (status) {
    case 'success': return 'border-green-500 bg-green-900/20';
    case 'error':   return 'border-red-500 bg-red-900/20';
    case 'warning': return 'border-amber-500 bg-amber-900/20';
    case 'running': return 'border-blue-500 bg-blue-900/20';
    default:        return 'border-gray-700 bg-gray-800/40';
  }
}

function labelColor(status: VerificationStep['status']): string {
  switch (status) {
    case 'success': return 'text-green-300';
    case 'error':   return 'text-red-300';
    case 'warning': return 'text-amber-300';
    case 'running': return 'text-blue-300';
    default:        return 'text-gray-400';
  }
}

export function VerificationProgress({ steps, isRunning }: Props) {
  if (steps.length === 0 && !isRunning) return null;

  return (
    <div className="space-y-2 mt-3">
      {steps.map((step) => (
        <div
          key={step.step}
          className={`flex items-start gap-3 rounded-lg border px-3 py-2 text-sm ${stepColor(step.status)}`}
        >
          <div className="flex h-5 w-5 shrink-0 items-center justify-center text-base">
            <StepIcon status={step.status} />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center justify-between gap-2">
              <span className={`font-medium ${labelColor(step.status)}`}>{step.name}</span>
              {step.durationMs > 0 && (
                <span className="shrink-0 text-xs text-gray-500">{step.durationMs} ms</span>
              )}
            </div>
            <p className="mt-0.5 text-gray-400 text-xs">{step.message}</p>
          </div>
        </div>
      ))}
      {isRunning && steps.length === 0 && (
        <div className="flex items-center gap-2 text-sm text-blue-400">
          <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-blue-400 border-t-transparent" />
          <span>Verifying connection…</span>
        </div>
      )}
    </div>
  );
}
