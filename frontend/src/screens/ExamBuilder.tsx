import { useRef, useState } from "react";
import { request } from "../api/client";
import type { ExamJobStatus } from "../types";
import { useToasts } from "../components/Toast";

interface Props {
  onStartExam: (examId: string) => void;
  onOpenExam: (examId: string) => void;
}

interface FeedItem {
  id: number;
  kind: "user" | "bot" | "exam" | "error";
  text?: string;
  examId?: string;
}

let nextId = 1;

export default function ExamBuilder({ onStartExam, onOpenExam }: Props) {
  const [feed, setFeed] = useState<FeedItem[]>([]);
  const [prompt, setPrompt] = useState("");
  const [busy, setBusy] = useState(false);
  const [shareFor, setShareFor] = useState<string | null>(null);
  const [shareUrl, setShareUrl] = useState("");
  const [shareCopied, setShareCopied] = useState(false);
  const [sharingId, setSharingId] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const feedRef = useRef<HTMLDivElement>(null);
  const { push } = useToasts();

  function newExam() {
    setFeed([]);
    setPrompt("");
    setBusy(false);
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
    setBusy(true);
    setFeed((f) => [
      ...f,
      { id: nextId++, kind: "user", text },
      { id: nextId++, kind: "bot", text: "Đã hiểu! Mình đang tạo bộ đề thi thử cho bạn. Dưới đây là bài thi của bạn:" },
    ]);

    try {
      const created = await request<{ jobId: string }>("/api/exam-jobs", { method: "POST", body: { prompt: text } });
      const job = await pollJob(created.jobId);
      if (job.status === "completed" && job.examId) {
        setPrompt("");
        setFeed((f) => [...f, { id: nextId++, kind: "exam", examId: job.examId! }]);
        push("success", "Exam generated");
      } else {
        setFeed((f) => [...f, { id: nextId++, kind: "error", text: job.error ?? "Generation failed." }]);
        push("error", "Generation failed — try again");
      }
    } catch (e) {
      // keep the typed prompt so nothing is lost; surface the error
      setFeed((f) => [...f, { id: nextId++, kind: "error", text: e instanceof Error ? e.message : "Request failed." }]);
      push("error", "Can't reach the practice server");
    } finally {
      setBusy(false);
    }
  }

  async function pollJob(jobId: string): Promise<ExamJobStatus> {
    for (let i = 0; i < 20; i++) {
      await new Promise((r) => setTimeout(r, 500));
      const job = await request<ExamJobStatus>(`/api/exam-jobs/${jobId}`);
      if (job.status === "completed" || job.status === "failed") return job;
    }
    return { jobId, status: "failed", examId: null, error: "Timed out", progress: null };
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
            New Exam
          </button>
        </div>
        <div className="px-lg pb-md">
          <h3 className="font-label-caps text-label-caps text-on-surface-variant mb-md uppercase tracking-wider">Recent Exams</h3>
        </div>
        <div className="flex-1 overflow-y-auto px-lg pb-lg space-y-sm">
          {feed.filter((i) => i.kind === "exam").length === 0 && (
            <p className="text-body-sm text-on-surface-variant/70">Your generated exams will appear here.</p>
          )}
          {feed
            .filter((i) => i.kind === "exam")
            .map((i) => (
              <button
                key={i.id}
                className="block w-full text-left p-md rounded-xl bg-surface hover:bg-surface-container transition-colors cursor-pointer"
                onClick={() => i.examId && onOpenExam(i.examId)}
              >
                <div className="flex justify-between items-start mb-xs">
                  <h4 className="font-label-md text-label-md text-on-surface">AZ-900 Cloud Concepts</h4>
                  <span className="bg-surface-variant text-on-surface-variant text-[10px] px-2 py-0.5 rounded-full">Ready</span>
                </div>
                <p className="font-body-sm text-[12px] text-on-surface-variant">5 Qs • Practice</p>
              </button>
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
              return (
                <div key={item.id} className="flex w-full justify-start animate-[slideInLeft_0.5s_ease-out]">
                  <div className="flex items-end gap-sm max-w-[90%] md:max-w-[80%]">
                    <div className="w-8 h-8 rounded-full bg-surface-tint text-on-primary flex items-center justify-center mb-1 shadow-sm shrink-0">✦</div>
                    <div className="bg-surface-container-lowest rounded-2xl p-lg shadow-[0_12px_40px_rgba(0,0,0,0.06)] border border-outline-variant/20 overflow-hidden w-full md:w-[420px]">
                      <div className="flex justify-between items-start mb-md">
                        <div className="flex items-center gap-xs bg-secondary-fixed/50 text-on-secondary-fixed px-3 py-1 rounded-full w-fit">
                          <span className="font-label-caps text-[10px] tracking-wider uppercase">Generated</span>
                        </div>
                      </div>
                      <div className="mb-lg">
                        <h3 className="font-headline-md text-headline-md text-on-surface mb-2 leading-tight">AZ-900 Cloud Concepts Practice</h3>
                        <div className="flex flex-wrap gap-4 mt-md">
                          <div className="flex items-center gap-2 text-on-surface-variant bg-surface px-3 py-2 rounded-lg">
                            <span className="font-label-md text-sm">5 Questions</span>
                          </div>
                          <div className="flex items-center gap-2 text-on-surface-variant bg-surface px-3 py-2 rounded-lg">
                            <span className="font-label-md text-sm">10 Minutes</span>
                          </div>
                          <div className="flex items-center gap-2 text-on-surface-variant bg-surface px-3 py-2 rounded-lg">
                            <span className="font-label-md text-sm">Practice</span>
                          </div>
                        </div>
                      </div>
                      <p className="text-[12px] text-on-surface-variant/70 mb-lg">Available for 3 days · expires soon</p>
                      <div className="flex gap-md">
                        <button
                          className="flex-1 bg-primary text-on-primary font-label-md py-4 rounded-xl flex items-center justify-center gap-2 shadow-[0_4px_14px_rgba(53,37,205,0.2)] hover:-translate-y-1 transition-transform duration-300"
                          onClick={() => onStartExam(item.examId!)}
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
                <div className="bg-surface-container-lowest border border-outline-variant/20 rounded-2xl px-4 py-3 animate-pulse">…</div>
              </div>
            </div>
          )}
        </div>

        {/* Composer */}
        <div className="absolute bottom-0 left-0 w-full bg-gradient-to-t from-surface via-surface/90 to-transparent pt-xl pb-lg px-md md:px-xxl z-20">
          <div className="max-w-4xl mx-auto">
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
              ezCert can make mistakes. Verify important information.
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
    </div>
  );
}
