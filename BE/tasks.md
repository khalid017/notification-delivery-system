# Backend Tasks

Specs: `docs/BE-01-plan.md` (what and order) and `docs/BE-02-details.md` (how). Section numbers below refer to those files.

**How to work through this file**
- Take the first unchecked task. Enter plan mode first, then implement.
- A task is done only when `dotnet build` passes, its tests pass (where listed) and its "Done when" check works. Then tick it `[x]`.
- Stop after each task so it can be reviewed and committed. Do not start the next one.
- Follow the dependency rules in BE-01 section 6.3. Do not build anything listed as out of scope (BE-01 section 3).

---

## B1. Foundation

- [x] **B1.1 Solution and projects**
  Create `BE/NotificationPlatform.sln`, the six `src` projects and `tests/NotificationPlatform.Services.Tests`, with NuGet packages and project references exactly as in BE-01 sections 4 and 6.
  *Done when:* `dotnet build` passes and no project references violate the rules in 6.3.

- [x] **B1.2 Domain, abstractions and data layer**
  Domain entities and status constants. The five interfaces in `Services/Abstractions`. In DataAccess: `MongoContext`, `MongoIndexInitializer` (indexes from BE-02 section 4), `AddDataAccess(config)`. Root `docker-compose.yml` with MongoDB only.
  *Done when:* `docker compose up` starts Mongo and the indexes are created on API startup.

- [x] **B1.3 API host skeleton**
  `Program.cs` composition root with options classes validated at startup, global exception middleware, the `Error` response shape for validation failures, camelCase JSON, `api` route prefix, and the Health controller. Fixed local port 5080.
  *Done when:* `GET /api/health` returns `200 { status: "ok" }` locally.

## B2. Auth

- [x] **B2.1 Token and API key services**
  `TokenService` (HS256, claims `sub`, `tenant_id`, `email`, `name`, `exp`), `JwtOptions`, `ApiKeyService` (`np_` key, SHA-256 hash, display prefix). Unit tests per BE-02 section 9.
  *Done when:* the `TokenService` and `ApiKeyService` tests pass.

- [x] **B2.2 Auth service and endpoints**
  `AuthService` with BCrypt, `TenantDataAccess`, and the Auth controller (`register`, `login`, `me`). JWT bearer scheme with 30 second clock skew and `MapInboundClaims = false`. Validation rules from BE-02 section 5.7. Unit tests for `AuthService`.
  *Done when:* register returns a token and an API key, `/auth/me` works with the token and returns 401 without it, a wrong password returns 401, a short password returns 400 with field errors, and a duplicate email returns 409.

- [x] **B2.3 API key authentication**
  `ApiKeyAuthenticationHandler` (scheme `ApiKey`, fixed-time hash comparison) and the `User.GetTenantId()` extension.
  *Done when:* a temporary protected endpoint accepts a valid `X-API-Key` and returns 401 for a missing or wrong key.

## B3. Sending

- [x] **B3.1 Delivery processor**
  `NotificationDataAccess`, `DeliveryLogDataAccess` and `DeliveryProcessor` (BE-02 section 6.4). Unit tests with a fake `IEmailSender`.
  *Done when:* tests pass for success, retry then success, final failure and the already-delivered skip.

- [ ] **B3.2 Notifications service and send endpoint**
  `SesEmailSender`, `NotificationService` (create, enqueue, handle enqueue failure), `LocalNotificationQueue` with `LocalDeliveryWorker` (3 attempts), and `POST /api/notifications` with API key auth. Unit tests for `NotificationService`.
  *Done when:* sending to a verified address delivers a real email and the status becomes `delivered`. Sending to an unverified address ends as `failed` with 3 delivery logs.

## B4. Dashboard API (the frontend can start after this milestone)

- [ ] **B4.1 Dashboard read endpoints**
  `DashboardService` and the Dashboard controller: `stats`, `notifications` list with status filter and paging, and `notifications/{id}` with delivery attempts.
  *Done when:* each endpoint matches the contract in BE-02 section 5, and an unknown or another tenant's id returns 404.

- [ ] **B4.2 Test email and key regeneration**
  `POST /api/dashboard/test-email` and `POST /api/tenants/keys`.
  *Done when:* a test email is sent as the logged-in tenant, and after regenerating, the old key returns 401 and the new key works.

- [ ] **B4.3 Public read endpoints and isolation check**
  `GET /api/notifications` and `GET /api/notifications/{id}` for API key callers.
  *Done when:* with two tenants registered, each only ever sees its own data on every endpoint, and every response matches the contract in section 5.

## B5. AWS pieces

- [ ] **B5.1 SQS queue**
  `SqsNotificationQueue` and the `Program.cs` switch: SQS when `Queue__Url` is set, otherwise the local queue.
  *Done when:* it builds and local mode still works. Real behaviour is verified in B6.

- [ ] **B5.2 Worker Lambda**
  `Worker/Function.cs`: service provider built once, reads `ApproximateReceiveCount` as the attempt number, calls `DeliveryProcessor`, throws on `Retry` or `FailedFinal`.
  *Done when:* it builds. Real behaviour is verified in B6.

## B6. Deploy (manual, AWS console)

- [ ] **B6.1 One-time AWS setup**
  Budget alert, Atlas M0 cluster, SES sender verification, SQS queue and DLQ (visibility timeout 180 s, max receives 3), IAM roles (BE-02 section 8.1).
  *Done when:* a test message can be sent to the queue in the console and the sender address shows as verified.

- [ ] **B6.2 Lambdas and API Gateway**
  Publish and zip both projects, create the two Lambdas with environment variables, the SQS trigger (batch size 1) and the HTTP API (BE-02 sections 8.2, 8.3, 8.5).
  *Done when:* the API Gateway URL returns 200 on `/api/health`.

- [ ] **B6.3 CloudFront and smoke test**
  Done together with FE task F6.1 (the `/api/*` behaviour is described in BE-02 section 8.4). Then run the smoke test in section 8.6.
  *Done when:* all five smoke-test checks pass, including 3 failed attempts and a message in the DLQ.
