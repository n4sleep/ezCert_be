# ezCert — ExamGenius v2

Ask for a mock exam in a chat; ExamGenius generates it grounded in trusted sources (official docs, your uploads, URLs). No accounts — everyone is a Guest. Exams live **3 days**, can be shared by link, and attempts/marks are saved per browser.

## Repo layout (3 folders)

```text
frontend/   React 18 + TypeScript + Vite — Builder (chat) / Exam / Results
processor/  .NET 8 — the ONE backend: guest identity, sources, generation jobs, exams, attempts
crawler/    TypeScript — URL → clean documents (Firecrawl v1, Crawlee fallback)
```

Dependency rule: `frontend → processor → crawler`. Only `processor/` writes the database.

## Local dev

```bash
docker compose up -d          # postgres (:5432) + qdrant (:6333)

# crawler
cd crawler && npm install && npm run start          # :8081

# processor
cd processor && dotnet run                          # :5080

# frontend
cd frontend && npm install && npm run dev           # :5173
```

Copy `.env.example` → `.env` and fill in values (Firecrawl key optional for local).

## Design contracts

- Design contract (human-friendly): [`docs/design/design-contract-examgenius-v2.md`](docs/design/design-contract-examgenius-v2.md)
- UX spines (visual + behavior): [`docs/ux/DESIGN.md`](docs/ux/DESIGN.md) and [`docs/ux/EXPERIENCE.md`](docs/ux/EXPERIENCE.md)
- Architecture spine: [`docs/architecture/ARCHITECTURE-SPINE.md`](docs/architecture/ARCHITECTURE-SPINE.md)

## Infra runbooks

- Qdrant + processor deploy: [`docs/qdrant-ec2-processor.md`](docs/qdrant-ec2-processor.md)
- Bedrock gateway: [`docs/bedrock-gateway.md`](docs/bedrock-gateway.md)

## Status

Branch `rebuild/examgenius-v2`. Scaffolded; vertical slice in progress (guest cookie → exam-jobs → 5-question exam → take → results).
