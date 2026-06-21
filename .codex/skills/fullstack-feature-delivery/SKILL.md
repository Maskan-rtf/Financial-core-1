---
name: fullstack-feature-delivery
description: Implement production features end-to-end across ASP.NET Core, EF Core, REST APIs, and React or Next.js. Use when a request requires coordinated backend, database, contract, and frontend changes rather than an isolated patch.
---

# Fullstack Feature Delivery

Implement the feature across domain, persistence, API, and UI with one coherent plan. Prefer contract-first changes and keep each layer aligned.

## Workflow

1. Read the feature brief, acceptance criteria, auth rules, and rollout constraints.
2. Locate the existing domain model, API entry points, data access path, frontend route, and tests.
3. Define the contract before editing code:
   - request and response DTOs
   - validation rules
   - auth requirements
   - migration needs
   - UI states for loading, empty, success, and failure
4. Implement backend in this order unless the codebase strongly suggests another:
   - domain or application logic
   - persistence changes
   - DTO mapping
   - API endpoint wiring
   - backend tests
5. Implement frontend against the final contract:
   - typed API access
   - state ownership
   - component composition
   - optimistic updates only when consistency risk is acceptable
   - visible error handling
6. Verify the full path from user action to persisted state and back to UI.

## Backend Rules

- Keep business rules out of controllers.
- Do not leak EF entities through API responses.
- Add validation at the request boundary.
- Prefer explicit mapping over incidental shape matching.
- If the change affects existing consumers, preserve backward compatibility or stage the rollout.

## Frontend Rules

- Keep server data separate from transient UI state.
- Avoid introducing global state unless multiple routes truly need shared ownership.
- Model loading, empty, error, and partial states explicitly.
- Prefer narrow components with clear prop and state boundaries over large page-level blobs.

## Database Rules

- Add migrations only when the data model actually changes.
- Consider nullability, defaults, backfill requirements, and index impact.
- For hot paths, assess query shape before merging.

## Deliverables

Produce:
- coherent code changes across layers
- targeted tests at the appropriate layers
- a short summary of contract changes, migration impact, and rollout risk

## Refuse The Wrong Shortcut

Do not implement the UI against guessed response shapes. Do not patch only one layer when the feature clearly spans several. Call out missing acceptance criteria only when they block safe implementation.
