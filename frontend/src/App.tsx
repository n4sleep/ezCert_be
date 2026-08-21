import { useCallback, useEffect, useState } from "react";
import AppShell from "./components/AppShell";
import ExamBuilder from "./screens/ExamBuilder";
import ExamTaking from "./screens/ExamTaking";
import ExamResults from "./screens/ExamResults";
import ExamListScreen from "./screens/ExamListScreen";
import ReviewListScreen from "./screens/ReviewListScreen";
import { request } from "./api/client";
import type { AttemptSummary, ExamSummary } from "./types";

const DELETED_KEY = "examgenius.deletedExamIds";

function loadDeleted(): string[] {
  try {
    const raw = localStorage.getItem(DELETED_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === "string") : [];
  } catch {
    return [];
  }
}

type Phase = "chat" | "exam" | "review";

export default function App() {
  const [phase, setPhase] = useState<Phase>("chat");
  const [examId, setExamId] = useState<string | null>(null);
  const [examMode, setExamMode] = useState<"practice" | "certification" | undefined>(undefined);
  const [examDuration, setExamDuration] = useState<number | undefined>(undefined);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [linkError, setLinkError] = useState("");
  const [exams, setExams] = useState<ExamSummary[]>([]);
  const [attempts, setAttempts] = useState<AttemptSummary[]>([]);
  const [deletedIds, setDeletedIds] = useState<string[]>(loadDeleted);
  const [apiDown, setApiDown] = useState(false);
  const [apiNoticeDismissed, setApiNoticeDismissed] = useState(false);

  const refresh = useCallback(() => {
    request<ExamSummary[]>("/api/exams").then(setExams).catch(() => {});
    request<AttemptSummary[]>("/api/me/attempts").then(setAttempts).catch(() => {});
  }, []);

  // Local-only exam hiding (the DB keeps the exam until it expires).
  const visibleExams = exams.filter((e) => !deletedIds.includes(e.examId));

  function deleteExam(examId: string) {
    setDeletedIds((prev) => {
      const next = prev.includes(examId) ? prev : [...prev, examId];
      try {
        localStorage.setItem(DELETED_KEY, JSON.stringify(next));
      } catch {
        /* storage unavailable — hide for this session only */
      }
      return next;
    });
  }

  // Health ping: a dead API must look like "service down", not a fresh install
  // (the exam/attempt lists fail silently by design).
  useEffect(() => {
    let cancelled = false;
    request<{ status: string }>("/api/health")
      .then(() => {
        if (!cancelled) setApiDown(false);
      })
      .catch(() => {
        if (!cancelled) setApiDown(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Load history + resolve shared-exam links on mount.
  useEffect(() => {
    refresh();
    const token = new URLSearchParams(window.location.search).get("take");
    if (!token) return;
    request<{ examId: string; title: string; expiresAt: string }>(`/api/exams/take/${token}`)
      .then((res) => {
        setExamId(res.examId);
        setPhase("exam");
      })
      .catch((e) => setLinkError(e instanceof Error ? e.message : "This exam is unavailable."));
  }, [refresh]);

  function startExam(id: string, mode: "practice" | "certification", durationMinutes?: number) {
    setExamMode(mode);
    setExamDuration(durationMinutes);
    setExamId(id);
    setPhase("exam");
  }

  function openResult(id: string) {
    setAttemptId(id);
    setPhase("review");
  }

  return (
    <AppShell
      active={phase}
      onChat={() => setPhase("chat")}
      onExam={() => setPhase("exam")}
      onReview={() => setPhase("review")}
    >
      {apiDown && !apiNoticeDismissed && (
        <div className="max-w-container-max mx-auto px-lg pt-lg">
          <div className="bg-danger-soft text-danger border border-danger/40 rounded-xl px-4 py-3 flex items-start justify-between gap-2">
            <span>Can't reach the practice server — check your connection and try again.</span>
            <button
              type="button"
              className="opacity-60 hover:opacity-100 text-base leading-none"
              onClick={() => setApiNoticeDismissed(true)}
              aria-label="Dismiss"
            >
              ×
            </button>
          </div>
        </div>
      )}
      {linkError && (
        <div className="max-w-4xl mx-auto p-xl">
          <div className="bg-danger-soft text-danger p-lg rounded-xl">{linkError}</div>
        </div>
      )}
      {phase === "chat" && (
        <ExamBuilder
          exams={visibleExams}
          onStartExam={startExam}
          onGenerated={refresh}
          onDeleteExam={deleteExam}
        />
      )}
      {examId ? (
        <div className={phase === "exam" ? "" : "hidden"} aria-hidden={phase !== "exam"}>
          <ExamTaking
            examId={examId}
            attemptMode={examMode}
            attemptDurationMinutes={examDuration}
            onFinished={(id) => {
              setAttemptId(id);
              setExamId(null);
              setExamMode(undefined);
              setExamDuration(undefined);
              refresh();
              setPhase("review");
            }}
            onAbandon={() => {
              setExamId(null);
              setExamMode(undefined);
              setExamDuration(undefined);
              refresh();
            }}
          />
        </div>
      ) : phase === "exam" ? (
        <ExamListScreen
          exams={visibleExams}
          onStartExam={startExam}
          onDeleteExam={deleteExam}
          onGoChat={() => setPhase("chat")}
        />
      ) : null}
      {phase === "review" && attemptId ? (
        <ExamResults attemptId={attemptId} onBack={() => setAttemptId(null)} />
      ) : phase === "review" ? (
        <ReviewListScreen
          attempts={attempts}
          onOpenResult={openResult}
          onGoChat={() => setPhase("chat")}
        />
      ) : null}
    </AppShell>
  );
}
