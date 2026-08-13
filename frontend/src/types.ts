// Domain types mirrored from the processor contracts.

export interface ExamJobStatus {
  jobId: string;
  status: "queued" | "running" | "completed" | "failed";
  examId: string | null;
  error: string | null;
  progress: number | null;
}

export interface AttemptQuestionDto {
  attemptQuestionId: string;
  ordinal: number;
  type: "single" | "multi" | "truefalse";
  text: string;
  choices: { label: string; text: string }[];
}

export interface AttemptDto {
  attemptId: string;
  status: string;
  questions: AttemptQuestionDto[];
}

export interface AnswerResult {
  attemptQuestionId: string;
  isCorrect: boolean | null;
  correct: string[] | null;
  explanation: string | null;
  source: string | null;
}

export interface ReviewItem {
  ordinal: number;
  text: string;
  selected: string[];
  correct: string[];
  isCorrect: boolean;
  explanation: string;
  source: string | null;
}

export interface SectionScoreDto {
  section: string;
  total: number;
  correct: number;
  percentage: number;
}

export interface AttemptResult {
  attemptId: string;
  totalQuestions: number;
  correctCount: number;
  scorePercent: number;
  passed: boolean;
  passPercent: number;
  expired: boolean;
  sections: SectionScoreDto[];
  review: ReviewItem[];
}

// Exam list entry (GET /api/exams) — persisted per-device history.
export interface ExamSummary {
  examId: string;
  title: string;
  mode: string;
  difficulty: string;
  status: string;
  questionCount: number;
  expiresAt: string;
  createdAt: string;
}

// Attempt history entry (GET /api/me/attempts) — newest first.
export interface AttemptSummary {
  attemptId: string;
  examId: string;
  title: string;
  status: string;
  scorePercent: number;
  passed: boolean;
  startedAt: string;
}
