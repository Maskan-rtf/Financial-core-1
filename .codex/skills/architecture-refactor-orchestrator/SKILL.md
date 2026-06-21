---
name: architecture-refactor-orchestrator
description: Refactor multi-layer production systems across API, application, domain, persistence, and frontend boundaries. Use when code is correct enough to run but structurally expensive to maintain, extend, or reason about.
---

# Architecture Refactor Orchestrator

Refactor for clearer ownership and lower coupling without changing behavior unintentionally. Sequence the work so each step remains shippable.

## Workflow

1. Map the current flow:
   - entry points
   - business logic location
   - data access path
   - DTO and mapping boundaries
   - frontend ownership and state flow
2. Identify structural problems:
   - fat controllers or pages
   - duplicate business rules
   - leaked persistence models
   - circular dependencies
   - over-shared UI state
3. Define the target boundaries before moving code.
4. Sequence the refactor into safe steps with passing tests between them.
5. Extract and relocate logic before renaming aggressively.
6. Remove compatibility shims only after callers are migrated.

## Backend Refactor Rules

- Move business rules toward application or domain layers.
- Keep repositories or EF access from becoming ad hoc logic containers.
- Normalize DTO boundaries so transport models stay transport-only.
- Prefer smaller composable services over one oversized orchestrator.

## Frontend Refactor Rules

- Split data loading, state coordination, and presentation concerns.
- Remove duplicated API and mapping logic from components.
- Keep mutation workflows near the feature boundary, not scattered across unrelated hooks.

## Safety Rules

- Preserve public contracts unless the task explicitly includes a contract change.
- Add focused tests around fragile seams before major moves.
- Call out refactors that should be staged behind feature flags or compatibility layers.

## Deliverables

Produce:
- a stepwise refactor plan
- the code changes for the approved slice
- updated tests
- notes on remaining debt and deferred steps
