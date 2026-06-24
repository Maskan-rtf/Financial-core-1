---
name: frontend-api
description: Frontend API integration via TestPanel.apiRequest, envelope unwrapping, auth, and error handling. Use with /frontend-api or when adding/changing API calls in Frontend/.
disable-model-invocation: true
---

# /frontend-api

## Purpose

Keep all backend communication consistent through `TestPanel.apiRequest` with correct auth, envelope handling, and error propagation.

## When to Use

- `/frontend-api` invoked
- New or changed API call in `Frontend/js/`
- New endpoint integration from frontend
- Review gate: frontend data layer

## Entry point

All authenticated calls go through `TestPanel.apiRequest` in `Frontend/app.js`:

```javascript
const res = await state.panel.apiRequest({
  method: "GET",
  path: "/api/v1/dashboard/me"
});
const payload = state.panel.unwrapEnvelope(res.body).payload;
```

| Option | Default | Notes |
|--------|---------|-------|
| `path` | — | Relative to `TESTPANEL_CONFIG.baseUrl` |
| `method` | `GET` | |
| `body` | — | Object → JSON.stringify when `json !== false` |
| `useAuth` | `true` | Set `false` for OTP send/verify, user create |
| `headers` | `{}` | Merged with `Content-Type` and `Authorization` |

## Responsibilities

1. Use `TestPanel.apiRequest` — never raw `fetch` for authenticated endpoints
2. Unwrap with `TestPanel.unwrapEnvelope` (strict) or documented dashboard pattern
3. Handle `{ success, data, message, validationErrors }` envelope
4. Set `useAuth: false` only for public endpoints (OTP, registration)
5. Pass `X-Correlation-Id` header when tracing is needed
6. Read `docs/frontend/API_INTEGRATION.md` for domain-specific endpoint guides
7. Config from `TESTPANEL_CONFIG` in `Frontend/config.js`

## Checklist

- [ ] No raw `fetch` bypassing `apiRequest` for auth endpoints
- [ ] Envelope unwrapped before using payload
- [ ] `success: false` surfaces user-visible error (not silent fail)
- [ ] `validationErrors` mapped to form fields when applicable
- [ ] Base URL from config, not hardcoded
- [ ] Correct API version path (`/api/v1/...`)

## Success Criteria

API calls follow established patterns; errors propagate to UI; auth tokens attached correctly.

## Failure Conditions

**Fail** if: raw fetch with manual auth · unwrapped envelope assumed flat · hardcoded base URL · silent error swallow · missing auth on protected endpoint

## Reference docs

- `docs/frontend/API_INTEGRATION.md`
- `docs/frontend/INVESTMENT_CASE_API_GUIDE.md`
- `docs/frontend/DEVELOPER_WORKFLOWS.md`
