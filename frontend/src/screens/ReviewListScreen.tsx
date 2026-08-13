import type { AttemptSummary } from "../types";

interface Props {
  attempts: AttemptSummary[];
  onOpenResult: (attemptId: string) => void;
  onGoChat: () => void;
}

// Review tab: this device's completed attempts, newest first (EXPERIENCE.md IA).
// Empty state: "Complete an exam to see your results".
function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

export default function ReviewListScreen({ attempts, onOpenResult, onGoChat }: Props) {
  if (attempts.length === 0) {
    return (
      <div className="max-w-2xl mx-auto p-xl text-center">
        <div className="bg-surface-container-lowest rounded-2xl shadow-md p-xl">
          <div className="w-12 h-12 rounded-full bg-secondary-container text-on-secondary-container grid place-items-center text-xl mx-auto mb-md">📋</div>
          <h2 className="font-headline-lg text-headline-lg text-on-background">Complete an exam to see your results</h2>
          <p className="text-body-md text-on-surface-variant mt-xs mb-lg">
            Your scores and review will be listed here, newest first.
          </p>
          <button
            className="inline-flex items-center gap-sm px-xl py-md rounded-lg bg-primary text-on-primary font-label-md shadow-md hover:-translate-y-0.5 transition-transform"
            onClick={onGoChat}
          >
            Take an exam →
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto p-xl flex flex-col gap-lg">
      <div>
        <h1 className="text-headline-lg font-headline-lg text-on-background">Your results</h1>
        <p className="text-body-md font-body-md text-on-surface-variant mt-xs">Completed attempts, newest first.</p>
      </div>
      {attempts.map((a) => (
        <button
          key={a.attemptId}
          className="text-left bg-surface shadow-sm rounded-xl overflow-hidden transition-shadow hover:shadow-md cursor-pointer"
          onClick={() => onOpenResult(a.attemptId)}
        >
          <div className="p-lg md:p-xl flex items-center gap-md">
            <div
              className={
                "shrink-0 w-16 h-16 rounded-xl grid place-items-center font-display-lg text-display-lg " +
                (a.passed ? "bg-success-soft text-success-strong" : "bg-danger-soft text-danger")
              }
            >
              {Math.round(a.scorePercent)}%
            </div>
            <div className="flex-1">
              <div className="flex items-center gap-sm mb-xs">
                <span
                  className={
                    "px-2 py-0.5 rounded-full text-[10px] uppercase tracking-wider " +
                    (a.passed ? "bg-success-soft text-success-strong" : "bg-danger-soft text-danger")
                  }
                >
                  {a.passed ? "Passed" : "Failed"}
                </span>
                <span className="text-label-caps text-on-surface-variant">{a.status}</span>
              </div>
              <h3 className="font-headline-md text-headline-md text-on-surface">{a.title}</h3>
              <p className="text-body-sm text-on-surface-variant mt-xs">{formatDate(a.startedAt)}</p>
            </div>
            <span className="text-primary shrink-0">→</span>
          </div>
        </button>
      ))}
    </div>
  );
}
