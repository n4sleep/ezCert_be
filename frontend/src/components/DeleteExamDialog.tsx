interface Props {
  examLabel: string;
  questionCount: number;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function DeleteExamDialog({ examLabel, questionCount, onConfirm, onCancel }: Props) {
  return (
    <div className="fixed inset-0 z-50 bg-black/30 flex items-center justify-center p-4" onClick={onCancel}>
      <div
        className="relative bg-surface-container-lowest rounded-2xl shadow-xl max-w-md w-full p-lg"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="Delete exam"
      >
        <button
          type="button"
          className="absolute top-3 right-3 w-8 h-8 grid place-items-center rounded-lg text-on-surface-variant hover:text-danger hover:bg-surface transition-colors cursor-pointer"
          onClick={onCancel}
          aria-label="Close"
        >
          ×
        </button>
        <h3 className="font-headline-md text-headline-md text-on-surface mb-md pr-lg">
          Do you want to delete {examLabel} - {questionCount} questions?
        </h3>
        <div className="flex justify-between gap-md mt-lg">
          <button
            type="button"
            className="px-xl py-md rounded-lg bg-danger text-white font-label-md shadow-md hover:bg-[#b91c1c] transition-colors cursor-pointer"
            onClick={onConfirm}
          >
            Yes
          </button>
          <button
            type="button"
            className="px-xl py-md rounded-lg bg-surface-container text-on-surface font-label-md hover:bg-surface-variant transition-colors cursor-pointer"
            onClick={onCancel}
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
