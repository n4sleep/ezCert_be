import { useRef, useState } from "react";
import { request } from "../api/client";
import { useToasts } from "../components/Toast";

export interface ExamGenConfig {
  count: number;
  durationMinutes?: number;
  title?: string;
  sourceIds: string[];
  autoCrawl: boolean;
}

interface Attachment {
  key: string;
  label: string;
  sourceId: string;
  chunkCount?: number;
  status: "adding" | "ready" | "error";
  error?: string;
}

interface Props {
  busy: boolean;
  onGenerate: (config: ExamGenConfig) => void;
  onCancel: () => void;
}

const QUESTION_TICKS = [5, 10, 15, 20];

export default function ExamConfigCard({ busy, onGenerate, onCancel }: Props) {
  const [title, setTitle] = useState("");
  const [count, setCount] = useState(5);
  const [duration, setDuration] = useState("10");
  const [autoCrawl, setAutoCrawl] = useState(true);
  const [links, setLinks] = useState("");
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [error, setError] = useState("");
  const [crawling, setCrawling] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const { push } = useToasts();

  function addAttachment(a: Attachment) {
    setAttachments((prev) => [...prev, a]);
  }

  function removeAttachment(key: string) {
    setAttachments((prev) => prev.filter((a) => a.key !== key));
  }

  async function onFiles(files: FileList | null) {
    if (!files || files.length === 0) return;
    for (const file of Array.from(files)) {
      if (!/\.(txt|md|markdown)$/i.test(file.name)) {
        push("error", `${file.name} — only .txt and .md are supported right now.`);
        continue;
      }
      const key = `doc-${file.name}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      addAttachment({ key, label: file.name, sourceId: "", status: "adding" });
      const fd = new FormData();
      fd.append("files", file);
      try {
        const res = await request<Array<{ fileName: string; sourceId: string; title: string; chunkCount: number; error?: string }>>(
          "/api/sources/upload",
          { method: "POST", body: fd as unknown as Record<string, unknown> }
        );
        const item = res.find((r) => r.fileName === file.name);
        if (item?.sourceId) {
          setAttachments((prev) =>
            prev.map((a) =>
              a.key === key ? { ...a, sourceId: item.sourceId, label: item.title || file.name, chunkCount: item.chunkCount, status: "ready" } : a
            )
          );
        } else {
          setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, status: "error", error: item?.error ?? "Upload failed." } : a)));
        }
      } catch (e) {
        setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, status: "error", error: e instanceof Error ? e.message : "Upload failed." } : a)));
      }
    }
  }

  async function crawlLinks() {
    const urls = links
      .split(/[;\n]+/)
      .map((u) => u.trim())
      .filter(Boolean);
    if (urls.length === 0) return;
    setCrawling(true);
    try {
      const res = await request<Array<{ url: string; sourceId?: string; title?: string; chunkCount?: number; error?: string }>>(
        "/api/sources/crawl",
        { method: "POST", body: { urls } }
      );
      res.forEach((r, i) => {
        const key = `link-${r.url}-${Date.now()}-${i}`;
        if (r.sourceId) {
          addAttachment({ key, label: r.title || r.url, sourceId: r.sourceId, chunkCount: r.chunkCount, status: "ready" });
        } else {
          addAttachment({ key, label: r.url, sourceId: "", status: "error", error: r.error ?? "Crawl failed." });
        }
      });
      setLinks("");
    } catch (e) {
      push("error", e instanceof Error ? e.message : "Couldn't crawl the links.");
    } finally {
      setCrawling(false);
    }
  }

  function generate() {
    const sourceIds = attachments.filter((a) => a.status === "ready" && a.sourceId).map((a) => a.sourceId);
    if (!autoCrawl && sourceIds.length === 0) {
      setError("Auto-crawl is off — add at least one document or link first.");
      return;
    }
    setError("");
    const minutes = duration.trim() === "" ? undefined : Number(duration);
    onGenerate({
      count,
      durationMinutes: minutes && minutes > 0 ? minutes : undefined,
      title: title.trim() === "" ? undefined : title.trim(),
      sourceIds,
      autoCrawl,
    });
  }

  const inputCls = "w-full bg-surface border border-outline-variant/40 rounded-lg px-md py-sm font-body-sm text-on-surface outline-none focus:ring-2 focus:ring-primary/50 placeholder-on-surface-variant/50";

  return (
    <div className="max-w-4xl mx-auto w-full bg-surface-container-lowest rounded-2xl border border-outline-variant/30 shadow-[0_8px_32px_rgba(0,0,0,0.08)] p-lg mb-md">
      <div className="flex items-center justify-between mb-md">
        <h3 className="font-headline-md text-headline-md text-on-surface">Configure your exam</h3>
        <button
          type="button"
          className="text-on-surface-variant hover:text-primary transition-colors text-sm cursor-pointer"
          onClick={onCancel}
          disabled={busy}
        >
          Cancel
        </button>
      </div>

      <div className="flex flex-col gap-lg">
        {/* Name */}
        <div>
          <label className="font-label-md text-label-md text-on-surface-variant mb-xs block">Exam name (optional)</label>
          <input
            className={inputCls}
            placeholder="Leave empty for an auto-generated title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            disabled={busy}
          />
        </div>

        {/* Questions + duration */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-lg items-stretch">
          <div className="flex flex-col">
            <label className="font-label-md text-label-md text-on-surface-variant mb-xs block">
              Questions: <span className="font-bold text-on-surface">{count}</span>
            </label>
            <input
              type="range"
              min={1}
              max={20}
              step={1}
              value={count}
              onChange={(e) => setCount(Number(e.target.value))}
              disabled={busy}
              className="w-full accent-[#863bff] self-center"
              aria-label="Number of questions"
            />
            <div className="flex justify-between mt-xs w-full">
              {QUESTION_TICKS.map((t) => (
                <span
                  key={t}
                  className={
                    "text-[11px] px-sm py-xs rounded-full font-label-md " +
                    (count === t ? "bg-primary text-on-primary" : "text-on-surface-variant/60")
                  }
                >
                  {t}
                </span>
              ))}
            </div>
            <p className="text-[11px] text-on-surface-variant/50 mt-auto pt-xs">Number of questions in the exam.</p>
          </div>
          <div className="flex flex-col">
            <label className="font-label-md text-label-md text-on-surface-variant mb-xs block">
              Duration (minutes, optional) <span className="text-on-surface-variant/50">· used in Mock mode</span>
            </label>
            <input
              type="number"
              min={1}
              max={60}
              className="w-16 self-center text-center rounded-lg border border-outline-variant/40 px-sm py-sm font-body-sm bg-surface-container-low text-on-surface-variant/80 outline-none focus:ring-2 focus:ring-primary/50"
              placeholder="auto"
              value={duration}
              onChange={(e) => setDuration(e.target.value)}
              disabled={busy}
              aria-label="Duration in minutes"
            />
            <p className="text-[11px] text-on-surface-variant/50 mt-auto pt-xs">Leave empty for auto (questions × 2). Practice has no time limit.</p>
          </div>
        </div>

        {/* Documents */}
        <div>
          <label className="font-label-md text-label-md text-on-surface-variant mb-xs block">Documents (optional)</label>
          <input
            ref={fileRef}
            type="file"
            multiple
            accept=".txt,.md,.markdown"
            className="hidden"
            onChange={(e) => onFiles(e.target.files)}
            disabled={busy}
          />
          <button
            type="button"
            className="px-md py-sm rounded-lg bg-surface-container border border-outline-variant/40 text-primary font-label-md hover:bg-surface-variant transition-colors cursor-pointer disabled:opacity-50"
            onClick={() => fileRef.current?.click()}
            disabled={busy}
          >
            + Attach .txt / .md
          </button>
          <p className="text-[11px] text-on-surface-variant/50 mt-xs">Used as the question source. PDF support is coming.</p>
        </div>

        {/* Links */}
        <div>
          <label className="font-label-md text-label-md text-on-surface-variant mb-xs block">Links to crawl (optional)</label>
          <textarea
            className={inputCls}
            rows={2}
            placeholder={"Separate links with ; or a new line\nhttps://example.com/page-a; https://example.com/page-b"}
            value={links}
            onChange={(e) => setLinks(e.target.value)}
            disabled={busy || crawling}
          />
          <button
            type="button"
            className="mt-xs px-md py-sm rounded-lg bg-surface-container border border-outline-variant/40 text-primary font-label-md hover:bg-surface-variant transition-colors cursor-pointer disabled:opacity-50"
            onClick={crawlLinks}
            disabled={busy || crawling || links.trim() === ""}
          >
            {crawling ? "Crawling…" : "Add links"}
          </button>
        </div>

        {/* Attachments */}
        {attachments.length > 0 && (
          <div className="flex flex-wrap gap-sm">
            {attachments.map((a) => (
              <span
                key={a.key}
                className={
                  "inline-flex items-center gap-xs px-md py-xs rounded-full text-[12px] border " +
                  (a.status === "ready"
                    ? "bg-secondary-fixed/50 text-on-secondary-fixed border-outline-variant/40"
                    : a.status === "error"
                    ? "bg-danger-soft text-danger border-danger/30"
                    : "bg-surface-container text-on-surface-variant border-outline-variant/40")
                }
              >
                <span className="max-w-[220px] truncate">{a.label}</span>
                {a.status === "adding" && <span className="opacity-60">…</span>}
                {a.status === "ready" && a.chunkCount !== undefined && (
                  <span className="opacity-70 text-[10px] uppercase tracking-wider">{a.chunkCount} chunks</span>
                )}
                {a.status === "error" && a.error && <span className="opacity-70 text-[10px]">{a.error}</span>}
                <button
                  type="button"
                  className="opacity-60 hover:opacity-100 cursor-pointer leading-none"
                  onClick={() => removeAttachment(a.key)}
                  disabled={busy}
                  aria-label="Remove"
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}

        {/* Auto-crawl */}
        <div className="flex items-center justify-between">
          <div>
            <p className="font-label-md text-label-md text-on-surface">Auto-crawl sources</p>
            <p className="text-[11px] text-on-surface-variant/60">
              On: ExamGenius finds sources for any topic. Off: only your documents and links are used.
            </p>
          </div>
          <button
            type="button"
            role="switch"
            aria-checked={autoCrawl}
            onClick={() => setAutoCrawl((v) => !v)}
            disabled={busy}
            className={
              "w-12 h-7 rounded-full transition-colors relative cursor-pointer disabled:opacity-50 " +
              (autoCrawl ? "bg-primary" : "bg-outline-variant")
            }
          >
            <span
              className={
                "absolute top-1 w-5 h-5 rounded-full bg-white shadow transition-all " +
                (autoCrawl ? "left-6" : "left-1")
              }
            />
          </button>
        </div>

        {error && <p className="text-danger text-body-sm">{error}</p>}

        <div className="flex justify-end gap-md">
          <button
            type="button"
            className="px-lg py-md rounded-lg text-on-surface-variant font-label-md hover:text-primary transition-colors cursor-pointer"
            onClick={onCancel}
            disabled={busy}
          >
            Cancel
          </button>
          <button
            type="button"
            className="flex items-center gap-sm px-xl py-md rounded-xl bg-primary text-on-primary font-label-md shadow-md hover:-translate-y-0.5 transition-transform disabled:opacity-50 disabled:hover:translate-y-0 cursor-pointer"
            onClick={generate}
            disabled={busy}
          >
            {busy ? "Generating…" : "Generate exam →"}
          </button>
        </div>
      </div>
    </div>
  );
}
