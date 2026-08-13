import { useCallback, useEffect, useState } from "react";
import AppShell from "./components/AppShell";
import ExamBuilder from "./screens/ExamBuilder";
import ExamTaking from "./screens/ExamTaking";
import ExamResults from "./screens/ExamResults";
import ExamListScreen from "./screens/ExamListScreen";
import ReviewListScreen from "./screens/ReviewListScreen";
import { request } from "./api/client";
import type { AttemptSummary, ExamSummary } from "./types";

type Phase = "chat" | "exam" | "review";

export default function App() {
  const [phase, setPhase] = useState<Phase>("chat");
  const [examId, setExamId] = useState<string | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [linkError, setLinkError] = useState("");
  const [exams, setExams] = useState<ExamSummary[]>([]);
  const [attempts, setAttempts] = useState<AttemptSummary[]>([]);

  const refresh = useCallback(() => {
    request<ExamSummary[]>("/api/exams").then(setExams).catch(() => {});
    request<AttemptSummary[]>("/api/me/attempts").then(setAttempts).catch(() => {});
  }, []);

  // Load history + resolve shared-exam links on mount.
  useEffect(() => {
    refresh();
    const token = new URLSearchParams(window.location.search).get("take");
    if (!token) return;
    request<{ examId: string; title: string; expiresAt: string }>(`/api/exams/take/${token}`)
      .then((res) => setExamId(res.examId))
      .catch((e) => setLinkError(e instanceof Error ? e.message : "This exam is unavailable."));
  }, [refresh]);

  useEffect(() => {
    if (examId && phase === "chat") setPhase("exam");
  }, [examId, phase]);

  function startExam(id: string) {
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
      {linkError && (
        <div className="max-w-4xl mx-auto p-xl">
          <div className="bg-danger-soft text-danger p-lg rounded-xl">{linkError}</div>
        </div>
      )}
      {phase === "chat" && (
        <ExamBuilder
          exams={exams}
          onStartExam={startExam}
          onGenerated={refresh}
        />
      )}
      {phase === "exam" && examId ? (
        <ExamTaking
          examId={examId}
          onFinished={(id) => {
            setAttemptId(id);
            refresh();
            setPhase("review");
          }}
        />
      ) : (
        <ExamListScreen
          exams={exams}
          onStartExam={startExam}
          onGoChat={() => setPhase("chat")}
        />
      )}
      {phase === "review" && attemptId ? (
        <ExamResults attemptId={attemptId} onBack={() => setPhase("review")} />
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
