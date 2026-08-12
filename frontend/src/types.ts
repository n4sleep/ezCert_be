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
