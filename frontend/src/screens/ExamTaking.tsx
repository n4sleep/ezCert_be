import { useEffect, useRef, useState } from "react";
import { request } from "../api/client";
import type { AnswerResult, AttemptDto, AttemptQuestionDto } from "../types";
import { useToasts } from "../components/Toast";
import ConfirmDialog from "../components/ConfirmDialog";

interface Props {
  examId: string;
  attemptMode?: "practice" | "certification";
  attemptDurationMinutes?: number;
  onFinished: (attemptId: string) => void;
  onAbandon: () => void;
}

function formatClock(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const mm = String(m).padStart(2, "0");
  const ss = String(sec).padStart(2, "0");
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
}

export default function ExamTaking({ examId, attemptMode, attemptDurationMinutes, onFinished, onAbandon }: Props) {
  const [attempt, setAttempt] = useState<AttemptDto | null>(null);
  const [index, setIndex] = useState(0);
  const [view, setView] = useState<"single" | "long">("single");
  const [selections, setSelections] = useState<Record<string, string[]>>({});
  const [revealed, setRevealed] = useState<Record<string, AnswerResult>>({});
  const [error, setError] = useState("");
  const [checking, setChecking] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [confirmAbandon, setConfirmAbandon] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  const autoSubmitted = useRef(false);
  const { push } = useToasts();

  useEffect(() => {
    const params = new URLSearchParams();
    if (attemptMode) params.set("mode", attemptMode);
    if (attemptDurationMinutes) params.set("durationMinutes", String(attemptDurationMinutes));
    const qs = params.toString() ? `?${params.toString()}` : "";
    request<AttemptDto>(`/api/exams/${examId}/attempts${qs}`, { method: "POST" })
      .then(setAttempt)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to start exam"));
  }, [examId, attemptMode, attemptDurationMinutes]);

  // Timer tick (drift-free: recompute from timestamps each second).
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  const current = attempt?.questions[index];
  const isLast = attempt !== null && index === attempt.questions.length - 1;
  const isCert = attempt?.mode === "certification";

  const answeredCount = Object.values(selections).filter((s) => s.length > 0).length;
  const allAnswered = attempt !== null && answeredCount >= attempt.questions.length;
  const progress = attempt ? Math.round((answeredCount / attempt.questions.length) * 100) : 0;

  const startedAt = attempt ? Date.parse(attempt.startedAt) : 0;
  const expiresAt = attempt?.expiresAt ? Date.parse(attempt.expiresAt) : null;
  const elapsedSeconds = startedAt ? (now - startedAt) / 1000 : 0;
  const remainingSeconds = expiresAt !== null ? (expiresAt - now) / 1000 : null;

  // Certification time-up: auto-submit and capture the review.
  useEffect(() => {
    if (!attempt || isCert !== true || expiresAt === null || autoSubmitted.current) return;
    if (remainingSeconds !== null && remainingSeconds <= 0) {
      autoSubmitted.current = true;
      push("info", "Time's up — your exam was submitted");
      void doSubmit();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remainingSeconds, attempt, isCert, expiresAt]);

  function requestExit() {
    if (answeredCount > 0) {
      setConfirmAbandon(true);
    } else {
      onAbandon();
    }
  }

  function recordAnswer(questionId: string, selected: string[]) {
    if (!attempt) return;
    request(`/api/attempts/${attempt.attemptId}/answers`, {
      method: "POST",
      body: { attemptQuestionId: questionId, selected },
    }).catch(() => push("error", "Couldn't save your answer — check your connection."));
  }

  function toggle(q: AttemptQuestionDto, choiceId: string) {
    const qid = q.attemptQuestionId;
    const prev = selections[qid] ?? [];
    const next = q.type === "multi"
      ? prev.includes(choiceId)
        ? prev.filter((c) => c !== choiceId)
        : [...prev, choiceId]
      : [choiceId];
    setSelections((s) => ({ ...s, [qid]: next }));
    if (next.length > 0) recordAnswer(qid, next);
    // A changed pick invalidates a previous reveal so the user can re-check.
    setRevealed((r) => {
      if (!r[qid]) return r;
      const { [qid]: _drop, ...rest } = r;
      return rest;
    });
  }

  async function checkFor(q: AttemptQuestionDto) {
    if (checking || isCert) return;
    setChecking(true);
    try {
      const res = await request<AnswerResult>(`/api/attempts/${attempt!.attemptId}/answers`, {
        method: "POST",
        body: { attemptQuestionId: q.attemptQuestionId, selected: selections[q.attemptQuestionId] ?? [] },
      });
      setRevealed((r) => ({ ...r, [q.attemptQuestionId]: res }));
    } catch (e) {
      push("error", e instanceof Error ? e.message : "Couldn't check the answer");
    } finally {
      setChecking(false);
    }
  }

  function next() {
    if (index < (attempt?.questions.length ?? 1) - 1) setIndex((i) => i + 1);
  }

  function previous() {
    setIndex((i) => Math.max(0, i - 1));
  }

  async function doSubmit() {
    if (!attempt || submitting) return;
    setSubmitting(true);
    try {
      const res = await request<{ attemptId: string }>(`/api/attempts/${attempt.attemptId}/submit`, { method: "POST" });
      push("success", "Exam submitted");
      onFinished(res.attemptId);
    } catch (e) {
      push("error", e instanceof Error ? e.message : "Submit failed — your attempt is still in progress");
      setSubmitting(false);
      setConfirmSubmit(false);
      autoSubmitted.current = false;
    }
  }

  if (error)
    return (
      <div className="max-w-4xl mx-auto p-xl">
        <div className="bg-danger-soft text-danger p-lg rounded-xl">{error}</div>
      </div>
    );
  if (!attempt) return <div className="max-w-4xl mx-auto p-xl text-on-surface-variant">Loading exam…</div>;
  if (!current) return null;

  const selected = selections[current.attemptQuestionId] ?? [];
  const rev = revealed[current.attemptQuestionId];
  const isAnswered = selected.length > 0;
  const showReveal = !isCert && rev?.isCorrect !== null && rev?.isCorrect !== undefined;
  const timeUp = remainingSeconds !== null && remainingSeconds <= 0;

  // Shared per-question card for both views (state comes from selections/revealed).
  function renderQuestion(q: AttemptQuestionDto, qIndex: number) {
    const qSelected = selections[q.attemptQuestionId] ?? [];
    const qRev = revealed[q.attemptQuestionId];
    const qAnswered = qSelected.length > 0;
    const qIsMulti = q.type === "multi";
    const showReveal = !isCert && qRev?.isCorrect !== null && qRev?.isCorrect !== undefined;

    return (
      <div key={q.attemptQuestionId} className="bg-surface-container-lowest rounded-xl p-xl shadow-md w-full relative overflow-hidden">
        <div className="absolute -top-20 -right-20 w-64 h-64 bg-primary/5 rounded-full blur-3xl pointer-events-none" />
        <div className="flex items-start gap-md mb-lg">
          <div
            className={
              "w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 font-headline-md shadow-sm " +
              (qAnswered ? "bg-primary text-on-primary" : "bg-primary-container text-on-primary ring-1 ring-outline-variant/40")
            }
          >
            {qIndex + 1}
          </div>
          <div className="flex flex-col gap-sm pt-xs">
            <h2 className="font-headline-lg text-headline-lg text-on-surface leading-tight">{q.text}</h2>
            {qIsMulti && <p className="font-body-sm text-on-surface-variant">Select all that apply.</p>}
          </div>
        </div>

        {/* Options */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-md">
          {q.choices.map((c) => {
            const picked = qSelected.includes(c.label);
            const isCorrectChoice = showReveal ? qRev?.correct?.includes(c.label) : undefined;
            const isWrongPick = showReveal ? qRev && picked && !qRev.correct?.includes(c.label) : undefined;

            let cls = "flex items-center p-md rounded-xl shadow-sm transition-all duration-200 w-full text-left ";
            let labelCls = "w-8 h-8 rounded-full border-2 border-outline flex items-center justify-center font-label-md text-on-surface-variant mr-md ";
            if (showReveal) {
              if (isCorrectChoice) {
                cls += "bg-success-soft ring-2 ring-success-strong shadow-[0_8px_20px_rgba(16,185,129,0.15)]";
                labelCls = "w-8 h-8 rounded-full bg-success-strong text-white flex items-center justify-center font-label-md mr-md shadow-sm ";
              } else if (isWrongPick) {
                cls += "bg-danger-soft ring-2 ring-danger shadow-[0_8px_20px_rgba(198,40,40,0.15)]";
                labelCls = "w-8 h-8 rounded-full bg-danger text-white flex items-center justify-center font-label-md mr-md shadow-sm ";
              } else {
                cls += "bg-surface-container-lowest hover:shadow-md";
              }
            } else if (picked) {
              cls += "bg-secondary-container rounded-xl shadow-[0_8px_20px_rgba(79,70,229,0.15)] ring-2 ring-primary";
              labelCls = "w-8 h-8 rounded-full bg-primary text-on-primary flex items-center justify-center font-label-md mr-md shadow-sm ";
            } else {
              cls += "bg-surface-container-lowest hover:shadow-md";
            }

            return (
              <button
                key={c.label}
                className={cls}
                onClick={() => toggle(q, c.label)}
                aria-pressed={picked}
              >
                <div className={labelCls}>{c.label}</div>
                <span className={"font-body-lg flex-grow " + (picked ? "font-semibold" : "")}>{c.text}</span>
                {picked && !showReveal && <span className="text-primary">●</span>}
              </button>
            );
          })}
        </div>

        {!isCert && qRev && (
          <div
            className={
              "mt-lg rounded-xl p-lg " +
              (qRev.isCorrect ? "bg-success-soft border border-success-strong/40" : "bg-danger-soft border border-danger/40")
            }
          >
            <h4 className="font-label-md font-bold mb-sm">{qRev.isCorrect ? "✓ Correct" : "✕ Not quite"}</h4>
            <p className="font-body-sm text-on-surface-variant">{qRev.explanation}</p>
            {qRev.source && (
              <a href={qRev.source} target="_blank" rel="noreferrer" className="inline-block mt-sm text-primary font-label-md">
                Read the source →
              </a>
            )}
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="flex flex-col min-h-[calc(100vh-5rem)] py-xl px-md md:px-xxl gap-xl max-w-container-max mx-auto">
      {/* Top info bar */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center bg-surface-container rounded-xl p-lg shadow-sm gap-md w-full">
        <div className="flex flex-col gap-xs">
          <button
            type="button"
            className="w-fit inline-flex items-center gap-xs text-danger hover:opacity-80 transition-opacity mb-xs cursor-pointer"
            onClick={requestExit}
          >
            <span>←</span>
            <span className="text-label-md font-label-md">Exit exam</span>
          </button>
          <span className="font-label-caps text-label-caps text-on-surface-variant uppercase tracking-wider">Current Exam</span>
          <h1 className="font-headline-md text-headline-md text-on-surface">{attempt.title || "Practice Exam"}</h1>
        </div>
        <div className="flex items-center gap-xl flex-wrap">
          <div className="inline-flex items-center gap-xs bg-surface-container-lowest border border-outline-variant/30 rounded-full p-1" role="group" aria-label="View">
            {(["single", "long"] as const).map((v) => (
              <button
                key={v}
                type="button"
                className={
                  "px-md py-xs rounded-full font-label-md transition-colors cursor-pointer " +
                  (view === v ? "bg-primary text-on-primary" : "text-on-surface-variant hover:text-primary")
                }
                onClick={() => setView(v)}
              >
                {v === "single" ? "Single" : "Long"}
              </button>
            ))}
          </div>
          <div
            className={
              "flex items-center gap-sm px-md py-sm rounded-full border " +
              (remainingSeconds !== null && remainingSeconds < 60
                ? "bg-danger-soft text-danger border-danger/40"
                : "bg-surface-container-lowest text-on-surface border-outline-variant/30")
            }
          >
            <span aria-hidden>⏱</span>
            <span className="font-label-md text-label-md font-bold tabular-nums">
              {remainingSeconds !== null ? formatClock(remainingSeconds) : formatClock(elapsedSeconds)}
            </span>
            <span className="font-label-caps text-[10px] uppercase tracking-wider opacity-70">
              {remainingSeconds !== null ? "remaining" : "elapsed"}
            </span>
          </div>
          {view === "single" && (
            <div className="flex items-center gap-sm bg-surface-container-lowest px-md py-sm rounded-full border border-outline-variant/30">
              <span className="font-label-md text-label-md font-bold tabular-nums text-on-surface">
                Question {index + 1} of {attempt.questions.length}
              </span>
            </div>
          )}
          <div className="flex flex-col gap-xs min-w-[150px]">
            <div className="flex justify-between w-full">
              <span className="font-label-caps text-label-caps text-on-surface-variant">Progress</span>
              <span className="font-label-caps text-label-caps text-on-surface-variant font-bold">{progress}%</span>
            </div>
            <div className="w-full bg-outline-variant rounded-full h-2 overflow-hidden">
              <div className="bg-primary h-full rounded-full transition-all duration-500" style={{ width: `${progress}%` }} />
            </div>
          </div>
        </div>
      </div>

      {view === "single" ? (
        <>
          {/* Question card */}
          <div className="flex flex-col max-w-4xl mx-auto w-full gap-xl">
            {renderQuestion(current, index)}
          </div>

          {/* Footer nav — always free in both modes */}
          <div className="mt-auto pt-xl flex flex-col sm:flex-row justify-between items-center gap-md max-w-4xl mx-auto w-full pb-xl">
            <div className="flex gap-md w-full sm:w-auto justify-between sm:justify-start">
              <button
                className="flex items-center justify-center px-lg py-md rounded-lg font-label-md text-secondary hover:text-primary min-w-[120px] disabled:opacity-40 disabled:hover:text-secondary"
                onClick={previous}
                disabled={index === 0}
              >
                ← Previous
              </button>
              {!isCert && !showReveal && (
                <button
                  className="flex items-center justify-center px-lg py-md rounded-lg bg-surface-container-high font-label-md text-on-surface min-w-[120px] disabled:opacity-50"
                  onClick={() => checkFor(current)}
                  disabled={!isAnswered || checking}
                  aria-busy={checking}
                >
                  {checking ? "Checking…" : "Check answer"}
                </button>
              )}
            </div>
            <div className="flex gap-md w-full sm:w-auto justify-between sm:justify-end">
              <button
                className="flex items-center justify-center px-lg py-md rounded-lg bg-primary text-on-primary font-label-md min-w-[120px] disabled:opacity-40"
                onClick={next}
                disabled={isLast}
              >
                Next →
              </button>
              <button
                className={
                  "flex items-center justify-center px-xl py-md rounded-lg font-label-md shadow-md transition-all disabled:opacity-50 " +
                  (allAnswered
                    ? "bg-success text-white hover:bg-[#059669]"
                    : "bg-surface-container-high text-on-surface-variant")
                }
                onClick={() => setConfirmSubmit(true)}
                disabled={submitting || !allAnswered}
                title={allAnswered ? "Submit exam" : "Answer all questions to submit"}
              >
                Submit Exam
              </button>
            </div>
          </div>
        </>
      ) : (
        <>
          {/* Long view: all questions */}
          <div className="flex flex-col gap-xl max-w-4xl mx-auto w-full">
            {attempt.questions.map((q, i) => renderQuestion(q, i))}
          </div>
          <div className="sticky bottom-0 pt-xl pb-xl max-w-4xl mx-auto w-full flex justify-center bg-gradient-to-t from-surface via-surface/95 to-transparent">
            <button
              className={
                "flex items-center justify-center px-xl py-md rounded-lg font-label-md shadow-md transition-all disabled:opacity-50 " +
                (allAnswered
                  ? "bg-success text-white hover:bg-[#059669]"
                  : "bg-surface-container-high text-on-surface-variant")
              }
              onClick={() => setConfirmSubmit(true)}
              disabled={submitting || !allAnswered}
              title={allAnswered ? "Submit exam" : "Answer all questions to submit"}
            >
              Submit Exam
            </button>
          </div>
        </>
      )}

      {confirmSubmit && (
        <ConfirmDialog
          title="Submit exam?"
          body="You can't change your answers after submitting. Your score will be saved to this browser's history."
          confirmLabel="Submit"
          busy={submitting}
          onConfirm={doSubmit}
          onCancel={() => setConfirmSubmit(false)}
        />
      )}

      {confirmAbandon && (
        <ConfirmDialog
          title="Exit exam?"
          body="This attempt won't be saved and won't appear in your results. You can start the exam again anytime."
          confirmLabel="Exit"
          busy={false}
          onConfirm={() => {
            setConfirmAbandon(false);
            onAbandon();
          }}
          onCancel={() => setConfirmAbandon(false)}
        />
      )}

      {timeUp && <div className="sr-only" role="status">Time's up — submitting your exam.</div>}
    </div>
  );
}
