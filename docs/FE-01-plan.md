# Notification Platform — Frontend Plan (v5)

Angular 20 | Standalone components | Signals | `inject()` | New control flow (`@if`, `@for`) | Plain CSS design tokens

> **Purpose of this file:** what to build on the frontend and in what order. Hand it to Claude Code together with `FE-02-details.md` (the full specification, including the UI design). `FE-03-interview-guide.md` is interview preparation only. Do not use it as build instructions. The backend has its own files (`BE-01`, `BE-02`, `BE-03`). The API contract the frontend depends on is in `BE-02-details.md`, section 5.

---

## 1. What We Are Building

The tenant dashboard for the notification platform. A business registers, receives an API key, sends test emails, and watches the delivery status of every email sent through its account, including each attempt and its error.

The UI was designed in Google Stitch (white page, deep navy `#1E3A8A` sidebar and buttons, Inter font, soft status badges). `FE-02-details.md` defines every screen exactly, including what was removed from the Stitch drafts.

---

## 2. Locked Decisions

| # | Decision | Choice |
| --- | --- | --- |
| 1 | Framework | **Angular 20** (use `@angular/cli@20`). Standalone components only, no NgModules |
| 2 | Project | **Separate project** in the repo's `FE/` folder, with its own `package.json`. It is already scaffolded with `ng new` (Angular 20.3, Angular project name **`portal`**, selector prefix `app`, routing and CSS on). Do not run `ng new` again and do not rename the project. Talks to the backend only through the relative path `/api` |
| 3 | State | **Signals** for component and service state. **RxJS** for HTTP and polling. **No NgRx** |
| 4 | Dependency injection | The **`inject()` function**, not constructor injection |
| 5 | Templates | New control flow: **`@if`, `@for` (with `track`), `@switch`, `@empty`, `@let`**. Never `*ngIf` or `*ngFor` |
| 6 | Change detection | **`OnPush`** on every component. Zone.js stays enabled (the default) |
| 7 | Component API | **`input()`, `output()`, `model()`** and signal queries (`viewChild()`), not decorators |
| 8 | Routing | Lazy-loaded routes with `loadComponent`. **Functional guards.** Route params bound to `input()` with `withComponentInputBinding()` |
| 9 | HTTP | `HttpClient` with a **functional interceptor** (`withInterceptors`) |
| 10 | Forms | **Typed Reactive Forms** with `NonNullableFormBuilder` |
| 11 | Auth | Plain JWT in `localStorage`, sent as `Authorization: Bearer`. No refresh token, no cookies |
| 12 | Styling | **Plain CSS** with CSS custom properties as design tokens. **No component library** (no Angular Material, no Tailwind). Inter via `@fontsource-variable/inter` |
| 13 | Layout target | **Desktop first** (1024px and wider). No mobile layout in this version |
| 14 | Experimental APIs | **Do not use** `resource`, `rxResource`, `httpResource`, signal forms or selectorless components. They are experimental in Angular 20 |
| 15 | Complexity | **Simple beats clever.** Fewer moving parts wins |

---

## 3. Scope

**In scope:** the 8 screens listed in section 5 and everything in `FE-02-details.md`.

**Out of scope (do not build):** forgot or reset password, search, settings, profile editing, dark mode, mobile or tablet layout, internationalisation, server-side rendering, NgRx or any state library, charts, WebSockets or SSE, environment or region switcher, usage or telemetry panels, rate-limit or scoping UI, multiple API keys, an API documentation link, notifications to the user in the header, unit-test coverage targets beyond the listed cases. These are discussed as future scope in the interview guide.

---

## 4. Tech Stack

| Layer | Technology |
| --- | --- |
| Framework | Angular 20 (latest 20.x), TypeScript in strict mode |
| Reactivity | Signals (`signal`, `computed`, `effect`, `toSignal`, `toObservable`, `input`, `output`, `model`) and RxJS |
| Routing | `@angular/router` with lazy `loadComponent` |
| HTTP | `HttpClient` and functional interceptors |
| Forms | Typed Reactive Forms |
| Styling | Plain CSS, design tokens in `styles.css`, component-scoped CSS |
| Font | `@fontsource-variable/inter` |
| Dev server | `ng serve` with `proxy.conf.json` forwarding `/api` to the local backend |
| Tests | The CLI's default test runner (Jasmine and Karma in Angular 20), `HttpTestingController` |
| Build | `ng build` (the esbuild-based application builder) |

---

## 5. Screens and Routes

| Route | Screen | Auth | Layout |
| --- | --- | --- | --- |
| `/login` | Login | guests only | centered card |
| `/register` | Register | guests only | centered card |
| `/welcome` | API key reveal (right after registering) | logged in and has a pending key | centered card |
| `/overview` | Overview | logged in | app shell |
| `/notifications` | Notifications list | logged in | app shell |
| `/notifications/:id` | Notification detail | logged in | app shell |
| `/send-test-email` | Send test email | logged in | app shell |
| `/api-key` | API key | logged in | app shell |

The empty path redirects to `/overview`, and unknown paths redirect to `/overview` (which sends logged-out users to `/login`).

---

## 6. Project Structure

```
FE/                                         (the Angular project root, `portal`)
├── proxy.conf.json                         /api → http://localhost:5080
├── angular.json, package.json, tsconfig*.json
└── src/
    ├── index.html, main.ts, styles.css     styles.css holds the design tokens and global classes
    └── app/
        ├── app.ts, app.html, app.css       root component (router outlet only)
        ├── app.config.ts                   providers: router, http, interceptor
        ├── app.routes.ts                   lazy routes and guards
        ├── core/
        │   ├── auth/
        │   │   ├── auth.service.ts         token and tenant signals, login, register, logout
        │   │   ├── auth.interceptor.ts     adds the Bearer header, handles 401
        │   │   ├── auth.guard.ts           authGuard, guestGuard, pendingKeyGuard
        │   │   └── jwt.util.ts             decode the exp claim
        │   ├── api/
        │   │   ├── dashboard.api.ts        stats, list, detail, test-email
        │   │   └── tenant.api.ts           regenerate key
        │   ├── models/                     TypeScript interfaces matching the API contract
        │   └── clock.service.ts            shared "now" signal for relative times
        ├── layout/
        │   ├── shell/                      sidebar, header, router outlet
        │   └── user-menu/                  avatar menu: API key, Log out
        ├── features/
        │   ├── auth/                       login, register, welcome (api key reveal)
        │   ├── overview/
        │   ├── notifications/              notifications-list, notification-detail
        │   ├── send-test-email/
        │   └── api-key/
        └── shared/
            ├── status-badge/
            ├── stat-card/
            ├── pager/
            ├── time-ago/
            ├── copy-button/
            ├── banner/
            ├── confirm-dialog/
            └── api-key-reveal/             amber banner, key, copy, "I've saved my key"
```

**Naming convention:** components are written the Angular 20 way, without the `.component` suffix (`login.ts`, class `Login`, selector `app-login`, plus `login.html` and `login.css`). Services, guards, interceptors and API classes keep explicit suffixes (`auth.service.ts` and `AuthService`, `auth.guard.ts`, `auth.interceptor.ts`, `dashboard.api.ts` and `DashboardApi`). Use exactly the names in this tree.

---

## 7. Coding Rules (follow in every file)

1. **Standalone components only.** No NgModules. Every component sets `changeDetection: ChangeDetectionStrategy.OnPush`.
2. **`inject()` for dependencies.** Declare them as `private readonly api = inject(DashboardApi);` fields. No constructor parameters for DI.
3. **Signals for state.** Local state is `signal()`. Derived state is `computed()`. Do not recompute in templates with method calls.
4. **Signal-based component API.** Use `input()` and `input.required()`, `output()`, `model()`, `viewChild()`. No `@Input()`, `@Output()` or `@ViewChild()` decorators.
5. **New control flow in templates.** `@if / @else`, `@for (item of items(); track item.id)` with `@empty`, `@switch`. Use `@let` for local template variables when it reads better. Never `*ngIf`, `*ngFor` or `ngSwitch`.
6. **RxJS for streams, signals for state.** Convert with `toSignal()` and `toObservable()` where it removes a manual `subscribe`. When you must subscribe, use `takeUntilDestroyed()`.
7. **Functional guards and interceptors.** `CanActivateFn` and `HttpInterceptorFn`. No class-based ones.
8. **Typed Reactive Forms.** Use `inject(NonNullableFormBuilder)`. Show field errors only after the field is touched or the form was submitted.
9. **TypeScript strict mode.** No `any`. Models mirror the API contract exactly. Status is a union type `'queued' | 'processing' | 'delivered' | 'failed'`, not an enum.
10. **No business logic in templates.** No HTTP calls in components that could live in an API service.
11. **Styling:** use the CSS custom properties from `styles.css`. No inline styles, no hard-coded colors in component CSS.
12. **Accessibility basics:** every input has a `<label>`, buttons are real `<button>` elements, the confirm dialog uses the native `<dialog>` element, and focus is visible.
13. **Do not use experimental APIs** (section 2, decision 14).

---

## 8. Build Milestones

The backend milestones B1 to B4 should be done first, so the frontend can run against the real API.

| Milestone | Build | Done when |
| --- | --- | --- |
| **F1** Scaffold and shell | The project already exists from `ng new`: delete the default welcome template in `app.html`, then build on it. Design tokens and global classes in `styles.css`. Inter font. App shell (sidebar, header, user menu). Routes with lazy loading. Dev proxy | `npm start` shows the shell, navigation switches between placeholder pages, and `/api/health` works through the proxy |
| **F2** Auth | `AuthService`, interceptor, guards, JWT util. Login, Register and Welcome screens with validation and error states | You can register (see the key screen), log out, log in, and a logged-out visit to `/overview` redirects to `/login`. An expired or invalid token returns you to login with the "session expired" message |
| **F3** Overview | `DashboardApi`, stat cards, recent notifications table, polling every 5 seconds, loading, empty and error states | Numbers and rows update on their own within 5 seconds of a status change. Stopping the backend shows the error banner and keeps the last data |
| **F4** Notifications | List with status chips, pager and Refresh. Detail page with the attempts timeline and polling while the status is not final | Filter and paging work. A failed notification shows 3 attempts with errors. A `queued` notification updates to its final status without reloading |
| **F5** Send test email and API key | Send form with success banner. API key page with the regenerate flow and quick start snippet | Sending queues an email and shows the banner. Regenerating shows the new key once, and the old key no longer works against the API |
| **F6** Deploy | Follow the runbook in `FE-02-details.md`, section 10 | The CloudFront URL serves the dashboard, deep links work after a refresh, and the full flow works against the deployed API |

---

## 9. Starter Prompt for Claude Code

> Read `docs/FE-01-plan.md` and `docs/FE-02-details.md`. The repo root is `notification-delivery-system`. The Angular 20 project already exists directly in `FE/` (project name `portal`), so continue in place and do not run `ng new` or touch `BE/`. Replace the default template in `app.html`. Build the frontend exactly as specified, following the milestone order in the plan and the coding rules in section 7. Match the screen specs in the details file, including what is explicitly removed. The API contract is in `BE-02-details.md` section 5. Do not build anything listed as out of scope and do not use experimental Angular APIs. Start with F1 and stop after each milestone so I can verify it.
