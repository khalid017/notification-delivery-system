# Notification Platform — Frontend Technical Details (v5)

Full specification for the build plan in `FE-01-plan.md`. Screens follow the Stitch designs, with the corrections listed in each screen's "Removed" line.

---

## 1. Design System

Define these as CSS custom properties in `src/styles.css` under `:root`, and use them everywhere.

### 1.1 Tokens

| Token | Value | Use |
| --- | --- | --- |
| `--color-brand` | `#1E3A8A` | Sidebar background, primary buttons, links, active states |
| `--color-brand-hover` | `#1E40AF` | Primary button hover |
| `--color-bg` | `#F8F9FC` | Page background behind cards |
| `--color-surface` | `#FFFFFF` | Cards, header, inputs |
| `--color-border` | `#E2E8F0` | Card and input borders, table row dividers |
| `--color-text` | `#0F172A` | Main text |
| `--color-muted` | `#64748B` | Secondary text, labels, table headers |
| `--color-danger` | `#B91C1C` | Error text |
| `--color-danger-bg` | `#FEF2F2` | Error box background |
| `--color-danger-border` | `#FECACA` | Error box border, invalid input border |
| `--badge-queued-bg` / `--badge-queued-fg` | `#F1F5F9` / `#475569` | Queued badge |
| `--badge-processing-bg` / `--badge-processing-fg` | `#FFEDD5` / `#9A3412` | Processing badge |
| `--badge-delivered-bg` / `--badge-delivered-fg` | `#DCFCE7` / `#166534` | Delivered badge |
| `--badge-failed-bg` / `--badge-failed-fg` | `#FEE2E2` / `#991B1B` | Failed badge |
| `--banner-success-bg` / `--banner-success-fg` | `#DCFCE7` / `#166534` | Success banner |
| `--banner-warning-bg` / `--banner-warning-fg` | `#FEF3C7` / `#92400E` | Warning banner |
| `--radius-card` | `12px` | Cards, dialog |
| `--radius-control` | `8px` | Inputs, buttons |
| `--shadow-card` | `0 1px 2px rgba(15, 23, 42, 0.06)` | Cards |
| `--sidebar-width` | `240px` | Sidebar |
| `--header-height` | `64px` | Top header |

### 1.2 Typography and spacing

- Font: Inter (variable), imported with `@fontsource-variable/inter`. Base size 14px, line height 1.5.
- Page title (h1): 24px, weight 600. Card title: 16px, weight 600. Table header: 12px, uppercase, letter spacing 0.04em, muted.
- Monospace (API key, errors, snippet): the system monospace stack at 13px.
- Spacing on a 4px grid. Common steps: 4, 8, 12, 16, 24, 32. Page content padding 32px, card padding 24px, gap between cards 16px to 24px.

### 1.3 Global classes (in `styles.css`)

| Class | Look |
| --- | --- |
| `.card` | White surface, 1px border, card radius, card shadow |
| `.btn` | Control radius, 36px height, 14px, weight 500, 16px horizontal padding |
| `.btn-primary` | Brand background, white text, hover `--color-brand-hover`, disabled at 50% opacity |
| `.btn-secondary` | White background, border, dark text, hover light grey |
| `.field`, `.field-label`, `.field-error`, `.field-hint` | Form field layout. Input is 40px high, control radius, border. Focus: 2px brand outline. Invalid: danger border |
| `.link` | Brand color, no underline, underline on hover |
| `.badge` | Pill, 12px, weight 500, 2px 8px padding, with a 6px dot before the text. Modifiers `.badge-queued`, `.badge-processing`, `.badge-delivered`, `.badge-failed` |
| `.table` | Full width, header row on a faint grey background, 48px rows, 1px dividers, hover on clickable rows |
| `.mono` | Monospace |
| `.muted` | Muted color |

Icons: simple inline SVG line icons, 16px or 20px, `currentColor`. Do not add an icon library.

---

## 2. App Shell

Used by `/overview`, `/notifications`, `/notifications/:id`, `/send-test-email` and `/api-key`.

**Sidebar** (240px, fixed, full height, `--color-brand` background, white text)
- Top: the wordmark "Notification Platform" (semibold, 16px) with a small bell icon.
- Then exactly four links with a line icon each: **Overview**, **Notifications**, **Send test email**, **API key**. Use `routerLink` and `routerLinkActive`. The active link has a white 14% opacity background, a 8px radius and white bold text. Inactive links are white at 80% opacity.
- Nothing else in the sidebar.
- `/notifications/:id` keeps "Notifications" highlighted.

**Header** (white, 64px, 1px bottom border, spans the content area)
- Only one thing, on the right: the tenant name (from `AuthService.tenant`) and a circular avatar (32px, brand background, white initials of the tenant name, up to two letters) with a small chevron. It is a single button that opens the **user menu**.
- **User menu:** a small white card below the button with two items, "API key" (links to `/api-key`) and "Log out". It closes on outside click, on Escape, and after choosing an item. "Log out" clears the token and goes to `/login`.
- Nothing else in the header: no search, no environment switcher, no status indicators, no help or docs link.

**Content area:** `--color-bg` background, 32px padding, content max width 1200px. Page title (h1) at the top of each page.

---

## 3. Screens

### 3.1 Login (`/login`)

Centered card, 400px wide, on `--color-bg`. Above the card: the wordmark with the bell icon.
- Card title "Log in". Fields: **Email**, **Password**. Full-width primary button "Log in". Below: "New here? Create an account" (link to `/register`).
- Validation: both required, email format. Show errors after submit or when touched.
- **Wrong credentials (401):** red border on the password input and the message "Incorrect email or password." with a small alert icon under it.
- **Network or server error:** a danger box above the button: "Couldn't reach the server. Try again."
- **Session expired:** when the URL has `?expired=1`, show an amber banner at the top of the card: "Your session expired. Please log in again."
- While submitting: button reads "Logging in…" and is disabled.
- After success, navigate to `returnUrl` from the query string if present and safe (starts with `/`), otherwise `/overview`.
- **Removed from the Stitch draft:** the "Forgot password?" link and the "All core dispatch systems operational" footer.

### 3.2 Register (`/register`)

Same layout as Login.
- Card title "Create your account". Fields: **Business name** (placeholder "Acme Corp"), **Email** (placeholder "name@company.com"), **Password** (hint "Min. 8 chars" on the right of the label, and an eye icon button to show or hide the password). Full-width primary button "Create account". Below: "Already have an account? Log in".
- Validation: name required (max 100), email required and valid, password at least 8 characters.
- Server errors: `409` shows "An account with this email already exists." under the email field. A `400` with `fields` maps each message onto the matching field. Anything else shows the generic danger box.
- On success: store the token, keep the returned API key in `AuthService.pendingApiKey`, and navigate to `/welcome`.
- **Removed from the Stitch draft:** the "Start delivering mission-critical notifications in minutes." tagline and the "256-bit TLS encrypted • SOC 2 Certified" footer. Do not claim certifications.

### 3.3 API key reveal (`/welcome`)

Centered card (no shell), same as the Stitch design.
- Title "Your API key". Amber warning banner: "Copy this key now. It won't be shown again."
- A read-only monospace field with the key and a "Copy" button beside it (becomes "Copied" for 2 seconds).
- A checkbox "I've saved my key" and a primary button "Continue to dashboard", **disabled until the box is checked**.
- On continue: clear `pendingApiKey` and go to `/overview`.
- Guard: if there is no pending key (for example after a page refresh), redirect to `/overview`. The key can always be replaced from the API key page.
- This card is the shared `api-key-reveal` component, also used on the API key page after regenerating.

### 3.4 Overview (`/overview`)

- Title "Overview" and a muted line "Updates every 5 seconds".
- Four stat cards in one row (equal width, 16px gap): **Queued**, **Processing**, **Delivered**, **Failed**. Each card is the label in muted text with a small status-coloured dot before it, and one large number (28px, weight 600), formatted with thousands separators. Nothing else in the card.
- Below, a card titled "Recent notifications" with a "View all" link at the top right (to `/notifications`). The table has columns **Recipient, Subject, Status (badge), Attempts, Created**. Show the 8 newest notifications. "Created" is a relative time ("12s ago") with the exact UTC time in a `title` tooltip. Rows are clickable and go to `/notifications/:id`. No pagination and no footer text.
- **Polling:** load immediately, then every 5 seconds (section 5.3).
- **Loading (first load):** four skeleton stat cards and 5 skeleton table rows.
- **Empty:** the table card shows "No notifications yet" and the hint "Send one from Send test email, or call the API with your key."
- **Error:** if a refresh fails, keep the last data and show a small danger banner above the cards: "Couldn't refresh. Retrying…" It disappears after the next success.
- **Removed from the Stitch draft:** time-range and filter controls, the "Send test email" button in the page header, any chart, delta or sub-metric on the stat cards, and the "Live feed" tag.

### 3.5 Notifications list (`/notifications`)

- Title "Notifications" with a muted line "Every email sent through your account." A secondary "Refresh" button with a refresh icon at the top right.
- A row of **filter chips**: All, Queued, Processing, Delivered, Failed. The selected chip has a brand background and white text, others are white with a border. Choosing a chip resets the page to 1.
- A card with the same table as the overview (Recipient, Subject, Status badge, Attempts, Created). 10 rows per page, newest first. Long subjects are truncated with an ellipsis on a single line. Rows are clickable.
- **Pager** at the bottom right of the card: the text "1–10 of 248" and "Previous" and "Next" buttons. "Previous" is disabled on page 1 and "Next" on the last page.
- **Loading:** skeleton rows. **Empty:** with the "All" chip, "No notifications yet" and the same hint as the overview. With another chip, "No {status} notifications". **Error:** a danger box "Couldn't load notifications." with a "Retry" button.
- No polling here. Data loads when the filter or page changes, and when "Refresh" is pressed.
- **Removed from the Stitch draft:** the "Dispatch" button, the coloured dot before each recipient, and the "Showing recent logs" footer.

### 3.6 Notification detail (`/notifications/:id`)

- A back link "← Notifications".
- **Summary card:** the subject as the title, the recipient in muted text below it, and the status badge (larger) at the top right. Under a thin divider, a three-column grid: **Created** (absolute time, such as `Oct 24, 2024, 14:22:10 UTC`, and the relative time below it), **Last updated** (same format), **Attempts** (`3 of 3`; when the status is `failed` and attempts equal 3, a small red line "Maximum retries reached"). For an email that was delivered, also show **Delivered** with its time.
- Below the summary, a card showing the message body in a muted block (preserve line breaks, plain text only, never render as HTML).
- **Delivery attempts card:** a vertical timeline, newest first. Each entry has a coloured dot (red for failed, green for success), "Attempt N", the time (`HH:mm:ss UTC`), "(Final)" after the last attempt of a failed notification, and the status badge at the right. A failed entry shows the error in a light red box in monospace. When there are no attempts yet (status `queued`), show the muted text "No attempts yet."
- **Polling:** if the status is `queued` or `processing`, reload every 5 seconds and stop as soon as the status is `delivered` or `failed` (section 5.4).
- **Not found:** a card with "Notification not found" and the back link. **Error:** a danger box with "Retry".

### 3.7 Send test email (`/send-test-email`)

- Title "Send test email". A white card (max width 560px) with the form: **To**, **Subject**, **Message** (multi-line, 6 rows), and a primary "Send" button ("Sending…" while submitting).
- Validation like the API: To must be a valid email, Subject required (max 200), Message required (max 10,000).
- **Success:** a dismissible green banner above the card: "Queued. View it in Notifications" with "Notifications" as a link to `/notifications`. Then reset the form.
- **Errors:** `400` field errors map onto fields. A `503` shows a red banner "Couldn't queue the email. Try again." Other errors show the generic danger banner.
- **Removed from the Stitch draft:** the "Test Suite > Dispatcher v2.4" breadcrumb.

### 3.8 API key (`/api-key`)

Single column, content max width 720px.
- Title "API key".
- **Card "Active API key":** the description "Use this key in the X-API-Key header when calling the API." A read-only box showing the masked key: the prefix from the tenant plus bullets, for example `np_9fK2xQ7L••••••••••••`. A muted note with an info icon: "The full key is only shown once. Keep it secret and never commit it to a public repository." A secondary button "Regenerate key". There is **no Copy button and no eye icon** on the masked key, because the full key is not available.
- **Regenerate flow:** clicking "Regenerate key" opens the confirm dialog: title "Regenerate API key?", text "The current key will stop working immediately. Any app using it will need the new key.", buttons "Cancel" and a primary "Regenerate". On confirm, call the API. On success show the **`api-key-reveal`** card (amber banner, key, Copy, "I've saved my key" checkbox) with a "Done" button enabled only after the checkbox is ticked. "Done" hides the card and updates the masked key to the new prefix. On failure show a danger banner.
- **Card "Quick start":** a short description "Send your first email with curl." and a read-only code block with a Copy button:
  ```
  curl -X POST https://<your-domain>/api/notifications \
    -H "X-API-Key: np_your_key" \
    -H "Content-Type: application/json" \
    -d '{"to": "user@example.com", "subject": "Hello", "body": "Your first notification"}'
  ```
  Replace `<your-domain>` with `window.location.origin`. This is the only addition beyond the planned pages. Drop it if you want zero extras.
- **Removed from the Stitch draft:** the "Environment · Production · Live" tag, the "API Docs" button, the "Status: 99.98% Healthy" pill, the "Usage & Telemetry" panel, the "Need multiple keys?" enterprise card, "Rate limit" and "Scoping" text, "Created … days ago", and any mention of WebSocket or Bearer tokens (the API uses the `X-API-Key` header).

---

## 4. Shared Components

All are standalone, `OnPush`, and use signal inputs and outputs.

| Component | API | Notes |
| --- | --- | --- |
| `status-badge` | `status = input.required<NotificationStatus>()` | A `.badge` with the matching modifier and capitalised label |
| `stat-card` | `label = input.required<string>()`, `value = input.required<number>()`, `status = input.required<NotificationStatus>()` | Formats the number with `number` pipe. Shows the coloured dot |
| `pager` | `page = input.required<number>()`, `pageSize = input.required<number>()`, `total = input.required<number>()`, `pageChange = output<number>()` | Computes the "1–10 of 248" text with `computed()` |
| `time-ago` | `value = input.required<string>()` | Renders the relative time and sets the exact UTC time as the `title`. Uses `Clock.now` so it updates every 30 seconds without reloading data |
| `copy-button` | `text = input.required<string>()` | Calls `navigator.clipboard.writeText`. Shows "Copied" for 2 seconds using a signal |
| `banner` | `kind = input<'success' \| 'warning' \| 'danger'>('success')`, `dismissible = input(false)`, `dismissed = output<void>()` | Content projected with `<ng-content>` |
| `confirm-dialog` | `title`, `message`, `confirmLabel` (inputs), `confirmed = output<void>()`, `cancelled = output<void>()`, and an `open()` method | Wraps the native `<dialog>` element and calls `showModal()` through `viewChild.required<ElementRef<HTMLDialogElement>>('dlg')`. Escape and Cancel close it |
| `api-key-reveal` | `apiKey = input.required<string>()`, `actionLabel = input('Done')`, `done = output<void>()` | Amber banner, key and copy button, checkbox and the action button, disabled until the box is checked. Used on `/welcome` and after regenerating |

`Clock` (`core/clock.service.ts`): a root service that exposes `now = toSignal(timer(0, 30_000).pipe(map(() => Date.now())), { initialValue: Date.now() })`.

---

## 5. State, Data and Patterns

The snippets show the intended style (Angular 20, signals, `inject()`, new control flow). Adapt names to the generated project, but keep the patterns.

### 5.1 Models (`core/models`)

```ts
export type NotificationStatus = 'queued' | 'processing' | 'delivered' | 'failed';

export interface Tenant { id: string; name: string; email: string; apiKeyPrefix: string; createdAt: string; }
export interface AuthResponse { token: string; expiresAt: string; tenant: Tenant; }
export interface RegisterResponse extends AuthResponse { apiKey: string; }

export interface NotificationItem {
  id: string; to: string; subject: string; status: NotificationStatus;
  attempts: number; createdAt: string; updatedAt: string;
}
export interface DeliveryAttempt {
  attempt: number; status: 'success' | 'failed'; errorMessage: string | null; attemptedAt: string;
}
export interface NotificationDetail extends NotificationItem {
  body: string; lastError: string | null; deliveredAt: string | null;
  deliveryAttempts: DeliveryAttempt[];
}
export interface Page<T> { items: T[]; page: number; pageSize: number; total: number; }
export interface Stats { queued: number; processing: number; delivered: number; failed: number; }
export interface ApiError { error: string; fields?: Record<string, string[]>; }
```

These mirror `BE-02-details.md` section 5.1 exactly.

### 5.2 Auth

`AuthService` (root service):
- State: `token = signal<string | null>(readFromLocalStorage())`, `tenant = signal<Tenant | null>(null)`, `pendingApiKey = signal<string | null>(null)`. Storage key `np_token`. Reads and writes to `localStorage` are wrapped in try/catch.
- `isLoggedIn()`: true when a token exists and its `exp` claim (decoded by `jwt.util.ts`) is in the future.
- `login()`, `register()` return observables and, with `tap`, store the token, the tenant and (for register) the pending key.
- `loadTenant()` calls `GET /api/auth/me` and fills `tenant` (used after a page refresh, by the shell).
- `logout()` clears the token, tenant and pending key, then navigates to `/login`.
- `expireSession()` does the same and navigates to `/login?expired=1`.

```ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isApi = req.url.startsWith('/api/');
  const isPublic = req.url === '/api/auth/login' || req.url === '/api/auth/register';
  const token = auth.token();

  const outgoing = isApi && !isPublic && token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(outgoing).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && isApi && !isPublic) auth.expireSession();
      return throwError(() => err);
    }),
  );
};

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isLoggedIn()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
```

`guestGuard` redirects a logged-in user from `/login` and `/register` to `/overview`. `pendingKeyGuard` allows `/welcome` only when `pendingApiKey()` is set, otherwise redirects to `/overview`.

### 5.3 Overview polling

```ts
export class Overview {
  private readonly api = inject(DashboardApi);

  readonly stats = signal<Stats | null>(null);
  readonly recent = signal<NotificationItem[] | null>(null);
  readonly refreshFailed = signal(false);

  constructor() {
    timer(0, 5000).pipe(
      switchMap(() =>
        forkJoin({ stats: this.api.stats(), recent: this.api.list(undefined, 1, 8) }).pipe(
          catchError(() => { this.refreshFailed.set(true); return EMPTY; }),
        ),
      ),
      takeUntilDestroyed(),
    ).subscribe(({ stats, recent }) => {
      this.stats.set(stats);
      this.recent.set(recent.items);
      this.refreshFailed.set(false);
    });
  }
}
```

Why it is written this way: `switchMap` cancels a slow request when the next tick arrives, `catchError` sits **inside** the `switchMap` so one failed request does not end the whole polling stream, and `takeUntilDestroyed()` stops the timer when the component is destroyed. The template uses `@if (stats(); as s)` and falls back to skeletons while the first response is pending.

### 5.4 List query and detail polling

List: the filter and page are signals; a `computed` query drives the request.

```ts
export class NotificationsList {
  private readonly api = inject(DashboardApi);

  readonly status = signal<NotificationStatus | undefined>(undefined);   // undefined means "All"
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly failed = signal(false);
  private readonly tick = signal(0);                                     // bump to refresh

  private readonly query = computed(() => ({ status: this.status(), page: this.page(), tick: this.tick() }));

  readonly result = toSignal(
    toObservable(this.query).pipe(
      tap(() => { this.loading.set(true); this.failed.set(false); }),
      switchMap(q => this.api.list(q.status, q.page, 10).pipe(
        catchError(() => { this.failed.set(true); return of(null); }),
      )),
      tap(() => this.loading.set(false)),
    ),
    { initialValue: null },
  );

  setStatus(s: NotificationStatus | undefined) { this.status.set(s); this.page.set(1); }
  refresh() { this.tick.update(n => n + 1); }
}
```

Detail: the route param is a signal input and polling stops when the status is final.

```ts
export class NotificationDetailPage {
  readonly id = input.required<string>();          // bound from :id by withComponentInputBinding()
  private readonly api = inject(DashboardApi);

  readonly notification = signal<NotificationDetail | null>(null);
  readonly notFound = signal(false);

  constructor() {
    toObservable(this.id).pipe(
      switchMap(id => timer(0, 5000).pipe(
        switchMap(() => this.api.detail(id)),
        takeWhile(n => n.status === 'queued' || n.status === 'processing', true),   // true = emit the final value too
      )),
      takeUntilDestroyed(),
    ).subscribe({
      next: n => this.notification.set(n),
      error: (e: HttpErrorResponse) => e.status === 404 && this.notFound.set(true),
    });
  }
}
```

### 5.5 Forms

```ts
export class Login {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly submitting = signal(false);
  readonly invalidCredentials = signal(false);
  // submit(): if invalid → markAllAsTouched(); else call auth.login(...)
}
```

Templates show `.field-error` text with `@if (form.controls.email.touched && form.controls.email.hasError('required')) { ... }`. For server-side `fields` errors, call `control.setErrors({ server: message })` and show the message.

### 5.6 API services

`DashboardApi` (`core/api/dashboard.api.ts`): `stats()`, `list(status, page, pageSize)`, `detail(id)`, `sendTestEmail(body)`. `TenantApi` (`tenant.api.ts`): `regenerateKey()`. Both use `inject(HttpClient)` and relative URLs such as `/api/dashboard/stats`. A `status` of `undefined` is omitted from the query string. They return typed observables and contain no UI logic.

### 5.7 Routes

```ts
export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], title: 'Log in', loadComponent: () => import('./features/auth/login').then(m => m.Login) },
  { path: 'register', canActivate: [guestGuard], title: 'Create account', loadComponent: () => import('./features/auth/register').then(m => m.Register) },
  { path: 'welcome', canActivate: [pendingKeyGuard], title: 'Your API key', loadComponent: () => import('./features/auth/welcome').then(m => m.Welcome) },
  {
    path: '', canActivate: [authGuard],
    loadComponent: () => import('./layout/shell/shell').then(m => m.Shell),
    children: [
      { path: 'overview', title: 'Overview', loadComponent: () => import('./features/overview/overview').then(m => m.Overview) },
      { path: 'notifications', title: 'Notifications', loadComponent: () => import('./features/notifications/notifications-list').then(m => m.NotificationsList) },
      { path: 'notifications/:id', title: 'Notification', loadComponent: () => import('./features/notifications/notification-detail').then(m => m.NotificationDetailPage) },
      { path: 'send-test-email', title: 'Send test email', loadComponent: () => import('./features/send-test-email/send-test-email').then(m => m.SendTestEmail) },
      { path: 'api-key', title: 'API key', loadComponent: () => import('./features/api-key/api-key').then(m => m.ApiKeyPage) },
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
    ],
  },
  { path: '**', redirectTo: 'overview' },
];
```

`app.config.ts` provides `provideRouter(routes, withComponentInputBinding())` and `provideHttpClient(withInterceptors([authInterceptor]))`, in addition to whatever `ng new` generated. The shell calls `loadTenant()` on creation when `tenant()` is empty.

---

## 6. Error and Empty-State Rules

| Situation | Behavior |
| --- | --- |
| `401` on any authenticated call | The interceptor clears the session and redirects to `/login?expired=1` |
| Validation `400` | Map `fields` to form fields. Unmapped message goes in a danger box |
| `404` on detail | "Notification not found" card |
| `409` on register | Email field error |
| `503` on send | Red banner "Couldn't queue the email. Try again." |
| Network failure or `5xx` | Danger box with the generic message. List and detail pages offer "Retry" |
| Empty data | A short title and one hint line, never a blank card |
| First load | Skeleton placeholders (grey rounded blocks with a subtle pulse) |

Show human messages only. Never show raw error JSON or status codes to the user.

---

## 7. Configuration

- **`proxy.conf.json`**
  ```json
  { "/api": { "target": "http://localhost:5080", "secure": false, "changeOrigin": true } }
  ```
  Reference it from `angular.json` under `serve.options.proxyConfig`, so `npm start` (which runs `ng serve`) uses it. The backend's local port must match.
- **No environment files for the API URL.** The app always calls the relative path `/api`, so it behaves identically in development (through the proxy) and in production (through CloudFront).
- **`package.json`:** keep the generated scripts. Add `@fontsource-variable/inter` and import it in `styles.css`.
- **Page title:** set through the `title` property on routes (already shown in 5.7). `index.html` title: "Notification Platform".

---

## 8. Testing

Use the CLI's default test runner. Keep tests focused.

| Area | Cases |
| --- | --- |
| `jwt.util` | Decodes `exp`. Returns "expired" for a past `exp` and for a malformed token |
| `AuthService` | `isLoggedIn()` true for a valid token and false for an expired one. `logout()` clears the token. `register()` stores the pending key |
| `authInterceptor` | Adds the Bearer header to `/api/` calls only, not to login and register. A `401` triggers `expireSession()`. Use `HttpTestingController` |
| `authGuard` | Returns true when logged in, otherwise a `UrlTree` to `/login` with `returnUrl` |
| `status-badge` | Renders the right label and class for each status |
| `pager` | Text is "1–10 of 248" for page 1 of 248, and the buttons disable at the ends |
| `api-key-reveal` | The action button is disabled until the checkbox is ticked |

---

## 9. Accessibility and Browser Support

- Every input has a visible `<label>`. Error text is linked with `aria-describedby`. The page has one `<h1>`.
- Focus outlines are visible. The confirm dialog uses the native `<dialog>` (focus trapping and Escape for free).
- Status is never conveyed by color alone: every badge has a text label.
- Target current evergreen browsers (Chrome, Edge, Firefox, Safari), desktop widths 1024px and up.

---

## 10. Deployment Runbook (S3 and CloudFront)

Done manually in the AWS console, after the backend is deployed (`BE-02-details.md`, section 8). Use the same region and keep the bucket private.

1. **Build:** from `FE/`, run `ng build`. The output is in `dist/portal/browser/` (`portal` is the Angular project name in `angular.json`).
2. **S3 bucket:** create a private bucket (Block Public Access fully on) and upload the contents of the `browser` folder, replacing existing files.
3. **CloudFront function** (Functions → Create, runtime cloudfront-js-2.0), associated with the default behavior as a **viewer request**. It rewrites SPA routes to `index.html`:
   ```js
   function handler(event) {
     var request = event.request;
     if (!request.uri.includes('.')) { request.uri = '/index.html'; }
     return event.request;
   }
   ```
   Use this instead of CloudFront's custom error pages. Error pages apply to the whole distribution and would rewrite the API's own 404 responses into `index.html`.
4. **Distribution:**
   - Origin 1: the S3 bucket, with Origin Access Control. Apply the bucket policy CloudFront offers to copy.
   - Origin 2: the API Gateway host (`xxxx.execute-api.<region>.amazonaws.com`), HTTPS only.
   - Default root object: `index.html`.
   - **Default behavior** → Origin 1, redirect HTTP to HTTPS, cache policy `CachingOptimized`, with the function above.
   - **Behavior `/api/*`** → Origin 2, redirect HTTP to HTTPS, **all HTTP methods allowed**, cache policy **`CachingDisabled`**, origin request policy **`AllViewerExceptHostHeader`**.
5. Use the `*.cloudfront.net` domain. No custom domain.
6. **After every frontend change:** `ng build`, upload the new files to S3, then create a CloudFront invalidation for `/*`. Hashed JS and CSS files get new names each build, and `index.html` is the file that needs the invalidation.

Because the dashboard and the API share the CloudFront domain, no CORS setup is needed.

**Smoke test:** open the CloudFront URL, check the login page loads, refresh on `/notifications` (the SPA rewrite), register, copy the key, send a test email to a verified address, and watch it reach `delivered` in the dashboard.
