# API integration (frontend)

How this panel calls the backend. For endpoint catalogs per case type, see the domain guides in this folder.

## Client entry point

All authenticated calls go through `TestPanel.apiRequest` in `app.js`:

```347:464:Frontend/app.js
  async function apiRequest(opts) {
    const method = (opts.method || "GET").toUpperCase();
    const path = opts.path || "";
    const url = makeUrl(path);
    ...
    if (useAuth && session && session.accessToken) {
      headers.Authorization = "Bearer " + session.accessToken;
    }
    ...
    res = await fetch(url, { method, headers, body });
```

| Option | Default | Notes |
|--------|---------|-------|
| `path` | — | Relative to `TESTPANEL_CONFIG.baseUrl` unless absolute URL |
| `method` | `GET` | |
| `body` | — | Object → JSON.stringify when `json !== false` |
| `useAuth` | `true` | Set `false` for OTP send/verify, user create |
| `headers` | `{}` | Merged with `Content-Type` and `Authorization` |

URL builder: `makeUrl(path)` — base has no trailing slash; path should start with `/`.

## Response envelope

Backend often returns `{ success, data, message, validationErrors }`. Two unwrap patterns exist:

**`TestPanel.unwrapEnvelope(body)`** (strict — throws on `success: false`):

```105:128:Frontend/app.js
  function unwrapEnvelope(body) {
    ...
    if (Object.prototype.hasOwnProperty.call(body, "success")) {
      if (body.success === false) { throw new Error(...); }
      return { envelope: body, payload: body.data ?? body.list ?? body };
    }
    return { envelope: null, payload: body };
  }
```

**Dashboard modules** sometimes use a lighter unwrap (`body.data ?? body.Data ?? body`).

Feature code typically:

```javascript
const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/dashboard/me" });
const payload = state.panel.unwrapEnvelope(res.body).payload;
```

`apiRequest` also throws when `success === false` on the parsed JSON body before returning.

## Error handling patterns

| Pattern | Where | Behavior |
|---------|-------|----------|
| `withUiError(fn)` | `app.js` auth handlers | Catches → `#globalError` |
| Module `setXxxError(msg)` | portals, kanban, dashboards | Module-specific alert div |
| `try/catch` in async UI handlers | cases-hub, admin | User-facing Persian message |

HTTP non-OK responses are **not** auto-thrown; check `res.ok` when needed. Envelope errors are thrown.

## Auth flows

| Action | Path | `useAuth` |
|--------|------|-----------|
| Send OTP | `POST /api/v1/identity/users/send-otp` | `false` |
| Verify OTP | `POST /api/v1/identity/users/verify-otp` | `false` |
| Refresh | `POST /api/v1/identity/users/refresh-token` | `false` (+ Bearer access token header) |
| Logout | `POST /api/v1/identity/users/logout` | `true` |
| Profile | `GET /api/v1/identity/users/profile` | `true` |

Login response → `saveSessionFromLogin` → `localStorage` session with `accessToken`, `refreshToken`, `userRoleText`, `userRoleNumber`.

## Case API bases

Built from `TESTPANEL_CONFIG.casesVersion` (default `"1"`):

```javascript
TestPanel.casesBasePath()           // /api/v1/investmentcases
TestPanel.guaranteeCasesBasePath()  // /api/v1/guaranteecases
TestPanel.guaranteeRenewalsBasePath()
TestPanel.loanCasesBasePath()       // /api/v1/loancases
TestPanel.kanbanBasePath()          // /api/v1/kanban
```

Identity and dashboard APIs use hardcoded `/api/v1/...` in feature modules.

## Common request shapes

**Create case (applicant):**

```javascript
await panel.apiRequest({
  method: "POST",
  path: panel.loanCasesBasePath(),
  body: { applicantType: 1 } // or 2 + companyId
});
```

**Workflow action (portal pattern):**

```javascript
await panel.apiRequest({
  method: "POST",
  path: panel.casesBasePath() + "/" + caseId + "/data-entry1/submit",
  body: { comment: null },
});
```

**Search / list:**

```javascript
const q = new URLSearchParams({ skip: "0", take: "50", applicantUserId });
await panel.apiRequest({ method: "GET", path: apiBase() + "?" + q });
```

**Documents (presign → PUT → confirm):**

1. `POST .../documents/presign` with `{ documentType, fileName, mimeType, fileSize }`
2. Raw `fetch(presignUrl, { method: "PUT", body: file })` — no Authorization
3. `POST .../documents/confirm?s3Key=...`

Implemented in `portal.js` and mirrored in legacy `app.js` `wireDocuments`.

## Dashboard endpoints

Used by `home-dashboard.js` / `admin-dashboard.js`:

| Endpoint | Purpose |
|----------|---------|
| `GET /api/v1/dashboard/me` | Current user's dashboard |
| `GET /api/v1/dashboard/admin-overview` | Admin all-roles view |
| `GET /api/v1/dashboard/ceo` | CEO slice |
| `GET /api/v1/dashboard/board` | Board slice |
| `GET /api/v1/dashboard/executive` | Executive slice |
| `GET /api/v1/dashboard/department?departmentKey=` | Department queue |
| `GET /api/v1/dashboard/applicant` | Applicant slice |
| `POST /api/v1/dashboard/refresh` | Invalidate server cache |

## Analytics

`employee-kpi-dashboard.js`:

- `GET /api/v1/analytics/employee-kpis?period=Last30Days`
- `POST /api/v1/analytics/employee-kpis/run-job`

## Fund credit limits

`fund-credit-limits.js`:

```
/api/v{casesVersion}/fund-credit-limits
/api/v{casesVersion}/fund-credit-limits/{id}
```

## Kanban

```
GET /api/v{casesVersion}/kanban/action-required
```

Returns action and watch lists; module field maps to case type for navigation.

## DTO field access

API may return camelCase or PascalCase. Use `UIComponents.pick(obj, "id", "Id")` or local `pick()` copies in modules. Do not assume one casing.

## Inspector / debug

`apiRequest` maintains an in-memory request log (`inspector.log`) and updates `#lastRequest` / `#lastResponse` when those elements exist (legacy debug tab). Most active modules do not depend on this.

## Adding a new API call (checklist)

1. Confirm path in backend or domain API guide.
2. Use `state.panel.apiRequest` (or `TestPanel.apiRequest` from `app.js`).
3. Unwrap with `unwrapEnvelope` or module convention.
4. Map errors to the module's alert element.
5. Refresh local `state` and re-render; dispatch `testpanel:case-changed` if case identity changed.

See [DEVELOPER_WORKFLOWS.md](./DEVELOPER_WORKFLOWS.md) for file-level steps.
