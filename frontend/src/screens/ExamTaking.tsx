import { useEffect, useState } from "react";
import { request } from "../api/client";
import type { AnswerResult, AttemptDto } from "../types";

interface Props {
  examId: string;
  onFinished: (attemptId: string) => void;
  onBack: () => void;
}

export default function ExamTaking({ examId, onFinished, onBack }: Props) {
  const [attempt, setAttempt] = useState<AttemptDto | null>(null);
  const [index, setIndex] = useState(0);
  const [selected, setSelected] = useState<string[]>([]);
  const [revealed, setRevealed] = useState<Record<string, AnswerResult>>({});
  const [error, setError] = useState("");

  useEffect(() => {
    request<AttemptDto>(`/api/exams/${examId}/attempts`, { method: "POST" })
      .then(setAttempt)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to start exam"));
  }, [examId]);

  const current = attempt?.questions[index];
  const isLast = attempt !== null && index === attempt.questions.length - 1;
  const isMulti = current?.type === "multi";

  function toggle(choiceId: string) {
    if (isMulti) {
      setSelected((s) => (s.includes(choiceId) ? s.filter((c) => c !== choiceId) : [...s, choiceId]));
    } else {
      setSelected([choiceId]);
    }
  }

  async function check() {
    if (!current) return;
    const res = await request<AnswerResult>(`/api/attempts/${attempt!.attemptId}/answers`, {
      method: "POST",
      body: { attemptQuestionId: current.attemptQuestionId, selected },
    });
    setRevealed((r) => ({ ...r, [current.attemptQuestionId]: res }));
  }

  function next() {
    setSelected([]);
    setIndex((i) => i + 1);
  }

  async function submit() {
    if (!attempt) return;
    const res = await request<{ attemptId: string }>(`/api/attempts/${attempt.attemptId}/submit`, { method: "POST" });
    onFinished(res.attemptId);
  }

  if (error) return <div className="bubble bubble--error">{error}</div>;
  if (!attempt) return <p>Loading exam…</p>;
  if (!current) return null;

  const rev = revealed[current.attemptQuestionId];
  const practice = true; // v1 slice: practice mode

  return (
    <div className="exam">
      <div className="exam__bar">
        <span className="exam__counter">
          Question {index + 1} of {attempt.questions.length}
        </span>
        <button className="btn btn--ghost" onClick={onBack}>Back</button>
      </div>

      <h2 className="exam__question">{current.text}</h2>
      {isMulti && <p className="exam__hint">Select all that apply.</p>}

      <div className="exam__choices">
        {current.choices.map((c) => {
          const picked = selected.includes(c.label);
          const isCorrectChoice = rev?.correct?.includes(c.label);
          const isWrongPick = rev && picked && !rev.correct?.includes(c.label);
          let cls = "exam-option";
          if (rev) {
            if (isCorrectChoice) cls += " exam-option--correct";
            else if (isWrongPick) cls += " exam-option--wrong";
          } else if (picked) cls += " exam-option--picked";
          return (
            <button key={c.label} className={cls} onClick={() => toggle(c.label)} disabled={!!rev}>
              <span className="exam-option__label">{c.label}</span>
              <span>{c.text}</span>
            </button>
          );
        })}
      </div>

      {rev && (
        <div className="feedback">
          <strong>{rev.isCorrect ? "Correct" : "Not quite"}</strong>
          <p>{rev.explanation}</p>
          {rev.source && <a href={rev.source} target="_blank" rel="noreferrer">Source ↗</a>}
        </div>
      )}

      <div className="exam__actions">
        {practice && !rev ? (
          <button className="btn btn--primary" onClick={check} disabled={selected.length === 0}>
            Check answer
          </button>
        ) : isLast ? (
          <button className="btn btn--success" onClick={submit}>Submit exam</button>
        ) : (
          <button className="btn btn--primary" onClick={next}>Next question</button>
        )}
      </div>
    </div>
  );
}
