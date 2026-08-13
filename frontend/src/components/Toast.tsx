import { createContext, useCallback, useContext, useRef, useState } from "react";

// Minimal toast system (EXPERIENCE.md Interaction Primitives):
// success/info toasts auto-dismiss ~4s; errors persist until dismissed.
export type ToastKind = "success" | "error" | "info";

export interface Toast {
  id: number;
  kind: ToastKind;
  text: string;
}

interface ToastContextValue {
  toasts: Toast[];
  push: (kind: ToastKind, text: string) => void;
  dismiss: (id: number) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

let nextToastId = 1;

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timers = useRef<Record<number, ReturnType<typeof setTimeout>>>({});

  const dismiss = useCallback((id: number) => {
    setToasts((t) => t.filter((x) => x.id !== id));
    const timer = timers.current[id];
    if (timer) {
      clearTimeout(timer);
      delete timers.current[id];
    }
  }, []);

  const push = useCallback(
    (kind: ToastKind, text: string) => {
      const id = nextToastId++;
      setToasts((t) => [...t, { id, kind, text }]);
      if (kind !== "error") {
        timers.current[id] = setTimeout(() => dismiss(id), 4000);
      }
    },
    [dismiss]
  );

  return (
    <ToastContext.Provider value={{ toasts, push, dismiss }}>{children}</ToastContext.Provider>
  );
}

export function useToasts(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToasts must be used within ToastProvider");
  return ctx;
}

export function ToastViewport() {
  const { toasts, dismiss } = useToasts();
  if (toasts.length === 0) return null;
  return (
    <div className="fixed top-24 right-4 z-[100] flex flex-col gap-2 w-80" role="region" aria-live="polite">
      {toasts.map((t) => (
        <div
          key={t.id}
          className={
            "rounded-xl px-4 py-3 shadow-lg text-sm flex items-start justify-between gap-2 border " +
            (t.kind === "success"
              ? "bg-success-soft text-success-strong border-success-strong/40"
              : t.kind === "error"
              ? "bg-danger-soft text-danger border-danger/40"
              : "bg-surface-container text-on-surface border-outline-variant")
          }
        >
          <span>{t.text}</span>
          <button className="opacity-60 hover:opacity-100 text-base leading-none" onClick={() => dismiss(t.id)} aria-label="Dismiss">
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
