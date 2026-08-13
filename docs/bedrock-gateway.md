# Bedrock Gateway — Setup, Challenges, Solutions

How we exposed Amazon Bedrock to the old PowerUser sandbox from the hackathon's
admin sandbox, and every obstacle hit along the way. Written 2026-08-13.

## Why this shape

- **IAM roles are account-scoped.** An App Runner service can only assume roles
  created in its own account. The old sandbox (400422680681) is PowerUser and
  **cannot create roles** (`iam:CreateRole` denied — verified live).
- Therefore Bedrock (which requires a role on the calling compute) must live in
  the hackathon's admin sandbox (730335245469, `AWSAdministratorAccess`).
- Result: a tiny **Bedrock gateway** service in the rich sandbox; the old
  sandbox's processor calls it over HTTP with a shared secret.
- This is AD-13/AD-14/AD-15 in the architecture spine.

## Architecture

```
old sandbox 400422680681 (PowerUser)            rich sandbox 730335245469 (Admin)
  processor (App Runner)  --Bearer secret-->      Bedrock gateway (App Runner)
                                                         |  instance role
                                                         v
                                                    Amazon Bedrock (us-east-1)
```

### Endpoints (all `POST /api/bedrock/*`, require `Authorization: Bearer {GATEWAY_SECRET}`)

| Endpoint | Model | Purpose |
|----------|-------|---------|
| `/api/bedrock/embed` | `amazon.titan-embed-text-v2:0` | text → 1024-dim vector |
| `/api/bedrock/generate` | `amazon.nova-micro-v1:0` | grounded question generation |
| `/api/bedrock/explain` | `amazon.nova-micro-v1:0` | wrong-vs-correct explanation |
| `/health` | — | **public**, App Runner health check (no auth allowed) |

### AWS resources (in 730335245469, us-east-1)

- **IAM role** `ezcert-bedrock-gateway-role`
  - Trust: `tasks.apprunner.amazonaws.com` (instance role — NOT `build.apprunner…`, that's the deployment principal)
  - Inline policy `bedrock-invoke`: `bedrock:InvokeModel` on `*`
  - Managed policy `AmazonEC2ContainerRegistryReadOnly` (ECR pull)
- **ECR Public repo**: `public.ecr.aws/j4t1r0w6/ezcert-bedrock-gateway`
- **App Runner service** `ezcert-bedrock-gateway`
  - URL: `https://gvqfk9rm2t.us-east-1.awsapprunner.com`
  - Port 8080, health check `GET /health`
  - Env: `GATEWAY_SECRET`, `AWS_REGION=us-east-1`
  - Instance role = `ezcert-bedrock-gateway-role`

## Step-by-step build

1. `dotnet new web -o processor/BedrockGateway -n EzCert.BedrockGateway`
2. `dotnet add package AWSSDK.BedrockRuntime --version 4.0.101.1`
3. Write `Program.cs`: 3 endpoints + bearer-secret middleware (`/health` exempt).
4. Publish self-contained for linux-x64:
   `dotnet publish -c Release -r linux-x64 --self-contained true -p:InvariantGlobalization=true`
5. Dockerfile: `debian:bookworm-slim` runtime + `COPY publish/ .` + `ASPNETCORE_URLS=http://0.0.0.0:8080`.
6. Push to ECR Public (login via `aws ecr-public get-login-password`), create App Runner service with the instance role.

## Challenges → Solutions

| # | Challenge | Symptom | Solution |
|---|-----------|---------|----------|
| 1 | **Health check can't send auth** | App Runner `CREATE_FAILED` after ~8 min; service log: "Failed to deploy your application image" | Add a **public `/health`** endpoint, exempt it from the auth middleware; point App Runner health check at it |
| 2 | **`InstanceRoleArn` vs access role** | `create-service` → "Authentication configuration is invalid" | Deployment access role is for ECR-private pull (not needed for ECR Public). Put the role in **`InstanceConfiguration.InstanceRoleArn`** |
| 3 | **Role trust principal** | (prevented the above) | Instance roles trust **`tasks.apprunner.amazonaws.com`**; the `build.apprunner…` principal is only for deployment roles |
| 4 | **`libssl` missing (AWS SDK)** | App started, but Bedrock call → "No usable version of libssl was found" | The AWS SDK (.NET) needs **OpenSSL 1.1** (`libssl.so.1.1`/`libcrypto.so.1.1`). `debian:bookworm-slim` has 3.x only; **apt is blocked by the corporate proxy** (also blocks MCR/apk). Copied the 1.1 libs from **`nginx:1.21`** (Docker Hub, reachable) via multi-stage `COPY --from` |
| 5 | **CA certificates missing** | Bedrock call → `AuthenticationException: The remote certificate is invalid … PartialChain` | Slim images have no CA bundle. Copied **`/etc/ssl/certs/ca-certificates.crt`** from `nginx:1.21` + set `SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt` |
| 6 | **Stale image tag** | Deployed "new" image still old behavior | App Runner doesn't re-pull an **unchanged tag** on `update-service`. Always push a **new tag** (e.g. `:ca-v1`) and point the service at it |
| 7 | **Cpu/Memory validation** | `Invalid length for parameter InstanceConfiguration.Cpu` (min length 3) | Drop `InstanceConfiguration` entirely; App Runner defaults are fine |
| 8 | **Proxy for external calls** | `curl` to the App Runner URL returned `000` | External URLs go **through** the corporate proxy (no `--noproxy`); localhost bypasses it. Use `-sSL` (no `--noproxy "*"`) for the gateway |

## Verified live (2026-08-13)

- `GET /health` → `{"status":"ok","service":"ezcert-bedrock-gateway"}`
- `POST /api/bedrock/embed` `{text}` → `200`, `embedding` length **1024**
- `POST /api/bedrock/generate` `{prompt,system,maxTokens,temperature}` → `200`, Nova text
- `POST /api/bedrock/explain` → `200`, Nova explanation
- Unauthorized (no/missing bearer) → `401`
- Instance-role credentials resolve automatically inside App Runner — no keys anywhere

## Rebuild / redeploy cheatsheet

```bash
dotnet publish processor/BedrockGateway -c Release -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o $TEMP/gw
# copy publish/ into a build ctx with the Dockerfile (see above)
docker build -t ezcert-bedrock-gateway:NEWTAG $CTX
docker tag ezcert-bedrock-gateway:NEWTAG public.ecr.aws/j4t1r0w6/ezcert-bedrock-gateway:NEWTAG
docker --config $DOCKERCFG push public.ecr.aws/j4t1r0w6/ezcert-bedrock-gateway:NEWTAG
# update spec ImageIdentifier -> NEWTAG, then:
aws apprunner update-service --profile AWSAdministratorAccess-730335245469 --region us-east-1 --service-arn <arn> --cli-input-json file://spec.json
```

## Security notes

- Shared secret is a bearer header, server-to-server only; **no CORS, no browser access**.
- Gateway holds no data, no state — stateless forwarder.
- Rotate `GATEWAY_SECRET` on the service + processor config together.
- The old sandbox's processor never holds AWS keys; it only knows the gateway URL + secret.
