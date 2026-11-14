# Notification Platform — Backend Plan (v5)

.NET 10 | Layered class libraries | Plain JWT + API key | SQS + SES | MongoDB Atlas | Manual AWS setup

> **Purpose of this file:** what to build on the backend and in what order. Hand it to Claude Code together with `BE-02-details.md` (the full technical specification). `BE-03-interview-guide.md` is interview preparation only. Do not use it as build instructions. The frontend has its own three files (`FE-01`, `FE-02`, `FE-03`) and depends on the API contract in `BE-02-details.md`, section 5.

---

## 1. What We Are Building

A multi-tenant email notification platform. Third-party apps send emails through one REST call. The platform queues the email, delivers it through Amazon SES, retries on failure, and records every attempt. A dashboard (the frontend project) lets each tenant watch delivery status and inspect failures.

**One-line summary:** a plug-and-play email notification backend that handles queuing, delivery, retries and tracking, so developers don't have to build it themselves.

---

## 2. Locked Decisions

| # | Decision | Choice |
| --- | --- | --- |
| 1 | Channels | **Email only** |
| 2 | Dashboard auth | **Plain JWT** in the `Authorization: Bearer` header. No cookies, no sessions, no refresh tokens, no CSRF |
| 3 | Tenant (third-party app) auth | **API key** in the `X-API-Key` header, stored as a SHA-256 hash |
| 4 | Email sending | **Amazon SES everywhere**, local and production. No Mailpit, no SMTP |
| 5 | Backend structure | **Layered class libraries** (Option A, section 6): Domain, Services, DataAccess, Integrations, plus Api and Worker hosts |
| 6 | Queue | **One SQS queue + dead-letter queue**. No SNS |
| 7 | Database | **MongoDB Atlas M0**, in the same AWS region as everything else |
| 8 | Runtime | **.NET 10** on AWS Lambda (`dotnet10`) |
| 9 | Infrastructure | **Created manually in the AWS console.** No SAM, no CloudFormation, no CI/CD |
| 10 | Secrets and config | **Lambda environment variables.** No SSM, no Secrets Manager |
| 11 | Complexity | **Simple beats clever.** If a choice is between "more robust" and "fewer moving parts", pick fewer parts |

---

## 3. Scope

**In scope:** everything in this plan and in `BE-02-details.md`.

**Out of scope (do not build):** more channels (webhook, in-app, SMS), SNS fan-out, cookie sessions, refresh tokens, token revocation, password reset or email verification, SSM or Secrets Manager, infrastructure as code, CI/CD, DLQ alarms or dashboards, rate limiting, templates, HTML email, scheduled sends, search, WebSockets or SSE, Redis, Dockerfile for the API, custom domain. These are discussed as future scope in the interview guide.

---

## 4. Tech Stack

| Layer | Technology |
| --- | --- |
| API | .NET 10, ASP.NET Core controllers, on AWS Lambda (API Gateway HTTP API). Same code runs on Kestrel locally |
| Worker | .NET 10 Lambda, SQS trigger |
| Queue | Amazon SQS (standard) + dead-letter queue |
| Email | Amazon SES |
| Database | MongoDB Atlas M0 |
| Front door | API Gateway (HTTP API) |
| Edge | CloudFront in front of API Gateway and the S3 bucket that holds the frontend |
| Local dev | Docker Compose (MongoDB only) and `dotnet run` |

**NuGet packages per project** (use the latest versions compatible with .NET 10):

| Project | Packages |
| --- | --- |
| Domain | none |
| Services | `System.IdentityModel.Tokens.Jwt`, `BCrypt.Net-Next` |
| DataAccess | `MongoDB.Driver` |
| Integrations | `AWSSDK.SimpleEmailV2`, `AWSSDK.SQS` |
| Api | `Amazon.Lambda.AspNetCoreServer.Hosting`, `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Worker | `Amazon.Lambda.Core`, `Amazon.Lambda.SQSEvents`, `Amazon.Lambda.Serialization.SystemTextJson` |
| Tests | `xunit`, `NSubstitute` (or Moq) |

**Version and package notes (decided during B1.1):**
- `BE/global.json` pins the SDK to `10.0.100` with `rollForward: latestFeature` (any 10.0.x SDK, never 11), because a newer preview SDK may be installed side by side.
- "Latest compatible with .NET 10" means the **10.0.x** line for Microsoft packages: `Microsoft.AspNetCore.Authentication.JwtBearer` and all `Microsoft.Extensions.*` packages are pinned to `10.0.15`, because their newest versions (11.0.0) belong to .NET 11.
- Small framework packages are also allowed where the `AddXxx()` methods and options need them (none are MongoDB or AWS): Services adds `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`. DataAccess adds `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Options.DataAnnotations` and `Microsoft.Extensions.Logging.Abstractions`. Integrations adds `Microsoft.Extensions.Configuration.Abstractions`.
- The solution file is the classic `.sln` format (`dotnet new sln --format sln`), not `.slnx`.
- The Worker `.csproj` sets `GenerateRuntimeConfigurationFiles`, `AWSProjectType=Lambda` and `CopyLocalLockFileAssemblies`.

---

## 5. System at a Glance

```
Browser / 3rd-party app
        │
        ▼
CloudFront (one domain)
   ├── /*      → S3 (private bucket, Angular build)        ← frontend project
   └── /api/*  → API Gateway → Lambda: .NET API ──► MongoDB Atlas
                                      │
                                      ▼ (notification id)
                               SQS queue ──(3 failed receives)──► DLQ
                                      │
                                      ▼
                               Lambda: .NET Worker ──► SES ──► recipient
                                      │
                                      └──► MongoDB Atlas (status + delivery log)
```

1. A tenant's app calls `POST /api/notifications` with its API key. The API stores the notification as `queued`, puts its id on SQS and returns `202`.
2. SQS triggers the worker, which sends the email through SES and records the result.
3. On failure SQS retries up to 3 times, then moves the message to the DLQ.
4. The dashboard polls the API for status.

Full detail is in `BE-02-details.md`.

---

## 6. Solution Structure (Option A)

### 6.1 Workspace

```
notification-delivery-system/               repo root
├── README.md
├── .gitignore
├── docker-compose.yml                      MongoDB only (local dev)
├── docs/                                   the BE and FE markdown files
├── BE/                                     the .NET solution lives here (this plan)
└── FE/                                     the Angular app lives here (see the FE files)
```

`BE/` is only the folder. The .NET solution and projects keep the `NotificationPlatform.*` names below. Do not name any project `BE`.

### 6.2 .NET solution

```
BE/
├── NotificationPlatform.sln
├── src/
│   ├── NotificationPlatform.Domain/              domain models and constants only
│   │   ├── DomainModels/                         Tenant, Notification, DeliveryLog
│   │   └── Constants/                            NotificationStatus, DeliveryStatus
│   │
│   ├── NotificationPlatform.Services/            business logic AND the interfaces it needs
│   │   ├── Abstractions/                         ITenantDataAccess, INotificationDataAccess,
│   │   │                                         IDeliveryLogDataAccess, IEmailSender, INotificationQueue
│   │   ├── Auth/                                 AuthService, TokenService, ApiKeyService, JwtOptions
│   │   ├── Notifications/                        NotificationService
│   │   ├── Dashboard/                            DashboardService
│   │   ├── Delivery/                             DeliveryProcessor
│   │   └── ServiceCollectionExtensions.cs        AddServices()
│   │
│   ├── NotificationPlatform.DataAccess/          MongoDB implementations
│   │   ├── MongoOptions.cs, MongoMappings.cs     options; BSON conventions (camelCase, ignore extra fields)
│   │   ├── MongoContext.cs                       client + database, generic GetCollection<T>()
│   │   ├── MongoDataAccessBase.cs                generic base every Mongo DAL inherits
│   │   ├── MongoIndexInitializer.cs
│   │   ├── TenantDataAccess.cs, NotificationDataAccess.cs, DeliveryLogDataAccess.cs
│   │   └── ServiceCollectionExtensions.cs        AddDataAccess(config)
│   │
│   ├── NotificationPlatform.Integrations/        AWS adapters
│   │   ├── Email/SesEmailSender.cs
│   │   ├── Queue/SqsNotificationQueue.cs
│   │   └── ServiceCollectionExtensions.cs        AddIntegrations(config)
│   │
│   ├── NotificationPlatform.Api/                 host 1: HTTP (Lambda + Kestrel)
│   │   ├── Controllers/                          Auth, Tenants, Notifications, Dashboard, Health
│   │   ├── Auth/                                 ApiKeyAuthenticationHandler, ClaimsPrincipalExtensions
│   │   ├── Queue/                                LocalNotificationQueue, LocalDeliveryWorker
│   │   ├── DTOs/                                 request and response types
│   │   └── Program.cs                            composition root
│   │
│   └── NotificationPlatform.Worker/              host 2: SQS-triggered Lambda
│       └── Function.cs
│
└── tests/
    └── NotificationPlatform.Services.Tests/      xUnit, mocks of the Services abstractions
```

### 6.3 Dependency rules

```
Api / Worker ──► DataAccess ───┐
             ──► Integrations ─┼──► Services ──► Domain
             ──► Services ─────┘
```

| Project | May reference | Must not reference |
| --- | --- | --- |
| **Domain** | nothing | everything else |
| **Services** | Domain | DataAccess, Integrations, Api, Worker, MongoDB or AWS packages |
| **DataAccess** | Services, Domain | Integrations, Api, Worker |
| **Integrations** | Services, Domain | DataAccess, Api, Worker |
| **Api / Worker** | all of the above | (they are the composition roots) |

### 6.4 Rules

1. **The consumer owns the abstraction.** Interfaces live in `Services/Abstractions`, because Services is the project that uses them. DataAccess and Integrations implement them. Never put an interface in the same project as its implementation.
2. **Domain has no references and no NuGet packages.** Domain models (folder `DomainModels/`, namespace `NotificationPlatform.Domain.DomainModels`) are plain C# classes with no MongoDB attributes. DataAccess maps them with driver conventions instead (BE-02 section 6.3). Ids are strings generated in DataAccess with `ObjectId.GenerateNewId().ToString()`.
3. **Services never references MongoDB or AWS packages.**
4. **Hosts only wire things up.** Api and Worker call `AddServices()`, `AddDataAccess(config)` and `AddIntegrations(config)`. Controllers call Services only, never a data access class.
5. **Controllers never return entities.** Map to response DTOs so `passwordHash` and `apiKeyHash` can't leak.
6. **One `DeliveryProcessor`** in Services is used by both the Worker Lambda and the API's local mode.
7. **Exactly these six `src` projects plus one test project.** Do not add more.

---

## 7. Build Milestones

Build in this order. Run and verify each milestone before moving on. The frontend can start once B4 is done.

| Milestone | Build | Done when |
| --- | --- | --- |
| **B1** Foundation | Solution with the six `src` projects and the test project, referenced exactly as in section 6.3. Entities, abstractions, `MongoContext`, index creation. `GET /api/health`. `docker-compose.yml` with MongoDB | `dotnet build` passes, `docker compose up` starts Mongo, health returns 200, indexes exist, and no project references violate the rules |
| **B2** Auth | `AuthService`, `TokenService`, BCrypt hashing. Register, login, me. JWT bearer scheme. API key generation and the `ApiKey` authentication handler. The consistent error shape. Unit tests | Register returns a token and an API key. `/auth/me` works with the token and returns 401 without it. A wrong password is rejected. A short password returns a 400 with field errors |
| **B3** Sending | `POST /notifications`, `NotificationService`, `LocalNotificationQueue` and `LocalDeliveryWorker`, `DeliveryProcessor`, `SesEmailSender`, 3 attempts. Unit tests for `DeliveryProcessor` with a fake `IEmailSender` | Sending with the API key delivers a real email to a verified address and the status becomes `delivered`. Sending to an unverified address ends as `failed` with 3 delivery logs |
| **B4** Dashboard API | Stats, notification list and detail, test-email, key regeneration, plus `GET /notifications` list and detail for API-key callers | Each endpoint returns only the authenticated tenant's data (test with two tenants) and matches the contract in `BE-02-details.md` section 5 |
| **B5** AWS pieces | `SqsNotificationQueue`, worker `Function.cs` | Code builds. Behavior is verified in B6 |
| **B6** Deploy | Follow the AWS runbook in `BE-02-details.md`, then its smoke test | All smoke-test checks pass |

---

## 8. Starter Prompt for Claude Code

> Read `docs/BE-01-plan.md` and `docs/BE-02-details.md`. The repo root is `notification-delivery-system`. Create the .NET solution directly inside the existing empty `BE/` folder (`BE/NotificationPlatform.sln`, `BE/src/...`, `BE/tests/...`), keeping the `NotificationPlatform.*` project names. Do not touch `FE/`. Build the backend exactly as specified, following the milestone order in the plan and the project dependency rules in section 6.3. Keep it simple. Do not build anything listed as out of scope. The response shapes in `BE-02-details.md` section 5 are a contract with the frontend, so match them exactly. Start with B1 and stop after each milestone so I can verify it.

---

## 9. Change Log

Decisions made during the build that change or refine this plan. Each one is also applied in place above.

| Date | Milestone | Change | Sections |
| --- | --- | --- | --- |
| 2026-11-13 | B1 planning | Domain folder is `DomainModels/` (namespace `NotificationPlatform.Domain.DomainModels`) instead of `Entities/` | 6.2, 6.4 |
| 2026-11-14 | B1.1 | `global.json` pins SDK 10.0.x. Classic `.sln` format. Microsoft packages pinned to the 10.0.x line (10.0.15). Extra framework-only `Microsoft.Extensions.*` packages allowed in Services, DataAccess and Integrations. Worker `.csproj` has the Lambda build properties | 4 |
| 2026-11-15 | B1.2 | DataAccess gains `MongoOptions`, `MongoMappings` (conventions, no class maps) and the generic `MongoDataAccessBase<T>`. `MongoContext` exposes `GetCollection<T>()` instead of one property per collection | 6.2, 6.4 |
