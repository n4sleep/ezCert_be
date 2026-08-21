import { useRef, useState } from "react";
import { request } from "../api/client";
import type { ExamJobStatus, ExamSummary } from "../types";
import { useToasts } from "../components/Toast";
import ExamConfigCard from "../components/ExamConfigCard";
import type { ExamGenConfig } from "../components/ExamConfigCard";
import DeleteExamDialog from "../components/DeleteExamDialog";

interface Props {
  exams: ExamSummary[];
  onStartExam: (examId: string, mode: "practice" | "certification") => void;
  onGenerated: () => void;
  onDeleteExam: (examId: string) => void;
}

interface FeedItem {
  id: number;
  kind: "user" | "bot" | "exam" | "error" | "prompt";
  text?: string;
  examId?: string;
  options?: string[];
}

let nextId = 1;

const PURPOSE_OPTIONS = ["AZ-900 — Azure Fundamentals", "CLF-C02 — AWS Cloud Practitioner", "Something else — I'll type it myself"];

interface ChatRule {
  re: RegExp;
  reply: (input: string) => string;
}

function cleanGreeting(input: string): string {
  return input.trim().replace(/[.!?]+$/, "");
}

// Ordered rules for topic-less chat. First match wins.
const CHAT_RULES: ChatRule[] = [
  { re: /^(hi+|hello+|hey+)(\s+(there|guys|everyone))?[.!?]*$/i, reply: (i) => `${cleanGreeting(i)}. What would you like to practice?` },
  { re: /^(good\s+(morning|afternoon|evening)|gm|gn)[.!?]*$/i, reply: () => "Good to see you! Ready to practice? Pick an exam to get started." },
  { re: /^(how are you|hi how are you|how r u|how's it going)[.?]*$/i, reply: () => "I'm feeling genius, let's practice!" },
  { re: /^(thanks|thank you|ty|thx)(\s+(a lot|so much|very much))?[.!?]*$/i, reply: (i) => (/a lot|so much|very much/i.test(i) ? "I'm glad I can help you" : "You're welcome! Whenever you're ready, tell me what to practice.") },
  { re: /^(help|i need help|help me)[.!?]*$/i, reply: () => "I'm here to help you practice. I create mock exams for AZ-900 or CLF-C02 — or from your own documents and links. What would you like?" },
  { re: /^(what can you do|what do you do)[.!?]*$/i, reply: () => "I support you to create Exam however you like" },
  { re: /^(how does this work|how to use|how do i use|how it works)[.!?]*$/i, reply: () => "Simple: tell me the exam topic (or attach your notes/links), adjust questions & duration, and I'll generate a practice exam you can take and review." },
  { re: /^(who are you|what's your name|whats your name)[.!?]*$/i, reply: () => "I'm ExamGenius - your study companion" },
  { re: /^(test|testing|testing 1 2 3)[.!?]*$/i, reply: () => "Oops, system is breaking down. Test fail!" },
  { re: /^\?+$/i, reply: () => "?" },
  { re: /^[12]$/, reply: () => "If you're picking from my suggestions, click one below — or type the topic you want." },
  { re: /^(ok|okay|ok got it|alright|sure)[.!?]*$/i, reply: () => "Great — what shall we practice? Pick an exam below or type it." },
  { re: /^(yes|no|yeah|nope|yep|nah)[.!?]*$/i, reply: () => "ok!" },
  { re: /^(what|what\?+)$/i, reply: () => "what?" },
  { re: /^(hello\?+|are you there|you there|anyone there)[.!?]*$/i, reply: () => "No i'm not here at all!!" },
  { re: /^(nothing|nevermind|never mind|forget it|skip)[.!?]*$/i, reply: () => "No problem! Whenever you're ready, I'll be here to make you an exam." },
  { re: /^(lol|haha|hahaha|cool|nice|wow|great|awesome|nice one)[.!?]*$/i, reply: () => "Glad you like it! When you're ready to practice, tell me the topic." },
];

function isCertTopic(text: string): boolean {
  return /\b(AZ-900|CLF-C02|AI-900|DP-900)\b/i.test(text);
}

// Returns a reply when the message is a greeting/no-topic pattern; null = proceed.
function topicLessReply(text: string): string | null {
  const trimmed = text.trim();
  for (const rule of CHAT_RULES) {
    if (rule.re.test(trimmed)) return rule.reply(trimmed);
  }
  // Fallback: very short, no cert code, no obvious subject.
  const words = trimmed.split(/\s+/).filter(Boolean);
  if (words.length <= 3 && trimmed.length <= 24 && !isCertTopic(trimmed)) return "What's on your mind?";
  return null;
}

export default function ExamBuilder({ exams, onStartExam, onGenerated, onDeleteExam }: Props) {
  const [feed, setFeed] = useState<FeedItem[]>([]);
  const [prompt, setPrompt] = useState("");
  const [busy, setBusy] = useState(false);
  const [stage, setStage] = useState<string | null>(null);
  const [showConfig, setShowConfig] = useState(false);
  const [shareFor, setShareFor] = useState<string | null>(null);
  const [shareUrl, setShareUrl] = useState("");
  const [shareCopied, setShareCopied] = useState(false);
  const [sharingId, setSharingId] = useState<string | null>(null);
  const [cardModes, setCardModes] = useState<Record<string, "practice" | "certification">>({});
  const [deleteFor, setDeleteFor] = useState<{ examId: string; label: string; count: number } | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const feedRef = useRef<HTMLDivElement>(null);
  const { push } = useToasts();

  function newExam() {
    setFeed([]);
    setPrompt("");
    setBusy(false);
    setShowConfig(false);
    setShareFor(null);
    inputRef.current?.focus();
    feedRef.current?.scrollTo({ top: 0 });
    push("info", "New conversation started");
  }

  async function share(examId: string) {
    setSharingId(examId);
    try {
      const res = await request<{ shareToken: string; url: string }>(`/api/exams/${examId}/share`, { method: "POST" });
      setShareUrl(res.url);
      setShareCopied(false);
      setShareFor(examId);
    } catch (e) {
      push("error", e instanceof Error ? e.message : "Share failed.");
    } finally {
      setSharingId(null);
    }
  }

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(shareUrl);
      setShareCopied(true);
      push("success", "Link copied");
      setTimeout(() => setShareCopied(false), 2000);
    } catch {
      // clipboard unavailable: fall back to selection + manual copy
      const input = document.querySelector<HTMLInputElement>("#share-url-input");
      input?.select();
      push("info", "Press Ctrl+C to copy the link");
    }
  }

  async function submit() {
    const text = prompt.trim();
    if (!text || busy) return;
    setFeed((f) => [...f, { id: nextId++, kind: "user", text }]);
    setPrompt("");

    const reply = isCertTopic(text) ? null : topicLessReply(text);
    if (reply !== null) {
      setFeed((f) => [...f, { id: nextId++, kind: "prompt", text: reply, options: PURPOSE_OPTIONS }]);
      return;
    }
    setShowConfig(true);
  }

  function pickPurpose(topic: string) {
    if (topic.startsWith("Something else")) {
      setPrompt("");
      inputRef.current?.focus();
      return;
    }
    setFeed([{ id: nextId++, kind: "user", text: topic }]);
    setPrompt(topic);
    setShowConfig(true);
  }

  async function generate(config: ExamGenConfig) {
    setShowConfig(false);
    setBusy(true);
    setStage(null);
    const configJson = JSON.stringify({
      count: config.count,
      ...(config.durationMinutes ? { durationMinutes: config.durationMinutes } : {}),
      ...(config.title ? { title: config.title } : {}),
      sourceIds: config.sourceIds,
      autoCrawl: config.autoCrawl,
    });
    const text = feed.length > 0 && feed[feed.length - 1].kind === "user" ? feed[feed.length - 1].text ?? "" : "";

    try {
      const created = await request<{ jobId: string }>("/api/exam-jobs", {
        method: "POST",
        body: { prompt: text, configJson },
      });
      const job = await pollJob(created.jobId);
      if (job.status === "completed" && job.examId) {
        setFeed((f) => [...f, { id: nextId++, kind: "exam", examId: job.examId! }]);
        push("success", "Exam generated");
        onGenerated();
      } else {
        setFeed((f) => [...f, { id: nextId++, kind: "error", text: job.error ?? "Generation failed." }]);
        push("error", "Generation failed — try again");
      }
    } catch (e) {
      setFeed((f) => [...f, { id: nextId++, kind: "error", text: e instanceof Error ? e.message : "Request failed." }]);
      push("error", "Can't reach the practice server");
    } finally {
      setBusy(false);
      setStage(null);
    }
  }

  async function pollJob(jobId: string): Promise<ExamJobStatus> {
    // Generation runs in the background worker now (WS-3A): poll for up to ~3 min.
    for (let i = 0; i < 180; i++) {
      await new Promise((r) => setTimeout(r, 1000));
      const job = await request<ExamJobStatus>(`/api/exam-jobs/${jobId}`);
      setStage(job.stage);
      if (job.status === "completed" || job.status === "failed") return job;
    }
    return { jobId, status: "failed", stage: null, examId: null, error: "Timed out", progress: null };
  }

  const stageCopy: Record<string, string> = {
    researching: "Working on it — researching sources…",
    embedding: "Embedding source material…",
    generating: "Generating questions…",
    validating: "Validating and saving…",
    persisting: "Validating and saving…",
    completed: "Finalizing…",
  };

  function defaultCardMode(summary: ExamSummary | undefined): "practice" | "certification" {
    return summary?.mode === "certification" ? "certification" : "practice";
  }

  function askDelete(examId: string, label: string, count: number) {
    setDeleteFor({ examId, label, count });
  }

  function confirmDelete() {
    if (!deleteFor) return;
    onDeleteExam(deleteFor.examId);
    // Remove the exam card from the conversation too.
    setFeed((f) => f.filter((item) => !(item.kind === "exam" && item.examId === deleteFor.examId)));
    setDeleteFor(null);
    push("success", "Exam removed from this browser");
  }

  return (
    <div className="flex h-[calc(100vh-5rem)]">
      {/* Left sidebar: Recent Exams */}
      <aside className="hidden md:flex flex-col w-1/4 max-w-sm bg-surface-container-lowest border-r border-outline-variant/30 h-full shadow-[4px_0_24px_rgba(0,0,0,0.02)]">
        <div className="p-lg">
          <button
            className="w-full flex items-center justify-center gap-sm bg-primary text-on-primary font-label-md py-md rounded-xl hover:bg-primary-container transition-all shadow-[0_4px_12px_rgba(53,37,205,0.2)] hover:-translate-y-0.5"
            onClick={newExam}
          >
            <span className="text-[20px]">+</span>
            New Chat
          </button>
        </div>
        <div className="px-lg pb-md">
          <h3 className="font-label-caps text-label-caps text-on-surface-variant mb-md uppercase tracking-wider">Recent Exams</h3>
        </div>
        <div className="flex-1 overflow-y-auto px-lg pb-lg space-y-sm">
          {exams.length === 0 && (
            <p className="text-body-sm text-on-surface-variant/70">Your generated exams will appear here.</p>
          )}
          {exams.map((e) => (
            <div
              key={e.examId}
              className="group block w-full text-left p-md rounded-xl bg-surface hover:bg-surface-container transition-colors cursor-pointer"
              onClick={() => onStartExam(e.examId, e.mode === "certification" ? "certification" : "practice")}
            >
              <div className="flex justify-between items-start mb-xs">
                <h4 className="font-label-md text-label-md text-on-surface">{e.title}</h4>
                <span className="flex items-center gap-xs shrink-0">
                  <button
                    type="button"
                    className="w-7 h-7 grid place-items-center rounded-lg text-on-surface-variant hover:text-danger hover:bg-danger-soft transition-colors text-base leading-none opacity-0 group-hover:opacity-100 cursor-pointer"
                    onClick={(ev) => {
                      ev.stopPropagation();
                      askDelete(e.examId, e.title, e.questionCount);
                    }}
                    aria-label="Delete exam"
                    title="Delete exam"
                  >
                    ×
                  </button>
                  <button
                    type="button"
                    className="w-8 h-8 grid place-items-center rounded-lg text-on-surface-variant hover:text-primary hover:bg-surface-container transition-colors text-lg leading-none"
                    onClick={(ev) => {
                      ev.stopPropagation();
                      share(e.examId);
                    }}
                    disabled={sharingId === e.examId}
                    aria-label="Share exam"
                    title="Share exam"
                  >
                    {sharingId === e.examId ? "…" : "⤴"}
                  </button>
                </span>
              </div>
              <p className="font-body-sm text-[12px] text-on-surface-variant">{e.questionCount} Qs • {e.mode === "certification" ? "Mock" : "Practice"}</p>
            </div>
          ))}
        </div>
      </aside>

      {/* Main chat area */}
      <div className="flex-1 flex flex-col bg-surface relative">
        <div className="absolute inset-0 pointer-events-none overflow-hidden opacity-30 mix-blend-multiply">
          <div className="absolute -top-[20%] -right-[10%] w-[50%] aspect-square rounded-full bg-gradient-to-br from-primary-fixed-dim/20 to-transparent blur-3xl" />
          <div className="absolute top-[60%] -left-[10%] w-[40%] aspect-square rounded-full bg-gradient-to-tr from-secondary-fixed/20 to-transparent blur-3xl" />
        </div>

        <div ref={feedRef} className="flex-1 overflow-y-auto px-md md:px-xxl py-xl space-y-xl z-10 pb-32">
          {feed.length === 0 && (
            <div className="text-center text-on-surface-variant mt-16 space-y-2">
              <p className="text-headline-md font-headline-md">Ask ExamGenius for a mock exam</p>
              <p className="text-body-sm">e.g. "5 câu hỏi AZ-900 mức Trung bình" — or attach your own document</p>
            </div>
          )}

          {feed.map((item) => {
            if (item.kind === "user") {
              return (
                <div key={item.id} className="flex w-full justify-end animate-[slideInRight_0.4s_ease-out]">
                  <div className="flex items-end gap-sm max-w-[85%] md:max-w-[70%]">
                    <div className="bg-primary text-on-primary p-lg rounded-2xl rounded-br-sm shadow-[0_8px_24px_rgba(53,37,205,0.15)]">
                      <p className="font-body-md leading-relaxed">{item.text}</p>
                    </div>
                    <div className="w-8 h-8 rounded-full bg-secondary-container text-on-secondary-container grid place-items-center mb-1 text-xs">G</div>
                  </div>
                </div>
              );
            }
            if (item.kind === "bot") {
              return (
                <div key={item.id} className="flex w-full justify-start animate-[slideInLeft_0.5s_ease-out]">
                  <div className="flex items-end gap-sm max-w-[90%] md:max-w-[80%]">
                    <div className="w-8 h-8 rounded-full bg-surface-tint text-on-primary flex items-center justify-center mb-1 shadow-sm shrink-0">✦</div>
                    <div className="bg-surface-container-lowest text-on-surface p-lg rounded-2xl rounded-bl-sm shadow-[0_4px_20px_rgba(0,0,0,0.03)] border border-outline-variant/20">
                      <p className="font-body-md leading-relaxed">{item.text}</p>
                    </div>
                  </div>
                </div>
              );
            }
            if (item.kind === "prompt") {
              return (
                <div key={item.id} className="flex w-full justify-start animate-[slideInLeft_0.5s_ease-out]">
                  <div className="flex items-end gap-sm max-w-[90%] md:max-w-[80%] w-full">
                    <div className="w-8 h-8 rounded-full bg-surface-tint text-on-primary flex items-center justify-center mb-1 shadow-sm shrink-0">✦</div>
                    <div className="flex flex-col gap-md w-full">
                      <div className="bg-surface-container-lowest text-on-surface p-lg rounded-2xl rounded-bl-sm shadow-[0_4px_20px_rgba(0,0,0,0.03)] border border-outline-variant/20 w-fit">
                        <p className="font-body-md leading-relaxed">{item.text}</p>
                      </div>
                      <div className="flex flex-wrap gap-sm">
                        {(item.options ?? []).map((opt) => (
                          <button
                            key={opt}
                            type="button"
                            className="px-md py-sm rounded-full bg-surface-container border border-outline-variant/40 text-on-surface font-label-md hover:bg-surface-variant hover:text-primary transition-colors cursor-pointer"
                            onClick={() => pickPurpose(opt)}
                          >
                            {opt}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              );
            }
            if (item.kind === "error") {
              return (
                <div key={item.id} className="flex w-full justify-start">
                  <div className="bg-danger-soft text-danger p-lg rounded-2xl border border-danger/30">
                    <p className="font-body-md">{item.text}</p>
                  </div>
                </div>
              );
            }
            if (item.kind === "exam" && item.examId) {
              const summary = exams.find((e) => e.examId === item.examId);
              const title = summary?.title ?? "Generated exam";
              const count = summary?.questionCount ?? 5;
              return (
                <div key={item.id} className="flex w-full justify-start animate-[slideInLeft_0.5s_ease-out]">
                  <div className="flex items-end gap-sm max-w-[90%] md:max-w-[80%]">
                    <div className="w-8 h-8 rounded-full bg-surface-tint text-on-primary flex items-center justify-center mb-1 shadow-sm shrink-0">✦</div>
                    <div className="group relative bg-surface-container-lowest rounded-2xl p-lg shadow-[0_12px_40px_rgba(0,0,0,0.06)] border border-outline-variant/20 overflow-hidden w-full md:w-[420px]">
                      <button
                        type="button"
                        className="absolute top-2 right-2 w-7 h-7 grid place-items-center rounded-lg text-on-surface-variant hover:text-danger hover:bg-danger-soft transition-colors text-base leading-none opacity-0 group-hover:opacity-100 cursor-pointer"
                        onClick={() => askDelete(item.examId!, title, count)}
                        aria-label="Delete exam"
                        title="Delete exam"
                      >
                        ×
                      </button>
                      <div className="flex justify-between items-start mb-md">
                        <div className="flex items-center gap-xs bg-secondary-fixed/50 text-on-secondary-fixed px-3 py-1 rounded-full w-fit">
                          <span className="font-label-caps text-[10px] tracking-wider uppercase">Generated</span>
                        </div>
                      </div>
                      <div className="mb-lg">
                        <h3 className="font-headline-md text-headline-md text-on-surface mb-2 leading-tight">{title}</h3>
                        <div className="flex flex-wrap gap-4 mt-md">
                          <div className="flex items-center gap-2 text-on-surface-variant bg-surface px-3 py-2 rounded-lg">
                            <span className="font-label-md text-sm">{count} Questions</span>
                          </div>
                        </div>
                      </div>
                      <p className="text-[12px] text-on-surface-variant/70 mb-lg">Available for 3 days · expires soon</p>
                      <div className="flex items-center justify-center gap-xs mb-md">
                        <div className="inline-flex items-center gap-xs bg-surface border border-outline-variant/30 rounded-full p-1" role="group" aria-label="Exam mode">
                          {(["practice", "certification"] as const).map((m) => (
                            <button
                              key={m}
                              type="button"
                              className={
                                "px-md py-xs rounded-full font-label-md transition-colors cursor-pointer " +
                                ((cardModes[item.examId!] ?? defaultCardMode(summary)) === m
                                  ? "bg-primary text-on-primary"
                                  : "text-on-surface-variant hover:text-primary")
                              }
                              onClick={() => setCardModes((cm) => ({ ...cm, [item.examId!]: m }))}
                            >
                              {m === "practice" ? "Practice" : "Mock"}
                            </button>
                          ))}
                        </div>
                      </div>
                      <div className="flex gap-md">
                        <button
                          className="flex-1 bg-primary text-on-primary font-label-md py-4 rounded-xl flex items-center justify-center gap-2 shadow-[0_4px_14px_rgba(53,37,205,0.2)] hover:-translate-y-1 transition-transform duration-300"
                          onClick={() => onStartExam(item.examId!, cardModes[item.examId!] ?? defaultCardMode(summary))}
                        >
                          <span className="font-bold tracking-wide">Start exam</span>
                          <span>→</span>
                        </button>
                        <button
                          className="px-4 py-4 rounded-xl bg-surface-container text-primary font-label-md hover:bg-surface-variant transition-colors disabled:opacity-50"
                          onClick={() => share(item.examId!)}
                          disabled={sharingId === item.examId}
                        >
                          {sharingId === item.examId ? "Preparing…" : "Share link"}
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              );
            }
            return null;
          })}

          {busy && (
            <div className="flex w-full justify-start opacity-50">
              <div className="flex items-end gap-sm">
                <div className="w-8 h-8 rounded-full bg-surface-container-high text-on-surface-variant flex items-center justify-center mb-1 shrink-0">✦</div>
                <div className="bg-surface-container-lowest border border-outline-variant/20 rounded-2xl px-4 py-3 animate-pulse">
                  <p className="font-body-sm">{stage ? (stageCopy[stage] ?? "Working on it…") : "Working on it…"}</p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Composer + config sheet */}
        <div className="absolute bottom-0 left-0 w-full bg-gradient-to-t from-surface via-surface/90 to-transparent pt-xl pb-lg px-md md:px-xxl z-20">
          <div className="max-w-4xl mx-auto">
            {showConfig && !busy && (
              <ExamConfigCard busy={busy} onGenerate={generate} onCancel={() => setShowConfig(false)} />
            )}
            <div className="relative bg-surface-container-lowest rounded-full shadow-[0_8px_32px_rgba(0,0,0,0.08)] border border-outline-variant/30 flex items-center p-2 focus-within:ring-2 focus-within:ring-primary/50">
              <input
                ref={inputRef}
                className="flex-1 bg-transparent border-none outline-none px-md font-body-md text-on-surface placeholder-on-surface-variant/50 h-12"
                placeholder="Type your exam request here..."
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && submit()}
                disabled={busy}
                aria-busy={busy}
              />
              <button
                className="w-10 h-10 rounded-full bg-primary text-on-primary flex items-center justify-center hover:scale-105 transition-all shadow-md disabled:opacity-50 disabled:hover:scale-100"
                onClick={submit}
                disabled={busy || !prompt.trim()}
                aria-busy={busy}
              >
                {busy ? (
                  <span className="inline-block w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin" />
                ) : (
                  <span className="text-[20px] -rotate-45 ml-1">➤</span>
                )}
              </button>
            </div>
            <p className="text-center font-body-sm text-[11px] text-on-surface-variant/60 mt-3">
              ExamGenius can make mistakes. Verify important information.
            </p>
          </div>
        </div>
      </div>

      {/* Share dialog */}
      {shareFor && (
        <div className="fixed inset-0 z-50 bg-black/30 flex items-center justify-center p-4" onClick={() => setShareFor(null)}>
          <div className="bg-surface-container-lowest rounded-2xl shadow-xl max-w-md w-full p-lg" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-headline-md text-headline-md mb-sm">Share this exam</h3>
            <p className="text-body-sm text-on-surface-variant mb-md">
              Anyone with this link can take the exam while it is available (3 days). Share it however you like.
            </p>
            <div className="flex gap-md items-center bg-surface rounded-xl border border-outline-variant p-md">
              <input id="share-url-input" readOnly value={shareUrl} className="flex-1 bg-transparent outline-none text-body-sm text-on-surface" />
              <button className="px-md py-sm rounded-lg bg-primary text-on-primary font-label-md shrink-0" onClick={copyLink}>
                {shareCopied ? "Copied!" : "Copy"}
              </button>
            </div>
            <div className="flex justify-end mt-md">
              <button className="px-md py-sm rounded-lg text-on-surface-variant font-label-md" onClick={() => setShareFor(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete exam dialog */}
      {deleteFor && (
        <DeleteExamDialog
          examLabel={deleteFor.label}
          questionCount={deleteFor.count}
          onConfirm={confirmDelete}
          onCancel={() => setDeleteFor(null)}
        />
      )}
    </div>
  );
}
