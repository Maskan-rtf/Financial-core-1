---
name: auth-flow-hardener
description: Implement, debug, or strengthen authentication and authorization flows across ASP.NET Core APIs and React or Next.js frontends. Use when working on JWTs, cookies, roles, claims, session handling, refresh flows, or route protection.
---

# Auth Flow Hardener

Treat auth as a cross-layer protocol, not a single library setting. Validate identity issuance, transport, policy enforcement, and frontend session behavior together.

## Workflow

1. Map the current flow:
   - login or token issuance
   - cookie or header transport
   - refresh or renewal behavior
   - API authentication middleware
   - authorization policies
   - frontend session and route protection
2. Identify the failure or requirement:
   - login fails
   - claims missing
   - roles not enforced
   - session expires incorrectly
   - SSR and CSR behavior diverge
3. Fix the narrowest broken link first, then validate the full chain.

## Rules

- Keep authorization policy checks on the server even if the UI hides controls.
- Do not trust frontend-managed role state as the source of truth.
- Make cookie and token lifetime behavior explicit.
- Be precise about claim names, issuer, audience, scheme names, and middleware order.
- If multiple apps consume the same identity, call out compatibility risks before changing token shape.

## Frontend Guidance

- Differentiate between unauthenticated, unauthorized, and expired-session states.
- In Next.js, account for server-side and client-side access checks separately when needed.
- Avoid leaking protected data during transient render states.

## Deliverables

Produce:
- corrected auth and authorization flow
- backend and frontend changes
- security notes and risky assumptions
- validation scenarios for login, refresh, protected API access, and route protection
