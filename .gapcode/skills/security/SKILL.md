---
name: security
description: Authn/authz and OWASP review. Use with /security.
disable-model-invocation: true
---

# /security

## Purpose

Enforce deny-by-default security for APIs and frontend surfaces.

## When to Use

- `/security` invoked
- New/changed endpoints or auth/permissions
- User-controlled content in React/Next/Vue
- File upload or external HTTP
- Review gate: security

## Responsibilities

1. Auth matrix: endpoint × auth required × policy/permission
2. Service-layer ownership checks (IDOR) — not controller-only
3. Confirm `/validation` on all untrusted input
4. Scan logs/errors for secret leakage
5. EF: parameterized queries only — no SQL concat
6. Frontend: flag unsafe HTML (`dangerouslySetInnerHTML`, `v-html`)
7. Issue severity-rated findings; **Fail** on Critical/High open

## Checklist

- [ ] Protected mutations require explicit authz
- [ ] Multi-tenant data scoped (company/user)
- [ ] No secrets in code, logs, client errors
- [ ] CORS explicit (not `*` with credentials)
- [ ] Rate limiting considered on auth endpoints

## Success Criteria

Auth matrix complete; IDOR prevented; no unmitigated Critical/High findings.

## Failure Conditions

**Fail** if: missing auth on protected endpoint · IDOR via id · credentials in logs · SQL injection vector · unsanitized user HTML · validation bypass to persistence
