// Reusable confirm dialog (EXPERIENCE.md: SubmitConfirmDialog pattern).
// Traps focus lightly, closes on Escape / Cancel, returns focus on close.
import { useEffect, useRef } from "react";

interface Props {
  title: string;
  body: string;
  confirmLabel: string;
  cancelLabel?: string;
  busy?: boolean;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function ConfirmDialog({
  title,
  body,
  confirmLabel,
  cancelLabel = "Cancel",
  busy = false,
  danger = false,
  onConfirm,
  onCancel,
}: Props) {
  const confirmRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    confirmRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !busy) onCancel();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [busy, onCancel]);

  return (
    <div className="fixed inset-0 z-50 bg-black/30 flex items-center justify-center p-4" onClick={busy ? undefined : onCancel}>
      <div className="bg-surface-container-lowest rounded-2xl shadow-xl max-w-sm w-full p-lg" onClick={(e) => e.stopPropagation()}>
        <h3 className="font-headline-md text-headline-md mb-sm">{title}</h3>
        <p className="text-body-sm text-on-surface-variant mb-md">{body}</p>
        <div className="flex justify-end gap-md">
          <button className="px-md py-sm rounded-lg text-on-surface-variant font-label-md" onClick={onCancel} disabled={busy}>
            {cancelLabel}
          </button>
          <button
            ref={confirmRef}
            className={
              "px-md py-sm rounded-lg text-on-primary font-label-md " +
              (danger ? "bg-danger" : "bg-primary")
            }
            onClick={onConfirm}
            disabled={busy}
          >
            {busy ? "Submitting…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
