# ezCert / ExamGenius — Design Contract v2 (Guest Mode)

> **Human-friendly summary of the agreed design.** Source of truth for UX spines, architecture, and the rebuild. Last updated 2026-08-12.

---

## 0. The Product

**ezCert, powered by ExamGenius** — a cloud-certification practice platform where you ask for a mock exam in a chat, ExamGenius generates it grounded in trusted sources (official docs, your uploads, URLs), and you take, review, and share it. **No accounts. Everyone is a Guest.** Generated exams live for **3 days** then expire.

**Core loop:** prompt → generate → take → review → share → repeat

**UI direction:** light indigo (Stitch design system) — primary `#4F46E5`, Inter font, rounded-xl cards, soft shadows, emerald (correct/pass) + rose (incorrect/fail) status colors. Modern, clean, premium EdTech.

---

## 1. Folder Structure

```text
ezCert_be/                          # monorepo, branch rebuild/examgenius-v2
├── frontend/                       # React 18 + TypeScript + Vite (the SPA)
│   └── src/
│       ├── screens/                # ExamBuilder, ExamTaking, ExamResults
│       ├── components/             # AppShell, SourcePicker, ExamCard, ShareDialog, QuestionReviewCard
│       ├── api/client.ts           # single API client
│       ├── lib/chatHistory.ts      # optional local chat history
│       └── styles/tokens.css       # Stitch design tokens
│
├── processor/                      # .NET 8 — the ONE backend (API + jobs + RAG + DB)
│   ├── Program.cs
│   ├── Features/                   # Guests, Sources, Generation, Exams, Attempts
│   ├── Infrastructure/             # Postgres, Qdrant, Bedrock, ObjectStorage, CrawlerClient
│   └── Migrations/
│
└── crawler/                        # TypeScript — URL → clean documents ONLY
    └── src/
        ├── server.ts               # POST /crawl
        ├── contract.ts             # CrawledDocument (neutral contract)
        └── providers/              # firecrawl.ts (v1) | crawlee.ts (fallback)
```

**Rules:**
- Only 3 top-level source folders. Root holds only `docker-compose.yml`, `.env.example`, `README.md`.
- `frontend → processor → crawler`. The crawler never touches the database, vectors, or user data.
- The processor is the only system that writes the database.

---

## 2. End-User Behavior

### Guest identity (invisible)
- The processor issues a guest_device_id (UUID) as an HttpOnly cookie on first visit. No form, no login, no logout.
- The header always shows "Guest". No avatar or auth states.
- Clearing cookies = a new Guest. History is tied to the browser (labeled honestly).

### Screen 1 — Exam Builder (chat)
- **Sidebar:** New Exam button · Recent Exams tabs (**Mine / Completed**)
- **Chat feed:** your prompt bubble → ExamGenius reply → **Exam Card** (animates in when generation completes)
- **Exam Card:** title · "10 Questions · 15 Minutes · Practice" · source chips · **Start exam** (primary) · **Share link** (secondary) · "Available for 3 days" badge with countdown ("expires in 2d 14h")
- **Composer:** rounded input "Type your exam request here…" · + to attach (upload PDF/markdown or paste a URL) · send
- **Flow:** send → POST /api/exam-jobs → typing/pulse state → poll job → completed → card appears
- **Source chips** under composer: Official / My uploads / URL (choose what grounds the exam)

### Screen 2 — Exam Taking
- **Top bar:** exam title · countdown timer (center, 14:25 remaining, server-authoritative) · progress 3 of 10 (right)
- **Question card:** number badge · question text · optional stimulus/code block · options A–D (radio behavior, keyboard accessible; selected = soft blue + indigo ring)
- **Footer:** Previous · Next · **Submit Exam** (emerald, with confirm dialog)
- **Practice mode:** correctness + explanation revealed per question. **Certification mode:** nothing revealed until submit; server timer + server scoring.
- **Expired mid-attempt:** submitting still scores from your saved snapshots; banner shows "Exam expired — scored from your saved answers." Saved to history.

### Screen 3 — Results & Review
- **Hero:** animated score ring · 8/10 (80%) · pass/fail badge (emerald "Great job! You passed." / rose "Keep practicing") · time taken · date
- **Per-section breakdown:** bars per objective
- **Review list:** correct cards (soft green, check, explanation + citation) · incorrect cards (soft red, X, your answer struck through, correct answer highlighted, explanation + citation)
- **Actions:** Explain deeper · Drill weak areas · Retry missed · Back to Chat
- History note: "Saved to this browser's history"

### Share (link-based)
- Exam Card → **Share link** → copies https://…/take/{shareToken}
- Anyone with the link (any Guest browser) can take the exam while it is alive
- Expired link → "This exam has expired."
- The owner (same device) can delete the exam → kills the link (410 for takers)
- Attempts from a shared exam save to *that* browser's history

### Failure states (never white-screen)
- Generation job failed → card shows error + "Try again" (new job)
- Rate limited → friendly 429 message
- AI unavailable → 503 banner; official bank still works
- Expired exam → 410 with clear copy

---

## 3. Backend — Run & Hooks

### API surface

``text
GENERATION
POST /api/exam-jobs            { prompt, config: {cert, mode, difficulty, count, sources[]} }
GET  /api/exam-jobs/{id}       -> { status, examId?, error? }

EXAMS
GET  /api/exams?scope=mine|completed
GET  /api/exams/{id}
POST /api/exams/{id}/share     -> { shareToken, url }     (link mode)
DELETE /api/exams/{id}                                    (owner device)
GET  /api/exams/take/{shareToken}                         (valid while alive)

ATTEMPTS
POST /api/exams/{id}/attempts
POST /api/attempts/{id}/answers    { attemptQuestionId, selected[] }
POST /api/attempts/{id}/submit
GET  /api/attempts/{id}/results
GET  /api/me/attempts             (device-scoped history)

SOURCES
POST /api/sources/upload          multipart (PDF/md, <=10 MB)
POST /api/sources/url             { url }  -> enqueues crawl
GET  /api/sources/{id}
``

### Guest hook (replaces auth)
- Every request carries the guest_device_id cookie (issued if absent).
- Ownership checks: deleting an exam requires owner_device_id == cookie; attempt results require device_id == cookie.
- CSRF protection still applies to cookie-authenticated mutations.

### Generation pipeline (the heart)
1. POST /api/exam-jobs -> ProcessingJob(queued) -> jobId
2. Background worker:
   a. Resolve sources: official -> Qdrant namespace official:{cert}; upload -> parse, chunk, embed, upsert guest:{deviceId}:{sourceId}; url -> call crawler, normalize, hash, store, chunk, embed
   b. Generate: embed topic -> Qdrant search (namespaced) -> Bedrock prompt -> parse JSON -> validate (type, >=2 choices, >=1 correct, explanation, citation) -> retry <=3
   c. Persist Exam(status=ready, expires_at=now+3d) + Questions + Choices + Citations
   d. Job(status=completed, examId)
3. Frontend polls -> Exam Card
- Run model: single .NET background worker; ProcessingJob table is the queue. No Redis/broker.

### Expiry hook (3-day TTL)
- Exam.expires_at = created_at + 3 days
- Lazy check on start/open-link (410 if expired)
- Cleanup worker: expired -> status=archived (Questions/Attempts kept for history)

### Crawler hook
``text
POST {crawler}/crawl { url, limit, includePaths[] } -> CrawledDocument[]
{ canonicalUrl, title, markdown, contentHash, fetchedAt, metadata }
``
- Firecrawl = v1 provider; Crawlee = fallback behind contract.ts
- Deterministic enforcement (domains, limits, robots, retries); an LLM may propose URLs but never executes
- The crawler never receives user documents or credentials

---

## 4. Database

**Stores:** PostgreSQL (authoritative) · Qdrant (vectors, derived) · S3/local object store (raw files).

``text
Source
- id UUID PK
- owner_device_id nullable        # null = official/system
- kind            # official | upload | url
- title, status   # pending | ready | failed
- created_at

SourceDocument
- id UUID PK
- source_id FK
- canonical_url nullable
- object_uri, content_hash, fetched_at

ProcessingJob
- id UUID PK
- owner_device_id nullable
- kind            # crawl | ingest | generate
- status          # queued | running | completed | failed
- prompt, config_json
- exam_id nullable, progress, error
- created_at, updated_at

Exam
- id UUID PK
- owner_device_id nullable        # null = official bank
- share_token nullable unique     # link sharing
- certification_code nullable, title, description
- mode, difficulty, duration_minutes
- status          # generating | ready | archived | failed
- expires_at                       # created_at + 3 days
- generation_prompt, created_at, updated_at
# IMMUTABLE once ready; regenerate = new exam; delete = 410 on link

Question
- id UUID PK, exam_id FK
- ordinal, type, text, explanation

Choice
- id UUID PK, question_id FK
- ordinal, label, text, is_correct

QuestionCitation
- id UUID PK, question_id FK
- source_document_id nullable, source_url nullable
- page_number nullable, quoted_text

Attempt
- id UUID PK
- exam_id FK, device_id string
- status          # in_progress | submitted | expired
- started_at, expires_at nullable, submitted_at nullable
- correct_count, total_questions, score_percent, passed

AttemptQuestion               # immutable snapshot
- id UUID PK, attempt_id FK
- source_question_id nullable, ordinal
- question_json, choices_json, correct_json, explanation, citation_json

Answer
- id UUID PK, attempt_question_id FK
- selected_json, is_correct, answered_at

SectionScore
- id UUID PK, attempt_id FK
- section, total, correct, percentage
``

**Rules:**
- No User table, no credentials, no SSO columns. device_id strings only.
- Exam immutable at eady; 3-day TTL; archived by cleanup; snapshots outlive archive.
- Sharing = unique share_token, valid while the exam is alive; delete invalidates it.
- Attempts/marks keyed by device_id; owner-only access via cookie match.
- Qdrant namespaces: official:{cert} · guest:{deviceId}:{sourceId} — server-set, client never supplies.
- Cascade: Exam->Questions->Choices; Attempt->AttemptQuestion->Answer.

---

## 5. Build Sequence

1. UX spines (DESIGN.md + EXPERIENCE.md) via bmad-ux
2. Architecture invariants via bmad-architecture
3. Rebuild on ebuild/examgenius-v2 (3 folders, old code kept in git history)
4. Vertical slice: guest cookie + exam-jobs + upload/paste -> 5-Q exam -> take -> results
5. Persistence + history: device-scoped attempts, 3-day expiry + cleanup
6. Share-by-link + delete
7. Crawler adapter (Firecrawl)
8. Optional: browser-local chat history

**Explicitly out this round:** credentials/SSO, groups, password reset, achievements, fork/copy exams, server-side chat history, directory lookups.
