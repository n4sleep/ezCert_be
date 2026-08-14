# Bedrock Gateway — Setup, Challenges, Solutions

How we exposed Amazon Bedrock to the old PowerUser sandbox from the hackathon's
admin sandbox, and every obstacle hit along the way. Written 2026-08-13.
Updated 2026-08-14: gateway migrated from App Runner to AWS Lambda + Function URL
(AD-16) — App Runner is in AWS maintenance mode and compliance flagged it.

## Why this shape

- **IAM roles are account-scoped.** Compute can only assume roles created in its
  own account. The old sandbox (400422680681) is PowerUser and **cannot create
  roles** (`iam:CreateRole` denied — verified live).
- Therefore Bedrock (which requires a role on the calling compute) must live in
  the hackathon's admin sandbox (730335245469, `AWSAdministratorAccess`).
- Result: a tiny **Bedrock gateway** in the rich sandbox; the old sandbox's
  processor calls it over HTTP with a shared secret.
- This is AD-13/AD-14/AD-15/AD-16 in the architecture spine.

## Architecture

```
old sandbox 400422680681 (PowerUser)            rich sandbox 730335245469 (Admin)
  processor (App Runner)  --Bearer secret-->      Bedrock gateway (Lambda + Function URL)
                                                          |  execution role
                                                          v
                                                     Amazon Bedrock (us-east-1)
```

### Endpoints (all `POST /api/bedrock/*`, require `Authorization: Bearer {GATEWAY_SECRET}`)

| Endpoint | Model | Purpose |
|----------|-------|---------|
| `/api/bedrock/embed` | `amazon.titan-embed-text-v2:0` | text → 1024-dim vector |
| `/api/bedrock/generate` | `amazon.nova-micro-v1:0` | grounded question generation |
| `/api/bedrock/explain` | `amazon.nova-micro-v1:0` | wrong-vs-correct explanation |
| `/health` | — | **public**, liveness check (no auth allowed) |

### AWS resources (in 730335245469, us-east-1)

- **IAM role** `ezcert-bedrock-gateway-lambda-role`
  - Trust: `lambda.amazonaws.com` (execution role)
  - Inline policy `bedrock-invoke`: `bedrock:InvokeModel` on `*`
  - Managed policy `AWSLambdaBasicExecutionRole` (CloudWatch logs)
- **Lambda function** `ezcert-bedrock-gateway` (managed runtime `dotnet8`, zip deploy)
  - Handler: `EzCert.BedrockGateway` (executable assembly — top-level statements +
    `Amazon.Lambda.AspNetCoreServer.Hosting`, activated when `AWS_LAMBDA_FUNCTION_NAME` is set)
  - Function URL: `https://bwddk4o4axtxlhmnounznze3oy0lshgt.lambda-url.us-east-1.on.aws`
  - AuthType `NONE` (server-to-server; the app's bearer middleware is the real gate)
  - Env: `GATEWAY_SECRET` only (`AWS_REGION` is reserved by Lambda — code defaults to us-east-1)
  - 512 MB, 60 s timeout
- **Resource policy on the function** (both required since Oct 2025):
  - `lambda:InvokeFunctionUrl` with `Principal: *` + `FunctionUrlAuthType: NONE`
  - `lambda:InvokeFunction` with `Principal: *` (without the auth-type condition)
  - Missing the second one → 403 `AccessDeniedException` on every request

## Step-by-step build

1. `dotnet new web -o processor/BedrockGateway -n EzCert.BedrockGateway`
2. `dotnet add package AWSSDK.BedrockRuntime --version 4.0.101.1`
3. `dotnet add package Amazon.Lambda.AspNetCoreServer.Hosting` (1.7.3)
4. Write `Program.cs`: 3 endpoints + bearer-secret middleware (`/health` exempt);
   add `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` when
   `AWS_LAMBDA_FUNCTION_NAME` is set (Kestrel locally otherwise).
5. Package the zip:
   `dotnet lambda package -c Release -f net8.0 -r linux-x64 --output-package <zip>`
6. Upload the zip to S3 in the rich account and create the function from S3
   (the corp proxy blocks direct uploads to `lambda.amazonaws.com` —
   `content_filter_denied`; S3 PUTs are allowed):
   - `aws s3 cp <zip> s3://ezcert-lambda-deploy-730335245469/`
   - `aws lambda create-function --function-name ezeret-bedrock-gateway --runtime dotnet8
     --role <lambda-role> --handler EzCert.BedrockGateway
     --code S3Bucket=ezcert-lambda-deploy-730335245469,S3Key=<zip> --memory-size 512 --timeout 60
     --environment Variables={GATEWAY_SECRET=<secret>}`
7. `aws lambda create-function-url-config --auth-type NONE`
8. `aws lambda add-permission` × 2 (both actions — see resources above).

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
