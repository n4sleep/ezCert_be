---
name: ezCert ExamGenius v2
status: final
created: '2026-08-12'
updated: '2026-08-12'
sources:
  - 'docs/design/design-contract-examgenius-v2.md'
  - 'imports/stitch-chat-request.html'
  - 'imports/stitch-taking-exam.html'
  - 'imports/stitch-results.html'
---

# Foundation

- **Form factor:** responsive web app (desktop-first; usable on tablet/mobile). No native app.
- **UI system:** custom React component set styled with CSS variables per `DESIGN.md` tokens (`{colors.primary}`, `{rounded.xl}`, `{spacing.lg}` â€¦). No third-party component library.
- **Identity model:** Guest-only. The server issues an HttpOnly `guest_device_id` cookie; the UI never asks for identity. Header shows "Guest" at all times.

# Information Architecture

Three screens, one shell (top bar + optional sidebar). No routing between more than three top-level views; dialogs overlay. **Chat / Exam / Review tabs are always visible** â€” they are destinations, not journey stages.

- **ExamBuilder** (`/`): sidebar (New Exam Â· Recent Exams: persisted per-device history) + chat feed + composer + source chips.
- **Exam tab** (list of this device's exams): empty state **"Tell us what's on your mind"** with a CTA to Chat; otherwise a list of past exams, each with Start action.
- **Review tab** (this device's completed attempts, **newest first**): empty state **"Complete an exam to see your results"**; otherwise entries with title, date, score, pass badge â€” clicking opens that attempt's Results.
- **ExamTaking** (`/take/{examId}` and `/take/{shareToken}`): exam header (title Â· timer Â· progress) + question card + footer nav.
- **ExamResults** (`/results/{attemptId}`): score hero + section breakdown + review list + actions.
- **Dialogs:** Share (copy link), Confirm submit. No auth dialogs exist.
- **Chat-history behavior:** exams persist server-side per device. The sidebar and Exam tab list every past exam (ChatGPT-style); the user can return to Chat and start any exam, including old ones.

# Voice and Tone

- Friendly, supportive, precise. ExamGenius speaks in short conversational replies, then hands off to the exam.
- Status copy is honest about limits: "Available for 3 days", "Saved to this browser's history", "This exam has expired."
- Errors are calm and actionable ("AI practice is temporarily unavailable â€” official questions still work.").


# Component Patterns

- **AppShell:** fixed top bar (logo left, "Guest" + actions right), content max 1280px. Sidebar only on Builder (recent exams list). Nav tabs (Chat / Exam / Review) are real phase switches: Chat always available; Exam and Review render only when an exam/attempt exists, and hide otherwise (no dead links). Logo click returns to Chat.
- **ExamCard:** appears in the chat feed after job completion. Fields: title, meta pills (question count Â· minutes Â· mode), source chips, expiry countdown ("expires in 2d 14h"), primary **Start exam**, secondary **Share link**. Failure state shows error + **Try again** (starts a new job).
- **SourcePicker:** chip group under the composer: Official / My uploads / URL. Selecting Upload opens file picker (PDF/md, â‰¤10 MB); URL chip reveals a small input; selection persists for the next request. Uploaded sources are device-scoped (tied to the guest cookie).
- **Composer:** rounded input + attach (+) + send. Disabled while a job is running (show pulse/typing state in feed instead). The typed prompt is NOT cleared until the request succeeds â€” on failure it is restored so nothing is lost.
- **ExamOption:** button; single/truefalse use radio semantics, multi uses checkbox semantics with a "Select all that apply" hint. Selected state = {exam-option-selected}; in Practice mode, correct â†’ emerald soft, incorrect â†’ rose soft after check; disabled after reveal. In Certification mode nothing reveals until submit. Exposes `aria-pressed` and a "locked after check" hint when disabled.
- **QuestionReviewCard:** header row (question number + Correct/Incorrect tag), question text, answer rows (Your answer / Correct answer with icons), explanation box, citation link.
- **ShareDialog:** shows the generated link, copy button (copies + "Copied!" which reverts after ~2s), expiry note. Clipboard failure falls back to text-selection + "Press Ctrl+C to copy". Closes on Escape and overlay click. No user search (link-based).
- **DeleteExamDialog:** confirm before destructive delete; explains the link will stop working.
- **SubmitConfirmDialog:** confirm before terminal Submit Exam ("You can't change answers after submitting.") with Cancel / Submit; the submit button then shows a "Submittingâ€¦" spinner.

# State Patterns

- **Exam lifecycle:** generating (pulse in feed) â†’ ready (card, countdown) â†’ expired (410 message, archived) â†’ deleted (link dead).
- **Attempt lifecycle:** in_progress â†’ submitted â†’ results; expired mid-attempt â†’ auto-scored from snapshots with "scored from saved answers" banner.
- **Feed items:** user bubble, bot reply bubble, exam card (ready/error), system note (expired etc.). On mount, past exams render as cards (ChatGPT-style history).
- **Empty states:** no exams â†’ Exam tab shows "Tell us what's on your mind" (CTA to Chat); no completed attempts â†’ Review tab shows "Complete an exam to see your results"; no recent exams in sidebar â†’ "Your generated exams will appear here."
- **Global states:** offline/network error, AI 503 banner (non-blocking), rate-limit 429 banner, AI-down-with-official-bank-still-usable state. Never white-screen.

# Interaction Primitives

- Polling: Builder polls GET /api/exam-jobs/{id} (2s interval) while status is queued/running.
- Copy-to-clipboard with transient success.
- Confirm dialog before Submit Exam (Certification mode especially).
- Countdown timers render from server expires_at/started_at; tab-numeric font; prefers-reduced-motion respected (no pulse/ring animation).
- **Async button states (mandatory for every fetch):** pending (spinner + disabled + aria-busy) â†’ success (result) â†’ error (inline or toast + retry affordance; the button re-enables).
- **Toast system:** small top-right toasts for success (generated / link copied / results saved), error (network / expired), info (expired link). Auto-dismiss ~4s; errors persist until dismissed.

# Accessibility Floor

- Exam options are real radio-group buttons; keyboard operable; visible focus ring ({colors.primary} 3px).
- Color is never the only signal: correct/incorrect also carry icons and text labels.
- Contrast: slate on white body text passes AA; emerald/rose on soft backgrounds used for status only.
- Timer, progress, and score are announced via aria-live where they change asynchronously.
- All dialogs trap focus and dismiss on Escape; overlay click closes non-critical dialogs; on close, focus returns to the triggering control.
- Async controls set aria-busy while loading; exam options expose aria-pressed.
- Best-effort screen-reader support this round (no full audit).

# Key Flows

**Flow 1 â€” Generate an exam from an upload (the core loop)**
- Guest opens the site; composer invites a request.
- They click + â†’ Upload â†’ pick a PDF/markdown â†’ the file appears as a chip; they type "5 cÃ¢u há»i má»©c Trung bÃ¬nh vá» ná»™i dung nÃ y" (or English) and send.
- Feed: user bubble â†’ bot bubble ("ÄÃ£ hiá»ƒu! Äang táº¡o Ä‘á»â€¦") with pulse â†’ POST /api/exam-jobs â†’ polling â†’ **Exam Card** slides in with "Available for 3 days".
- Climax: the exam card appears with **Start exam**; the user starts and takes it.
- Edge: job fails â†’ card shows error + Try again; upload too large â†’ inline error before send.

**Flow 2 â€” Take a certification-mode exam with server timer**
- Guest starts the exam; header shows countdown (server-authoritative), progress "3 of 10".
- Options select with radio behavior; no correctness revealed; Previous/Next navigate; a confirmation dialog guards Submit.
- If time expires mid-exam: submit still scores from snapshots; banner "Exam expired â€” scored from your saved answers."
- Climax: Results screen with score ring + pass/fail + per-section bars.
- Edge: tab closed mid-exam â†’ reopening the exam resumes or shows expired state per server status.

**Flow 3 â€” Share an exam by link**
- Guest (owner device) opens a ready Exam Card â†’ **Share link** â†’ dialog shows https://â€¦/take/{token} + Copy.
- Another Guest browser opens the link â†’ loads the same exam â†’ takes it â†’ their attempt saves to *their* browser history.
- Owner deletes the exam â†’ takers see 410 "This exam has expired."
- Climax: the recipient completes a shared exam and sees their own score; no account involved anywhere.
- Edge: link opened after 3 days â†’ expired message with no score.

**Flow 4 â€” Review and drill (results loop)**
- After submit, Results shows the score ring, section breakdown, and review list (correct/incorrect with citations).
- Guest clicks **Drill weak areas** or **Retry missed** â†’ a new job is created from the same sources with a scoped config â†’ new Exam Card in feed.
- **Explain deeper** on a question â†’ grounded AI explanation appended inline.
- Climax: the guest turns a 62% result into a targeted retry in two clicks.
