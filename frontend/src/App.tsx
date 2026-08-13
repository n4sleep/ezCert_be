import { useEffect, useState } from "react";
import AppShell from "./components/AppShell";
import ExamBuilder from "./screens/ExamBuilder";
import ExamTaking from "./screens/ExamTaking";
import ExamResults from "./screens/ExamResults";
import { request } from "./api/client";

type Phase = "chat" | "exam" | "review";

export default function App() {
  const [phase, setPhase] = useState<Phase>("chat");
  const [examId, setExamId] = useState<string | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [linkError, setLinkError] = useState("");

  // Shared-exam link: /?take={shareToken} -> resolve the exam and start it.
  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get("take");
    if (!token) return;
    request<{ examId: string; title: string; expiresAt: string }>(`/api/exams/take/${token}`)
      .then((res) => setExamId(res.examId))
      .catch((e) => setLinkError(e instanceof Error ? e.message : "This exam is unavailable."));
  }, []);

  useEffect(() => {
    if (examId && phase === "chat") setPhase("exam");
  }, [examId, phase]);

  return (
    <AppShell active={phase}>
      {linkError && (
        <div className="max-w-4xl mx-auto p-xl">
          <div className="bg-danger-soft text-danger p-lg rounded-xl">{linkError}</div>
        </div>
      )}
      {phase === "chat" && (
        <ExamBuilder onStartExam={(id) => { setExamId(id); setPhase("exam"); }} />
      )}
      {phase === "exam" && examId && (
        <ExamTaking
          examId={examId}
          onFinished={(id) => { setAttemptId(id); setPhase("review"); }}
        />
      )}
      {phase === "review" && attemptId && (
        <ExamResults attemptId={attemptId} onBack={() => { setPhase("chat"); setExamId(null); }} />
      )}
    </AppShell>
  );
}
