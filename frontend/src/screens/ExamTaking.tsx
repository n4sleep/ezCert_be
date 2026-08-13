import { useEffect, useState } from "react";
import { request } from "../api/client";
import type { AnswerResult, AttemptDto } from "../types";
import { useToasts } from "../components/Toast";
import ConfirmDialog from "../components/ConfirmDialog";

interface Props {
  examId: string;
  onFinished: (attemptId: string) => void;
}

export default function ExamTaking({ examId, onFinished }: Props) {
  const [attempt, setAttempt] = useState<AttemptDto | null>(null);
  const [index, setIndex] = useState(0);
  const [selected, setSelected] = useState<string[]>([]);
  const [revealed, setRevealed] = useState<Record<string, AnswerResult>>({});
  const [error, setError] = useState("");
  const [checking, setChecking] = useState(false);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const { push } = useToasts();

  useEffect(() => {
    request<AttemptDto>(`/api/exams/${examId}/attempts`, { method: "POST" })
      .then(setAttempt)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to start exam"));
  }, [examId]);

  const current = attempt?.questions[index];
  const isLast = attempt !== null && index === attempt.questions.length - 1;
  const isMulti = current?.type === "multi";
  const rev = current ? revealed[current.attemptQuestionId] : undefined;
  const progress = attempt ? Math.round(((index + 1) / attempt.questions.length) * 100) : 0;

  function toggle(choiceId: string) {
    if (isMulti) {
      setSelected((s) => (s.includes(choiceId) ? s.filter((c) => c !== choiceId) : [...s, choiceId]));
    } else {
      setSelected([choiceId]);
    }
  }

  async function check() {
    if (!current || checking) return;
    setChecking(true);
    try {
      const res = await request<AnswerResult>(`/api/attempts/${attempt!.attemptId}/answers`, {
        method: "POST",
        body: { attemptQuestionId: current.attemptQuestionId, selected },
      });
      setRevealed((r) => ({ ...r, [current.attemptQuestionId]: res }));
    } catch (e) {
      push("error", e instanceof Error ? e.message : "Couldn't check the answer");
    } finally {
      setChecking(false);
    }
  }

  function next() {
    setSelected([]);
    setIndex((i) => i + 1);
  }

  function previous() {
    setSelected([]);
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

  return (
    <div className="flex flex-col min-h-[calc(100vh-5rem)] py-xl px-md md:px-xxl gap-xl max-w-container-max mx-auto">
      {/* Top info bar */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center bg-surface-container rounded-xl p-lg shadow-sm gap-md w-full">
        <div className="flex flex-col gap-xs">
          <span className="font-label-caps text-label-caps text-on-surface-variant uppercase tracking-wider">Current Exam</span>
          <h1 className="font-headline-md text-headline-md text-on-surface">AZ-900 Cloud Concepts Practice</h1>
        </div>
        <div className="flex items-center gap-xl">
          <div className="flex items-center gap-sm bg-surface-container-lowest px-md py-sm rounded-full border border-outline-variant/30">
            <span className="font-label-md text-label-md font-bold tabular-nums text-on-surface">
              Question {index + 1} of {attempt.questions.length}
            </span>
          </div>
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

      {/* Question card */}
      <div className="flex flex-col max-w-4xl mx-auto w-full gap-xl">
        <div className="bg-surface-container-lowest rounded-xl p-xl shadow-md w-full relative overflow-hidden">
          <div className="absolute -top-20 -right-20 w-64 h-64 bg-primary/5 rounded-full blur-3xl pointer-events-none" />
          <div className="flex items-start gap-md mb-lg">
            <div className="w-10 h-10 rounded-full bg-primary-container text-on-primary flex items-center justify-center flex-shrink-0 font-headline-md shadow-sm">
              {index + 1}
            </div>
            <div className="flex flex-col gap-sm pt-xs">
              <h2 className="font-headline-lg text-headline-lg text-on-surface leading-tight">{current.text}</h2>
              {isMulti && <p className="font-body-sm text-on-surface-variant">Select all that apply.</p>}
            </div>
          </div>

          {/* Options */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-md">
            {current.choices.map((c) => {
              const picked = selected.includes(c.label);
              const isCorrectChoice = rev?.correct?.includes(c.label);
              const isWrongPick = rev && picked && !rev.correct?.includes(c.label);

              let cls = "flex items-center p-md rounded-xl shadow-sm transition-all duration-200 w-full text-left ";
              let labelCls = "w-8 h-8 rounded-full border-2 border-outline flex items-center justify-center font-label-md text-on-surface-variant mr-md ";
              if (rev) {
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
                  onClick={() => toggle(c.label)}
                  disabled={!!rev}
                  aria-pressed={picked}
                  title={rev ? "Locked after checking" : undefined}
                >
                  <div className={labelCls}>{c.label}</div>
                  <span className={"font-body-lg flex-grow " + (picked ? "font-semibold" : "")}>{c.text}</span>
                  {picked && !rev && <span className="text-primary">●</span>}
                </button>
              );
            })}
          </div>
        </div>

        {rev && (
          <div
            className={
              "rounded-xl p-lg " +
              (rev.isCorrect ? "bg-success-soft border border-success-strong/40" : "bg-danger-soft border border-danger/40")
            }
          >
            <h4 className="font-label-md font-bold mb-sm">{rev.isCorrect ? "✓ Correct" : "✕ Not quite"}</h4>
            <p className="font-body-sm text-on-surface-variant">{rev.explanation}</p>
            {rev.source && (
              <a href={rev.source} target="_blank" rel="noreferrer" className="inline-block mt-sm text-primary font-label-md">
                Read the source →
              </a>
            )}
          </div>
        )}
      </div>

      {/* Footer nav */}
      <div className="mt-auto pt-xl flex flex-col sm:flex-row justify-between items-center gap-md max-w-4xl mx-auto w-full pb-xl">
        <div className="flex gap-md w-full sm:w-auto justify-between sm:justify-start">
          <button
            className="flex items-center justify-center px-lg py-md rounded-lg font-label-md text-secondary hover:text-primary min-w-[120px] disabled:opacity-40 disabled:hover:text-secondary"
            onClick={previous}
            disabled={index === 0}
          >
            ← Previous
          </button>
          {!rev && (
            <button
              className="flex items-center justify-center px-lg py-md rounded-lg bg-surface-container-high font-label-md text-on-surface min-w-[120px] disabled:opacity-50"
              onClick={check}
              disabled={selected.length === 0 || checking}
              aria-busy={checking}
            >
              {checking ? "Checking…" : "Check answer"}
            </button>
          )}
        </div>
        {rev &&
          (isLast ? (
            <button
              className="w-full sm:w-auto flex items-center justify-center px-xl py-md rounded-lg bg-success text-white font-label-md shadow-md hover:bg-[#059669] transition-all"
              onClick={() => setConfirmSubmit(true)}
            >
              Submit Exam →
            </button>
          ) : (
            <button className="w-full sm:w-auto flex items-center justify-center px-xl py-md rounded-lg bg-primary text-on-primary font-label-md shadow-md" onClick={next}>
              Next →
            </button>
          ))}
      </div>

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
    </div>
  );
}
