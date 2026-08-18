import { useState } from "react";
import type { ExamSummary } from "../types";

interface Props {
  exams: ExamSummary[];
  onStartExam: (examId: string, mode: "practice" | "certification") => void;
  onGoChat: () => void;
}

// Exam tab: list of this device's persisted exams (newest first).
// Mode (practice | certification) is chosen per attempt here.
// Empty state: "Tell us what's on your mind" (EXPERIENCE.md State Patterns).
export default function ExamListScreen({ exams, onStartExam, onGoChat }: Props) {
  if (exams.length === 0) {
    return (
      <div className="max-w-2xl mx-auto p-xl text-center">
        <div className="bg-surface-container-lowest rounded-2xl shadow-md p-xl">
          <div className="w-12 h-12 rounded-full bg-secondary-container text-on-secondary-container grid place-items-center text-xl mx-auto mb-md">✦</div>
          <h2 className="font-headline-lg text-headline-lg text-on-background">Tell us what's on your mind</h2>
          <p className="text-body-md text-on-surface-variant mt-xs mb-lg">
            Ask ExamGenius for a mock exam — e.g. "5 câu hỏi AZ-900 mức Trung bình" — and it will appear here.
          </p>
          <button
            className="inline-flex items-center gap-sm px-xl py-md rounded-lg bg-primary text-on-primary font-label-md shadow-md hover:-translate-y-0.5 transition-transform"
            onClick={onGoChat}
          >
            Ask ExamGenius →
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto p-xl flex flex-col gap-lg">
      <div>
        <h1 className="text-headline-lg font-headline-lg text-on-background">Your exams</h1>
        <p className="text-body-md font-body-md text-on-surface-variant mt-xs">Pick any exam to start or restart it.</p>
      </div>
      {exams.map((e) => (
        <ExamCard key={e.examId} exam={e} onStartExam={onStartExam} />
      ))}
    </div>
  );
}

function ExamCard({ exam: e, onStartExam }: { exam: ExamSummary; onStartExam: Props["onStartExam"] }) {
  const [mode, setMode] = useState<"practice" | "certification">(
    e.mode === "certification" ? "certification" : "practice"
  );

  return (
    <div className={"bg-surface shadow-sm rounded-xl overflow-hidden transition-shadow " + (e.expired ? "opacity-60" : "hover:shadow-md")}>
      <div className="p-lg md:p-xl flex flex-col gap-md">
        <div className="flex-1">
          <div className="flex items-center gap-sm mb-xs">
            <span className="bg-secondary-fixed/50 text-on-secondary-fixed px-2 py-0.5 rounded-full text-[10px] uppercase tracking-wider">
              {e.expired ? "Expired" : e.status}
            </span>
            <span className="text-label-caps text-on-surface-variant">{e.mode === "certification" ? "Mock" : "Practice"}</span>
          </div>
          <h3 className="font-headline-md text-headline-md text-on-surface">{e.title}</h3>
          <p className="text-body-sm text-on-surface-variant mt-xs">
            {e.questionCount} questions · {e.difficulty}
          </p>
        </div>
        <div className="flex flex-col sm:flex-row sm:items-center gap-md sm:justify-between">
          <div className="inline-flex items-center gap-xs bg-surface-container-lowest border border-outline-variant/30 rounded-full p-1 w-fit" role="group" aria-label="Exam mode">
            {(["practice", "certification"] as const).map((m) => (
              <button
                key={m}
                type="button"
                className={
                  "px-md py-xs rounded-full font-label-md transition-colors cursor-pointer " +
                  (mode === m ? "bg-primary text-on-primary" : "text-on-surface-variant hover:text-primary")
                }
                onClick={() => setMode(m)}
              >
                {m === "practice" ? "Practice" : "Mock"}
              </button>
            ))}
          </div>
          <button
            className="shrink-0 bg-primary text-on-primary font-label-md py-md px-lg rounded-lg shadow-[0_4px_14px_rgba(53,37,205,0.2)] hover:-translate-y-0.5 transition-transform disabled:opacity-50 disabled:hover:translate-y-0"
            onClick={() => onStartExam(e.examId, mode)}
            disabled={e.expired}
          >
            Start exam →
          </button>
        </div>
      </div>
    </div>
  );
}
