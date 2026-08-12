import { useState } from "react";
import AppShell from "./components/AppShell";
import ExamBuilder from "./screens/ExamBuilder";
import ExamTaking from "./screens/ExamTaking";
import ExamResults from "./screens/ExamResults";

type Phase = "chat" | "exam" | "review";

export default function App() {
  const [phase, setPhase] = useState<Phase>("chat");
  const [examId, setExamId] = useState<string | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);

  return (
    <AppShell active={phase}>
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
