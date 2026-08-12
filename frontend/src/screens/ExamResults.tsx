import { useEffect, useState } from "react";
import { request } from "../api/client";
import type { AttemptResult } from "../types";

interface Props {
  attemptId: string;
  onBack: () => void;
}

export default function ExamResults({ attemptId, onBack }: Props) {
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    request<AttemptResult>(`/api/attempts/${attemptId}/results`)
      .then(setResult)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load results"));
  }, [attemptId]);

  if (error) return <div className="bubble bubble--error">{error}</div>;
  if (!result) return <p>Loading results…</p>;

  return (
    <div className="results">
      <div className={"results__hero " + (result.passed ? "is-pass" : "is-fail")}>
        <div className="results__score">
          <span className="results__pct">{result.scorePercent}%</span>
          <span className="results__frac">{result.correctCount} / {result.totalQuestions}</span>
        </div>
        <div className="results__verdict">
          <h2>{result.passed ? "Great job! You passed." : "Keep practicing"}</h2>
          <p>Pass mark: {result.passPercent}%. {result.expired ? "Exam expired — scored from saved answers." : ""}</p>
        </div>
      </div>

      <h3>By section</h3>
      <ul className="results__sections">
        {result.sections.map((s) => (
          <li key={s.section}>
            <span>{s.section}</span>
            <div className="results__bar"><div style={{ width: `${s.percentage}%` }} /></div>
            <span>{s.correct}/{s.total}</span>
          </li>
        ))}
      </ul>

      <h3>Review</h3>
      <ul className="results__review">
        {result.review.map((r) => (
          <li key={r.ordinal} className={r.isCorrect ? "review-item review-item--ok" : "review-item review-item--no"}>
            <p><strong>Q{r.ordinal + 1}</strong> {r.text}</p>
            <p className="review-item__line"><span>Your answer:</span> {r.selected.join(", ") || "—"}</p>
            {!r.isCorrect && (
              <p className="review-item__line"><span>Correct:</span> {r.correct.join(", ")}</p>
            )}
            <p className="review-item__exp">{r.explanation}</p>
            {r.source && <a href={r.source} target="_blank" rel="noreferrer">Source ↗</a>}
          </li>
        ))}
      </ul>

      <button className="btn btn--primary" onClick={onBack}>Back to chat</button>
    </div>
  );
}
