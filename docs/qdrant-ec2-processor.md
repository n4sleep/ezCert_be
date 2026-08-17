# Qdrant on EC2 + Processor Deploy — Setup & Gotchas

How the hosted RAG stack runs (Phase D, AD-7/AD-15/AD-16) and every obstacle hit.

## Topology (hosted)

```
Browser → https://examgenius.bd-apa-coi.com (Route53 alias, rich sandbox 730335245469)
              ↓ CloudFront (old sandbox 400422680681, ACM cert, /api/* → App Runner)
        App Runner: ezcert-processor  (old sandbox, ECR-Public image)
              ├─→ PostgreSQL (RDS, old sandbox)
              ├─→ Qdrant (EC2 virginia-ec2, old sandbox, EIP 18.210.70.195:6333/6334)
              └─→ Bedrock gateway (Lambda + Function URL, rich sandbox)
```

- Custom domain: cert in OLD sandbox (ACM us-east-1), DNS alias in RICH sandbox Route53
  (`examgenius.bd-apa-coi.com → d3ku4gdv1yd16a.cloudfront.net`).
- Processor env drives everything: `Qdrant__Host/Port/ApiKey`, `BedrockGateway__Url/Secret`,
  `Bedrock__Mode=gateway`, `PublicSiteUrl`, `Cors__AllowedOrigins`.

## EC2 (`virginia-ec2`, i-08d029dbf5ae10740, t2.small)

- **Public IP is NOT stable** — stop/start reassigns it. We attached an **Elastic IP**
  `18.210.70.195` (eipalloc-0bdcf3e30472c86ae). The processor env must point at the EIP.
- **The VPC had NO internet path** (no IGW, no NAT, no 0.0.0.0/0 route). Fixed:
  `create-internet-gateway` → attach to `vpc-0fd47e944f4bc176c` → route
  `0.0.0.0/0 → igw-06e8bdbfe3d82de16` in main route table `rtb-03f917e2f3ad45986`.
  Without this, `dnf`/`docker pull` hang — and cloud-init boothooks fail silently.
- **`#cloud-boothook` user-data does NOT run reliably on Amazon Linux 2023** — the log
  never appears even with a correct `#cloud-boothook` header + network wait. Don't rely on
  it; bootstrap manually or via a real config-management path.
- **Manual Qdrant bootstrap** (runs on EC2):
  ```bash
  sudo dnf install -y docker
  sudo systemctl enable --now docker
  sudo docker run -d --name qdrant --restart unless-stopped \
    -p 6333:6333 -p 6334:6334 \
    -e QDRANT__SERVICE__API_KEY=<rotated-secret-not-in-repo> \
    -v qdrant_data:/qdrant/storage \
    qdrant/qdrant:v1.19.0
  ```
  `--restart unless-stopped` + docker `enable --now` survives reboots.
- Security group `sg-04a2bca76aaeaf7b4` opens 6333 (REST) + 6334 (gRPC) to 0.0.0.0/0,
  plus 22/80/443. Qdrant is API-key protected.

## Qdrant data model

- Collection `ezcert`, 1024-dim cosine (Titan embed v2).
- Chunks carry payload: `namespace` (`official:AZ900`), `source_url`, `section`, `text`.
- **Namespace normalization is critical** — cert codes are normalized to
  `NormalizeCert` (AZ-900 → AZ900) in BOTH `SeedService` and `GenerationService`.
  A mismatch (`official:az-900` vs `official:az900`) silently yields zero search hits.
- `.NET Qdrant.Client` uses **gRPC (6334)**, not REST (6333). Constructor:
  `new QdrantClient(host, port, https, apiKey)`.

## Seeding

- Seed docs live in `processor/seed/official/az900/*.md` (MS Learn units fetched with
  `Accept: text/markdown`, then cleaned in `SeedService.Clean`).
- Auto-seed on startup when the collection is missing; manual re-seed:
  `POST /api/admin/seed?cert=az900` (demo-only endpoint, no auth).
- 4 AZ-900 units → 18 chunks.

## Generation (AD-7)

- `GenerationService.GenerateAsync`: embed prompt → Qdrant search (namespace filter,
  top 8) → grounded Nova prompt (strict JSON schema) → parse + validate → persist Exam.
- Validation: exact question count, ≥2 choices, unique labels, ≥1 correct, citation
  required. Retry ≤3, then job `failed` with a friendly error (AI-path degradation).
- **Model tends to invent source URLs** (e.g. `/azure/app-service/`). Fix: prompt says
  `sourceUrl` must be `""`, and the service attaches the URL of a retrieved chunk
  (fallback = first allowed URL). Citations are therefore always grounded in seed content.
- Prompt parsing: detects cert (`AZ-900` etc.) and difficulty (`dễ/easy`, `khó/hard`,
  else medium); `configJson` can override `{count, cert, difficulty}`.

## Processor deploy cheatsheet

1. Publish + build image:
   ```powershell
   dotnet publish processor/EzCert.Processor.csproj -c Release -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o $env:TEMP\ezcert-publish\processor
   # copy into $env:TEMP\ezcert-dockerctx\publish, docker build -t ezcert-processor:hosted
   docker tag ezcert-processor:hosted public.ecr.aws/r2o9x8b0/ezcert-api:processor-vN-tag
   ```
2. ECR-Public auth (corp proxy breaks `credsStore`): `aws ecr-public get-login-password`,
   write a manual `%TEMP%\ezcert-dockercfg\config.json` with base64 `AWS:<pw>` under
   `auths["public.ecr.aws"]`, then `docker --config %TEMP%\ezcert-dockercfg push ...`.
3. **Always use a NEW image tag** — App Runner won't re-pull the same tag.
4. Update service with a full `--source-configuration` JSON spec (no BOM, UTF-8) including
   all env vars (they REPLACE on update).
5. Poll `Service.Status` → RUNNING, then `/health`, then a real exam job.
6. Note: the corp proxy blocks direct uploads to `lambda.amazonaws.com` but allows S3 PUTs
   — for Lambda code, stage the zip in S3 first (see `bedrock-gateway.md`).

## Gotcha list (in one place)

| # | Gotcha | Workaround |
|---|--------|-----------|
| 1 | EC2 public IP changes on stop/start | Elastic IP; env points at EIP |
| 2 | VPC had no IGW/route → no internet | IGW + 0.0.0.0/0 route |
| 3 | boothook user-data doesn't run on AL2023 | manual bootstrap + `--restart unless-stopped` |
| 4 | cert namespace mismatch → 0 search hits | `NormalizeCert` in both services |
| 5 | model invents source URLs | prompt `sourceUrl:""` + attach retrieved chunk URL |
| 6 | Qdrant.Client uses gRPC 6334 | env `Qdrant:Port=6334` |
| 7 | ECR-Public push fails via proxy | manual dockercfg auth |
| 8 | App Runner won't re-pull same tag | new tag per deploy |
| 9 | update-service env vars replace | include ALL vars in the spec JSON |
| 10 | proxy blocks lambda.amazonaws.com uploads | stage zip in S3, `--code S3Bucket=...` |
