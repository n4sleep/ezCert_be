import { useState } from "react";
import ExamBuilder from "./screens/ExamBuilder";
import ExamTaking from "./screens/ExamTaking";
import ExamResults from "./screens/ExamResults";

type Phase = "builder" | "exam" | "results";

export default function App() {
  const [phase, setPhase] = useState<Phase>("builder");
  const [examId, setExamId] = useState<string | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);

  return (
    <div className="app">
      <header className="app__header">
        <span className="app__logo">◆</span>
        <span className="app__title">ezCert</span>
        <span className="app__guest">Guest</span>
      </header>

      <main className="app__main">
        {phase === "builder" && (
          <ExamBuilder
            onStartExam={(id) => { setExamId(id); setPhase("exam"); }}
          />
        )}
        {phase === "exam" && examId && (
          <ExamTaking
            examId={examId}
            onFinished={(id) => { setAttemptId(id); setPhase("results"); }}
            onBack={() => setPhase("builder")}
          />
        )}
        {phase === "results" && attemptId && (
          <ExamResults attemptId={attemptId} onBack={() => { setPhase("builder"); setExamId(null); }} />
        )}
      </main>
    </div>
  );
}
