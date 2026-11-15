# Notification Platform — Backend Technical Details (v5)

Full specification for the build plan in `BE-01-plan.md`. Section 5 is the API contract the frontend relies on.

---

## 1. Architecture and Flows

### 1.1 Components

| Piece | Role |
| --- | --- |
| CloudFront | One public domain. Serves the dashboard from S3 and forwards `/api/*` to API Gateway |
| S3 (private) | Holds the Angular build. Only CloudFront can read it (Origin Access Control) |
| API Gateway (HTTP API) | Front door with one catch-all route to the API Lambda |
| API Lambda | The whole ASP.NET Core app in one function |
| SQS + DLQ | Buffer and retry mechanism between the API and the worker |
| Worker Lambda | Reads one message at a time, sends the email through SES, records the result |
| SES | Sends the email |
| MongoDB Atlas M0 | Stores tenants, notifications and delivery logs |
| CloudWatch Logs | Default Lambda logging |

### 1.2 Diagram

```
Browser / 3rd-party app
        │
        ▼
CloudFront (one domain)
   ├── /*      → S3 (private bucket, Angular build)
   └── /api/*  → API Gateway (HTTP API) → Lambda: .NET API ──► MongoDB Atlas
                                                │
                                                ▼  (SendMessage: notification id)
                                         SQS: notification-email-queue ──(3 failed receives)──► DLQ
                                                │
                                                ▼  (batch size 1)
                                         Lambda: .NET Worker ──► SES ──► recipient
                                                │
                                                └──► MongoDB Atlas (status + delivery log)
```

### 1.3 Send flow

1. A third-party app calls `POST /api/notifications` with `X-API-Key`.
2. The API hashes the key, finds the tenant, validates the body, inserts a `notifications` document with status `queued`, sends the notification id to SQS, and returns `202 Accepted` with the id.
3. If sending to SQS fails after the insert, the API sets the notification to `failed` with the error in `lastError` and returns `503`.
4. SQS triggers the worker Lambda with one message (batch size 1).
5. The worker calls `DeliveryProcessor` (section 6.4) and, if delivery failed, throws so SQS retries the message.
6. The dashboard polls the API to show live status.

### 1.4 Retry rule

- The queue's **maximum receives is 3**, so a message gets 3 attempts. After the third failed receive SQS moves it to the DLQ.
- The worker reads `ApproximateReceiveCount` from the SQS record and uses it as the attempt number (1, 2, 3).
- On attempt 3 the processor marks the notification `failed`. The `maxAttempts` constant (3) must match the queue's maximum receives.
- Between attempts the message waits for the queue's **visibility timeout (180 seconds)**, so retries are about 3 minutes apart.

### 1.5 Local mode

The same code runs locally. Only the queue differs, chosen in the API's `Program.cs`:

| `Queue__Url` | Queue | Email |
| --- | --- | --- |
| set (production) | `SqsNotificationQueue` sends to SQS. The worker Lambda consumes it | `SesEmailSender` |
| empty (local) | `LocalNotificationQueue`: an in-process `Channel<string>` plus a hosted background service (`LocalDeliveryWorker`) that calls the same `DeliveryProcessor`, up to 3 attempts with a short delay between them | `SesEmailSender` (real SES) |

This lets you run the whole flow on your machine without deploying the worker Lambda, while still sending real email through SES.

---

## 2. Authentication

Two patterns for two kinds of caller.

| Aspect | Dashboard (human) | Tenant API (machine) |
| --- | --- | --- |
| Credential | JWT in the `Authorization: Bearer` header | `X-API-Key` header |
| Lifetime | 60 minutes (`Jwt__ExpiryMinutes`), then log in again | Long-lived, regenerated on demand |
| Validation | Verify signature, issuer, audience and expiry. No database lookup | SHA-256 hash lookup in MongoDB |
| Revocation | Not supported. The client discards the token | Regenerate the key |
| Protects | `/api/auth/me`, `/api/tenants/*`, `/api/dashboard/*` | `/api/notifications*` |

### 2.1 Dashboard JWT

- `POST /api/auth/register` and `POST /api/auth/login` return `{ token, expiresAt, tenant }`. Registration also returns the new API key, **once**.
- The token is HS256, signed with `Jwt__Secret` (at least 32 characters). Fixed issuer and audience strings in code. Claims: `sub` (tenant id), `tenant_id` (the same value, read by `User.GetTenantId()`), `email`, `name`, `exp`.
- Passwords are hashed with BCrypt.
- There is no refresh endpoint and no logout endpoint. When the token expires, or the API returns `401`, the dashboard sends the user to the login page. Logging out means the browser deletes the token.
- Set `ClockSkew` on the validation parameters to 30 seconds. The default is 5 minutes, which would let an expired token keep working for 5 more minutes.
- Implementation (B2.1):
  - Issuer `notification-platform` and audience `notification-platform-dashboard` are constants on `JwtOptions`. Both are validated.
  - `TokenService.CreateToken(tenant)` returns `IssuedToken(Token, ExpiresAt)`, built with `JsonWebTokenHandler` (the handler `JwtBearer` validates with; it ships in the `System.IdentityModel.Tokens.Jwt` package). Times come from an injected `TimeProvider` and are truncated to whole seconds, so `expiresAt` equals the token's `exp`.
  - `TokenService.CreateValidationParameters()` is the single definition of how tokens are validated: issuer, audience, lifetime, signing key, `ClockSkew` 30 s, and `ValidAlgorithms = [HS256]` (pinning the algorithm blocks `alg: none` and algorithm-confusion attacks). `AddJwtBearer` uses it in B2.2, and the unit tests use it too.
  - `JwtOptions` (`[Required, MinLength(32)] Secret`, `[Range(1, 1440)] ExpiryMinutes = 60`) is bound and validated in the **Api host only**, not in `AddServices()`, because the Worker also calls `AddServices()` and has no `Jwt__Secret`.

### 2.2 API key

- Format: `np_` followed by 32 random bytes encoded as base64url.
- Stored: the SHA-256 hex hash (`apiKeyHash`) and a short display prefix (`apiKeyPrefix`, `np_` plus the first 8 characters of the random part). The full key is never stored and is shown to the tenant only once, at registration or regeneration.
- Implementation (B2.1): `ApiKeyService.Generate()` returns `GeneratedApiKey(Key, Hash, Prefix)`. The random part is `Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))` (43 characters, no padding, so a key is 46 characters). `ApiKeyService.Hash(key)` is SHA-256 of the full key including `np_`, as 64 **lowercase** hex characters.

### 2.3 Implementation in ASP.NET Core

Use the built-in authentication system with **two schemes**:

- **`Bearer`** (default scheme): `AddJwtBearer`, with `MapInboundClaims = false` so claim names stay as written. Controllers use `[Authorize]`.
- **`ApiKey`**: a small custom `AuthenticationHandler` in the Api project. It reads `X-API-Key`, hashes it with SHA-256, looks up the tenant through `ITenantDataAccess`, and builds the principal. The notifications controller uses `[Authorize(AuthenticationSchemes = "ApiKey")]`.
- Both schemes add a `tenant_id` claim. A helper `User.GetTenantId()` (an extension method on `ClaimsPrincipal`) reads it.
- **A controller never takes the tenant id from the request body or query string.** Every query is scoped to the authenticated tenant.
- Compare hashes with `CryptographicOperations.FixedTimeEquals`.

---

## 3. Configuration (Environment Variables)

.NET maps `Section__Key` environment variables to `Section:Key`. Use the same names locally and in Lambda. Bind them to strongly typed options classes (`JwtOptions`, `MongoOptions`, `EmailOptions`, `QueueOptions`) with the options pattern, and validate them on startup so a missing value fails fast (`ValidateDataAnnotations().ValidateOnStart()`). Each options class is added in the milestone whose code uses it (`MongoOptions` in B1.2, `JwtOptions` in B2.1, `EmailOptions` in B3.2, `QueueOptions` in B5.1), so earlier milestones don't require secrets they don't use.

**API Lambda**

| Variable | Meaning |
| --- | --- |
| `Mongo__ConnectionString` | Atlas `mongodb+srv://...` connection string |
| `Mongo__Database` | `notifications` |
| `Jwt__Secret` | Random string, at least 32 characters |
| `Jwt__ExpiryMinutes` | Optional, default `60` |
| `Queue__Url` | The SQS queue URL. **Leave empty locally** |

**Worker Lambda**

| Variable | Meaning |
| --- | --- |
| `Mongo__ConnectionString` | Same as above |
| `Mongo__Database` | `notifications` |
| `Email__From` | The SES-verified sender address |

**API, local only** (local mode sends mail itself): `Email__From`, plus AWS credentials and region from your normal AWS profile (`AWS_PROFILE`, `AWS_REGION`).

Lambda environment variables are encrypted at rest by default, but anyone with read access to the function in the console can see them. That is acceptable for a personal project.

---

## 4. MongoDB Data Models

Ids are strings (an ObjectId hex string). All timestamps are UTC.

**tenants**
```json
{
  "_id": "string",
  "name": "Acme Inc",
  "email": "admin@acme.com",
  "passwordHash": "bcrypt",
  "apiKeyHash": "sha256-hex",
  "apiKeyPrefix": "np_ab12cd34",
  "createdAt": "..."
}
```

**notifications**
```json
{
  "_id": "string",
  "tenantId": "string",
  "to": "user@example.com",
  "subject": "Your order has shipped",
  "body": "Your order #1234 is on its way.",
  "status": "queued | processing | delivered | failed",
  "attempts": 0,
  "lastError": null,
  "createdAt": "...",
  "updatedAt": "...",
  "deliveredAt": null
}
```

**deliveryLogs** (one document per attempt, kept separate so retries don't grow the notification document)
```json
{
  "_id": "string",
  "notificationId": "string",
  "tenantId": "string",
  "attempt": 1,
  "status": "success | failed",
  "errorMessage": "…",
  "attemptedAt": "..."
}
```

**Indexes** (created at API startup by `MongoIndexInitializer.EnsureIndexesAsync()`, called in `Program.cs` right after `Build()`. Idempotent. All five creations sit in **one** try/catch that logs the error and lets the API start, so an unreachable database costs one server-selection timeout, not five)

| Collection | Index | Why |
| --- | --- | --- |
| tenants | `{ email: 1 }` unique | Login lookup, and duplicate email protection |
| tenants | `{ apiKeyHash: 1 }` unique | API key lookup on every request |
| notifications | `{ tenantId: 1, createdAt: -1 }` | Dashboard list |
| notifications | `{ tenantId: 1, status: 1 }` | Stats and status filter |
| deliveryLogs | `{ notificationId: 1 }` | Attempts per notification |

---

## 5. API Contract

All controllers use the route prefix `api` (for example `[Route("api/auth")]`), because CloudFront forwards the full path `/api/...` to API Gateway unchanged. JSON uses **camelCase**. Timestamps are ISO 8601 UTC strings (for example `2026-11-12T08:15:30Z`). Status values are lowercase strings.

### 5.1 Response and request shapes

```
Tenant            { id, name, email, apiKeyPrefix, createdAt }

AuthResponse      { token, expiresAt, tenant }            // login
RegisterResponse  { token, expiresAt, tenant, apiKey }    // register: apiKey shown once

NotificationItem  { id, to, subject, status, attempts, createdAt, updatedAt }

NotificationDetail {
  id, to, subject, body, status, attempts, lastError,
  createdAt, updatedAt, deliveredAt,
  deliveryAttempts: [ { attempt, status, errorMessage, attemptedAt } ]   // oldest first
}

Page<T>           { items: T[], page, pageSize, total }   // page is 1-based

Stats             { queued, processing, delivered, failed }

SendRequest       { to, subject, body }
SendResponse      { id, status }                           // 202

RegenerateResponse { apiKey, apiKeyPrefix }

Error             { error: "message", fields?: { fieldName: ["message", ...] } }
```

**Error shape.** Every error response uses the `Error` shape. Validation failures return `400` with `error: "Validation failed"` and a `fields` object keyed by camelCase field name. Configure `ApiBehaviorOptions.InvalidModelStateResponseFactory` so that `[ApiController]` model validation produces this shape instead of the default ProblemDetails. An unhandled exception returns `500` with `{ error: "Something went wrong" }` through global exception-handling middleware, and the details go to the logs only.

How every error gets this shape (decided in B1.3):

| Error | Produced by |
| --- | --- |
| Validation `400` | `ApiBehaviorOptions.InvalidModelStateResponseFactory` (`Validation/ValidationErrorResponse.cs`) |
| Unhandled exception `500` | `Middleware/ExceptionHandlingMiddleware.cs`, registered first. If the client disconnected (`RequestAborted`), it logs and writes nothing. If the response already started, it rethrows |
| Bodiless `4xx`: `401`/`403` from the authentication handlers, unknown route `404`, `405`, a controller's `NotFound()` | `SuppressMapClientErrors = true` (so `[ApiController]` doesn't turn them into ProblemDetails) plus `app.UseStatusCodePages(...)` (`Validation/ErrorStatusCodePages.cs`) with fixed messages: "Unauthorized", "Forbidden", "Not found", "Method not allowed" |
| Errors with a specific message (`409` duplicate email, `401` "Incorrect email or password.", `503` enqueue failure) | The controller returns `ErrorResponse` itself |

`fields` details:
- Keys are camelCase field names. DataAnnotations errors (keyed by property name, `Password`) and JSON errors (keyed by JSON path, `$.password`) are both normalised to `password`. A missing or unreadable body is reported under `body`.
- Raw JSON parser messages are not returned (`AllowInputFormatterExceptionMessages = false`), because they contain internal type names. A type mismatch or malformed JSON shows "The value is invalid." under the affected field.
- When the body is missing or unreadable, the extra "The request field is required." error for the action parameter is dropped.

### 5.2 Auth

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| POST | `/auth/register` | none | Body `{ name, email, password }`. Create tenant and API key. Returns `RegisterResponse`. Duplicate email returns `409` |
| POST | `/auth/login` | none | Body `{ email, password }`. Returns `AuthResponse`. Wrong credentials return `401` with the message "Incorrect email or password." (same message for unknown email and wrong password) |
| GET | `/auth/me` | Bearer | Returns `Tenant` |

### 5.3 Tenant

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| POST | `/tenants/keys` | Bearer | Regenerate the API key. Returns `RegenerateResponse` once. The old key stops working immediately |

### 5.4 Dashboard

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/dashboard/stats` | Bearer | Returns `Stats` |
| GET | `/dashboard/notifications?status=&page=&pageSize=` | Bearer | Returns `Page<NotificationItem>`, newest first. `status` is optional |
| GET | `/dashboard/notifications/{id}` | Bearer | Returns `NotificationDetail`. Unknown id or another tenant's id returns `404` |
| POST | `/dashboard/test-email` | Bearer | Body `SendRequest`. Returns `202 SendResponse`. Sends as the logged-in tenant |

### 5.5 Public API

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| POST | `/notifications` | API key | Body `SendRequest`. Returns `202 SendResponse` |
| GET | `/notifications?page=&pageSize=` | API key | Returns `Page<NotificationItem>` for the key's tenant |
| GET | `/notifications/{id}` | API key | Returns `NotificationDetail` |

### 5.6 Other

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/health` | none | Returns `200 { status: "ok" }`. Used for smoke tests. Doesn't check the database |

### 5.7 Validation rules

| Field | Rule |
| --- | --- |
| `name` | Required, at most 100 characters |
| `email` | Required, valid email address, at most 254 characters, stored lowercase |
| `password` | Required, at least 8 characters, at most 100 |
| `to` | Required, valid email address |
| `subject` | Required, at most 200 characters |
| `body` | Required, plain text, at most 10,000 characters |
| `status` (filter) | Optional, one of `queued`, `processing`, `delivered`, `failed`, otherwise `400` |
| `page` | Default 1, minimum 1 |
| `pageSize` | Default 10, minimum 1, capped at 100 |

Every notification query filters by the authenticated `tenantId`. A notification id belonging to another tenant returns `404`, not `403`.

---

## 6. Layer Responsibilities

### 6.1 Domain

Plain domain models `Tenant`, `Notification`, `DeliveryLog` in the `DomainModels/` folder (namespace `NotificationPlatform.Domain.DomainModels`), and string constants for statuses in `Constants/` (`NotificationStatus` also exposes an `All` list used by the status filter validation). No logic that needs external services.

### 6.2 Services

| Class | Responsibility |
| --- | --- |
| `AuthService` | Register (create tenant, hash password, generate API key), login (verify password, issue token), get current tenant |
| `TokenService` | Create and describe the JWT (`CreateToken`, `CreateValidationParameters`). Reads `JwtOptions` and `TimeProvider` |
| `ApiKeyService` | Generate a key, compute its SHA-256 hash and display prefix (`Generate`, `Hash`), regenerate for a tenant (added in B4.2) |

`AddServices()` registers `TimeProvider.System` (with `TryAddSingleton`, so tests or hosts can replace it), `TokenService` and `ApiKeyService` as singletons. They are stateless and thread-safe.
| `NotificationService` | Create a notification, enqueue it, list and get notifications for a tenant. Handles the enqueue-failure case from section 1.3 |
| `DashboardService` | Stats (four count queries by status), paged list with status filter, detail with delivery attempts |
| `DeliveryProcessor` | Delivers one notification (see 6.4) |

**Abstractions** (in `Services/Abstractions`):

| Interface | Methods (indicative) |
| --- | --- |
| `ITenantDataAccess` | `GetByIdAsync`, `GetByEmailAsync`, `GetByApiKeyHashAsync`, `InsertAsync`, `UpdateApiKeyAsync` |
| `INotificationDataAccess` | `InsertAsync`, `GetByIdAsync(tenantId, id)`, `GetByIdForProcessingAsync(id)`, `MarkProcessingAsync(id, attempt)` (status `processing`, `attempts`, `updatedAt`), `UpdateStatusAsync(id, status, lastError, deliveredAt)` (sets all three plus `updatedAt`; a null `lastError` clears it), `ListAsync(tenantId, status, page, pageSize)` returning `(Items, Total)` newest first, `CountByStatusAsync(tenantId, status)` |
| `IDeliveryLogDataAccess` | `InsertAsync`, `GetByNotificationAsync(notificationId)` (oldest first) |
| `IEmailSender` | `SendAsync(to, subject, body, ct)` |
| `INotificationQueue` | `EnqueueAsync(notificationId, ct)` |

Every method is async and takes `CancellationToken ct = default` as its last parameter, so cancellation (an aborted HTTP request, host shutdown) can flow from the controller or worker down to the MongoDB driver and AWS SDK. `InsertAsync` methods set the model's `Id`.

### 6.3 DataAccess and Integrations

- **DataAccess:**
  - `MongoOptions` (section `Mongo`, both values `[Required]`).
  - `MongoContext` (singleton) holds the one `MongoClient` and the database, created once and reused across requests and Lambda invocations. It has no property per collection: `GetCollection<T>()` resolves a collection from the model type, named **camelCase type name + "s"** (`Tenant` → `tenants`, `DeliveryLog` → `deliveryLogs`). Renaming a model class therefore renames its collection.
  - `MongoDataAccessBase<T>` (internal, abstract) is the base every Mongo data access class inherits, e.g. `TenantDataAccess : MongoDataAccessBase<Tenant>, ITenantDataAccess`. It provides `Collection` and `NewId()` (an ObjectId hex string). One class per interface. Ids are generated here.
  - `MongoMappings` registers BSON **conventions only** (no class maps, no attributes) for the `DomainModels` namespace: `CamelCaseElementNameConvention` and `IgnoreExtraElementsConvention(true)`. Ids rely on driver defaults: a property named `Id` maps to `_id`, and a `string` is stored as a BSON string. `DateTime` values are stored and read back as UTC. `MongoContext` registers the conventions in its constructor, once per process, before any collection is used.
  - `MongoIndexInitializer` creates the indexes (section 4).
  - The Services interfaces are the only thing Services sees, so moving to another database (SQL, DynamoDB) means new implementations of those interfaces and different registrations in `AddDataAccess`. Generics stay inside DataAccess. There is no generic repository interface in Services.
- **Integrations:** `SesEmailSender` uses the SES v2 API with a plain-text body and the `Email__From` address. `SqsNotificationQueue` sends the notification id as the message body.

### 6.4 DeliveryProcessor

`DeliveryProcessor.ProcessAsync(notificationId, attempt, maxAttempts, ct)` returns a result (`Delivered`, `Retry` or `FailedFinal`, plus the error) instead of throwing, so it is easy to test and each host decides what to do.

1. Load the notification. If it doesn't exist, return `Delivered` (nothing to do). If its status is already `delivered`, return `Delivered`. This makes duplicate SQS deliveries harmless.
2. Set status `processing` and `attempts = attempt` (`MarkProcessingAsync`).
3. Call `IEmailSender.SendAsync`.
4. **Success:** insert a delivery log (`success`), set status `delivered`, set `deliveredAt`, clear `lastError`.
5. **Failure:** insert a delivery log (`failed`, with the error message), set `lastError`. If `attempt >= maxAttempts`, set status `failed` and return `FailedFinal`. Otherwise set status `queued` and return `Retry`.

**Cancellation:** the token is passed to the load and to `IEmailSender.SendAsync`, but not to the delivery-log insert and status update that follow a successful send, so a cancellation can't leave an email sent with no record of it.

Callers:
- **Worker Lambda:** `attempt` comes from `ApproximateReceiveCount`. On `Retry` or `FailedFinal` it **throws**, so SQS retries the message or moves it to the DLQ.
- **LocalDeliveryWorker:** loops up to `maxAttempts` with a short delay while the result is `Retry`.

### 6.5 Api and Worker

- **Api `Program.cs`:** the composition root. Calls `AddServices()`, `AddDataAccess(config)`, `AddIntegrations(config)`, configures both authentication schemes, the global exception middleware and the validation error shape, chooses the queue (SQS if `Queue__Url` is set, otherwise the local queue and worker), calls `AddAWSLambdaHosting(LambdaEventSource.HttpApi)`, and kicks off index creation. Pipeline order: `ExceptionHandlingMiddleware` → `UseStatusCodePages` → `UseAuthentication` (B2) → `UseAuthorization` → `MapControllers`. `AddAWSLambdaHosting` is wired from B1.3 (it has no effect under Kestrel). The local port is fixed at **5080** in `Properties/launchSettings.json`.
- **Worker `Function.cs`:** builds the service provider once in a static constructor (so it is reused between invocations) and exposes `FunctionHandler(SQSEvent, ILambdaContext)`.

---

## 7. Local Development

0. **SDK:** the .NET 10 SDK. `BE/global.json` makes `dotnet` use it even when a newer SDK is installed.
1. **MongoDB:** `docker compose up -d` from the repo root (Docker Desktop must be running). The compose file contains only the official `mongo:8` image, published on `127.0.0.1:27017` only (the container has no authentication, so it must not be reachable from the network), with a named volume `mongo-data` for `/data/db`. The data lives in Docker's own storage, not in the repo folder (the repo is under OneDrive, which must not sync live database files). `docker compose stop` keeps the data. `docker compose down -v` deletes it. Inspect with `docker compose exec mongo mongosh notifications`. Alternatively, point `Mongo__ConnectionString` at your Atlas cluster and skip Docker.
2. **API:** `dotnet run --project src/NotificationPlatform.Api`. Keep local settings in `appsettings.Development.json` (git-ignored) or .NET user-secrets: `Mongo__ConnectionString=mongodb://localhost:27017`, `Mongo__Database`, `Jwt__Secret`, `Email__From`, and no `Queue__Url`. Use a fixed local port, for example 5080, that the frontend's dev proxy points to.
3. **Email locally uses real SES.** You need an AWS profile on your machine (`aws configure`) whose IAM user can call `ses:SendEmail`, and `AWS_REGION` set to the region where you verified your sender. While SES is in the sandbox, verify every recipient address you test with.

**Local check:** register through the API (curl or Postman), send a notification with the API key to a verified address, and see it arrive with status `delivered`. To test failure handling, send to an unverified address: SES rejects it, and you should see 3 failed attempts and a final `failed` status.

---

## 8. Manual AWS Setup Runbook

Pick **one region** and use it for everything, including the Atlas cluster. Do the steps in this order.

### 8.1 One-time setup

1. **Budget alert.** In Billing, create a monthly budget of a few dollars with an email alert. Do this first.
2. **MongoDB Atlas.** Create an M0 cluster in your chosen AWS region. Create one database user with read/write on the `notifications` database. Under Network Access, allow `0.0.0.0/0` (M0 has no private networking and Lambda has no fixed IP). Copy the connection string.
3. **SES.** Verify the sender email address (the `Email__From` value). A new account is in the sandbox, so each recipient address you test with must be verified too. Requesting production access is optional and only needed for real recipients.
4. **SQS.**
   - Create the DLQ `notification-email-dlq` (standard).
   - Create `notification-email-queue` (standard) with visibility timeout **180 seconds** (at least 6 times the worker timeout) and a dead-letter queue pointing at the DLQ with **Maximum receives = 3**.
   - Copy the main queue URL.
5. **IAM roles** (Lambda as the trusted service for both):
   - `notification-worker-role`: the AWS-managed `AWSLambdaSQSQueueExecutionRole` policy, plus an inline policy allowing `ses:SendEmail` and `ses:SendRawEmail`.
   - `notification-api-role`: the AWS-managed `AWSLambdaBasicExecutionRole` policy, plus an inline policy allowing `sqs:SendMessage` on the main queue only.

### 8.2 Lambda functions

Both functions: runtime **.NET 10**, architecture **x86_64**.

| Setting | Worker | API |
| --- | --- | --- |
| Name | `notification-worker` | `notification-api` |
| Role | `notification-worker-role` | `notification-api-role` |
| Handler | `NotificationPlatform.Worker::NotificationPlatform.Worker.Function::FunctionHandler` | `NotificationPlatform.Api` (the assembly name, for minimal hosting with `AddAWSLambdaHosting`) |
| Memory | 256 MB | 512 MB |
| Timeout | 30 s | 15 s |
| Environment | see section 3 | see section 3 |
| Trigger | SQS: `notification-email-queue`, **batch size 1** | none (API Gateway invokes it) |

### 8.3 API Gateway

Create an **HTTP API** using quick create with the `notification-api` Lambda integration. This creates a `$default` route and a `$default` stage with auto-deploy. Copy the invoke URL host (`xxxx.execute-api.<region>.amazonaws.com`).

### 8.4 S3 and CloudFront

The S3 bucket and the CloudFront distribution serve the frontend and route `/api/*` to the backend, so they are described in `FE-02-details.md`, section 10. The backend-specific requirement is the `/api/*` behavior: **all HTTP methods allowed**, cache policy **`CachingDisabled`**, origin request policy **`AllViewerExceptHostHeader`** (this forwards the `Authorization` and `X-API-Key` headers and query strings, while letting API Gateway see its own host).

### 8.5 Deploying code (repeat whenever code changes)

From `BE/`:

```
dotnet publish src/NotificationPlatform.Api    -c Release -r linux-x64 --self-contained false -o publish/api
dotnet publish src/NotificationPlatform.Worker -c Release -r linux-x64 --self-contained false -o publish/worker
```

Zip the **contents** of each publish folder (the DLLs must be at the root of the zip, not inside a subfolder). Upload each zip in the Lambda console under Code → Upload from → .zip file.

### 8.6 Smoke test

1. Open `https://<id>.cloudfront.net/api/health` and check for 200.
2. Register through the dashboard and confirm you land on the API key screen.
3. Send a test email to a **verified** SES address and confirm it arrives and shows `delivered`.
4. Call `POST /api/notifications` with the API key using curl or Postman.
5. Force a failure by sending to an unverified recipient. Confirm 3 failed attempts appear on the detail page, the status becomes `failed`, and the message lands in the DLQ (visible in the SQS console).

### 8.7 Free tier and costs

Lambda, SQS, CloudFront and CloudWatch fit inside the always-free allowances at this scale. API Gateway and SES are not on the always-free list, so they run on the signup credits and then cost very little (about $1 per million API requests, $0.10 per 1,000 emails). Accounts created after 15 July 2025 start on a Free plan that lasts up to 6 months or until credits run out. Upgrade to the Paid plan to keep the deployment running. Always-free services stay free within their limits on both plans.

Expect the first request after idle time to be slower (Lambda cold start).

---

## 9. Testing

Unit tests live in `NotificationPlatform.Services.Tests` and use mocks of the Services abstractions, so no database or AWS is needed.

| Area | Cases |
| --- | --- |
| `TokenService` | Token contains the expected claims and expiry. An expired or tampered token fails validation |
| `AuthService` | Register hashes the password and generates an API key. Duplicate email fails. Login with a wrong password fails |
| `ApiKeyService` | Generated keys have the `np_` prefix. The hash is deterministic. Two keys differ |
| `NotificationService` | Creates a `queued` notification and enqueues its id. If enqueue fails, the notification is marked `failed` |
| `DeliveryProcessor` | Success on first attempt. Failure then success. Final failure at the last attempt. Already-delivered notification is skipped |

End-to-end checks are the manual "done when" checks in the plan's milestones and the smoke test in section 8.6.

---

## 10. Change Log

Decisions made during the build that change or refine this specification. Each one is also applied in place above.

| Date | Milestone | Change | Sections |
| --- | --- | --- | --- |
| 2026-11-13 | B1 planning | Domain models live in `DomainModels/` (namespace `NotificationPlatform.Domain.DomainModels`), not `Entities/` | 6.1 |
| 2026-11-14 | B1.1 | .NET 10 SDK pinned with `BE/global.json` | 7 |
| 2026-11-15 | B1.2 | Options classes are added in the milestone that uses them. Only `MongoOptions` exists after B1 | 3 |
| 2026-11-15 | B1.2 | Index creation runs right after `Build()` in `Program.cs`, inside one try/catch that logs and continues | 4 |
| 2026-11-15 | B1.2 | `INotificationDataAccess` gains `MarkProcessingAsync(id, attempt)`. `UpdateStatusAsync` signature fixed as `(id, status, lastError, deliveredAt)`. `ListAsync` returns `(Items, Total)`. All abstraction methods take `CancellationToken ct = default` | 6.2, 6.4 |
| 2026-11-15 | B1.2 | `MongoContext.GetCollection<T>()` with collection name = camelCase type name + "s". Generic `MongoDataAccessBase<T>` base class for all Mongo DALs. BSON mapping by conventions only (camelCase, ignore extra elements), ids by driver defaults | 6.3 |
| 2026-11-15 | B1.2 | `DeliveryProcessor` doesn't pass the cancellation token to the bookkeeping writes after a successful send | 6.4 |
| 2026-11-15 | B1.2 | docker-compose: `mongo:8`, bound to `127.0.0.1` only, named volume `mongo-data` | 7 |
| 2026-11-15 | B1.3 | Keep the custom `{ error, fields }` shape (not ProblemDetails) and guarantee it for every error: validation factory, exception middleware, `SuppressMapClientErrors` + `UseStatusCodePages` for bodiless 4xx, explicit `ErrorResponse` for specific messages | 5.1 |
| 2026-11-15 | B1.3 | `fields` keys normalised to camelCase (`$.password`/`Password` → `password`, missing body → `body`). Raw JSON parser messages hidden. Parameter-level "required" noise dropped | 5.1 |
| 2026-11-15 | B1.3 | Exception middleware ignores client disconnects and rethrows if the response has started | 5.1 |
| 2026-11-15 | B1.3 | Health doesn't check the database. Pipeline order fixed. Lambda hosting wired now. Port 5080 | 5.6, 6.5 |
| 2026-11-16 | B2.1 | Issuer/audience constants on `JwtOptions`. Tokens built with `JsonWebTokenHandler`. `CreateValidationParameters()` shared by `AddJwtBearer` and tests, with HS256 pinned. Times from `TimeProvider`, truncated to whole seconds | 2.1, 6.2 |
| 2026-11-16 | B2.1 | `JwtOptions` bound and validated in the Api host only, because the Worker has no JWT secret | 2.1 |
| 2026-11-16 | B2.1 | `ApiKeyService.Generate()` returns `GeneratedApiKey(Key, Hash, Prefix)`. Hash is lowercase hex | 2.2 |
