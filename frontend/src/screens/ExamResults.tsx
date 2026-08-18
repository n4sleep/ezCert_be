import { useEffect, useState } from "react";
import { request } from "../api/client";
import type { AttemptResult } from "../types";
import { useToasts } from "../components/Toast";

interface Props {
  attemptId: string;
  onBack: () => void;
}

export default function ExamResults({ attemptId, onBack }: Props) {
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [error, setError] = useState("");
  const { push } = useToasts();

  useEffect(() => {
    request<AttemptResult>(`/api/attempts/${attemptId}/results`)
      .then((r) => {
        setResult(r);
        push("success", "Results saved to this browser's history");
      })
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load results"));
  }, [attemptId, push]);

  if (error)
    return (
      <div className="max-w-4xl mx-auto p-xl">
        <div className="bg-danger-soft text-danger p-lg rounded-xl">{error}</div>
      </div>
    );
  if (!result) return <div className="max-w-4xl mx-auto p-xl text-on-surface-variant">Loading results…</div>;

  const circumference = 2 * Math.PI * 40;
  const offset = circumference * (1 - result.scorePercent / 100);

  return (
    <div className="py-xl px-md md:px-xxl max-w-container-max mx-auto flex flex-col gap-lg">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-headline-lg font-headline-lg text-on-background">Exam Results: AZ-900 Cloud Concepts</h1>
          <p className="text-body-md font-body-md text-on-surface-variant mt-xs">Review your performance and explanations below.</p>
        </div>
        <button
          className="inline-flex items-center gap-xs px-md py-sm bg-surface-container rounded-lg text-primary hover:bg-surface-variant transition-colors shadow-sm"
          onClick={onBack}
        >
          <span>←</span>
          <span className="text-label-md font-label-md">Back to history</span>
        </button>
      </div>

      {/* Score hero */}
      <div className="bg-surface-container rounded-xl p-lg md:p-xl shadow-md flex flex-col md:flex-row items-center gap-xl relative overflow-hidden">
        <div className="absolute -right-8 -top-8 w-64 h-64 bg-primary/5 rounded-full blur-3xl" />
        <div className="flex-1 w-full text-center md:text-left flex flex-col items-center md:items-start gap-md z-10">
          <span
            className={
              "inline-flex items-center gap-xs px-md py-xs rounded-full text-label-caps font-label-caps uppercase tracking-wider " +
              (result.passed ? "bg-success-soft text-success-strong" : "bg-danger-soft text-danger")
            }
          >
            {result.passed ? "✓ Great job! You passed." : "✕ Keep practicing"}
          </span>
          <h2 className="text-display-lg font-display-lg text-on-background">
            {result.correctCount}/{result.totalQuestions}{" "}
            <span className="text-headline-md font-headline-md text-on-surface-variant align-middle">({result.scorePercent}%)</span>
          </h2>
          {result.expired && <p className="text-body-sm text-on-surface-variant">Exam expired — scored from your saved answers.</p>}
        </div>
        <div className="relative w-48 h-48 z-10 shrink-0">
          <svg className="w-full h-full transform -rotate-90" viewBox="0 0 100 100">
            <circle className="text-surface-variant" cx="50" cy="50" fill="none" r="40" stroke="currentColor" strokeWidth="8" />
            <circle
              className="text-primary"
              cx="50"
              cy="50"
              fill="none"
              r="40"
              stroke="currentColor"
              strokeDasharray={circumference}
              strokeDashoffset={offset}
              strokeLinecap="round"
              strokeWidth="8"
              style={{ transition: "stroke-dashoffset 1s ease-out" }}
            />
          </svg>
          <div className="absolute inset-0 flex items-center justify-center flex-col">
            <span className="text-headline-lg font-headline-lg text-primary">{result.scorePercent}%</span>
          </div>
        </div>
      </div>

      {/* Section breakdown */}
      <div className="flex flex-col gap-lg">
        <h3 className="text-headline-md font-headline-md text-on-background pt-md">By section</h3>
        {result.sections.map((s) => (
          <div key={s.section} className="flex items-center gap-md">
            <span className="w-40 font-label-md text-on-surface-variant">{s.section}</span>
            <div className="flex-1 bg-surface-variant rounded-full h-2 overflow-hidden">
              <div className="bg-primary h-full rounded-full" style={{ width: `${s.percentage}%` }} />
            </div>
            <span className="w-16 text-right font-label-md text-on-surface-variant tabular-nums">{s.correct}/{s.total}</span>
          </div>
        ))}
      </div>

      {/* Question review */}
      <div className="flex flex-col gap-lg">
        <h3 className="text-headline-md font-headline-md text-on-background pt-md">Question Review</h3>
        {result.review.map((r) => (
          <div key={r.ordinal} className="bg-surface shadow-sm rounded-xl overflow-hidden transition-shadow hover:shadow-md">
            <div
              className={
                "px-lg py-md flex items-center justify-between " +
                (r.isCorrect ? "bg-success-soft/50" : "bg-danger-soft/50")
              }
            >
              <div className="flex items-center gap-sm">
                <span
                  className={
                    "flex items-center justify-center w-8 h-8 rounded-full text-white " +
                    (r.isCorrect ? "bg-success-strong" : "bg-danger")
                  }
                >
                  {r.isCorrect ? "✓" : "✕"}
                </span>
                <span className={"text-label-md font-label-md " + (r.isCorrect ? "text-success-strong" : "text-danger")}>
                  Question {r.ordinal + 1}
                </span>
              </div>
              <span
                className={
                  "text-label-caps font-label-caps uppercase px-sm py-xs rounded " +
                  (r.isCorrect ? "text-success-strong bg-success-soft" : "text-danger bg-danger-soft")
                }
              >
                {r.isCorrect ? "Correct" : "Incorrect"}
              </span>
            </div>
            <div className="p-lg md:p-xl flex flex-col gap-lg">
              <p className="text-body-lg font-body-lg text-on-background">{r.text}</p>
              {!r.isCorrect && (
                <div className="flex flex-col gap-md pl-4 relative">
                  <div className="absolute left-0 top-0 bottom-0 w-1 bg-danger rounded-full" />
                  <div className="flex items-start gap-md p-md bg-danger-soft/30 rounded-lg">
                    <span className="text-danger mt-1">✕</span>
                    <div className="flex flex-col">
                      <span className="text-label-caps font-label-caps text-danger mb-1">Your Answer</span>
                      <span className="text-body-md text-on-surface-variant line-through decoration-danger">
                        {r.selected.join(", ") || "No answer"}
                      </span>
                    </div>
                  </div>
                  <div className="flex items-start gap-md p-md bg-success-soft/30 rounded-lg">
                    <span className="text-success-strong mt-1">✓</span>
                    <div className="flex flex-col">
                      <span className="text-label-caps font-label-caps text-success-strong mb-1">Correct Answer</span>
                      <span className="text-body-md text-on-background">{r.correct.join(", ")}</span>
                    </div>
                  </div>
                </div>
              )}
              <div className="mt-md bg-surface-container-low rounded-lg p-md">
                <h4 className="text-label-md font-label-md text-on-surface flex items-center gap-xs mb-sm">
                  <span className="text-primary">💡</span> Explanation
                </h4>
                <p className="text-body-sm font-body-sm text-on-surface-variant">{r.explanation}</p>
                {r.source && (
                  <a href={r.source} target="_blank" rel="noreferrer" className="inline-block mt-sm text-primary font-label-md">
                    Read the source →
                  </a>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
