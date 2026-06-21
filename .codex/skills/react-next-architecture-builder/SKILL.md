---
name: react-next-architecture-builder
description: Structure React and Next.js features with clear component boundaries, state ownership, data fetching, and rendering strategy. Use when building or restructuring non-trivial frontend features in production applications.
---

# React Next Architecture Builder

Design the frontend around ownership, data flow, and rendering constraints. Optimize for maintainability and predictable behavior before micro-optimizing abstractions.

## Workflow

1. Identify the feature boundary:
   - route or page ownership
   - server and client responsibilities
   - shared state needs
   - mutation paths
2. Choose the rendering model appropriate to the framework area:
   - server components, client components, or mixed
   - SSR, streaming, or client fetch when relevant
3. Define state ownership:
   - server state
   - local UI state
   - cross-component shared state
4. Split the feature into:
   - data boundary
   - coordinator container
   - presentational components
   - form or mutation primitives
5. Implement explicit loading, empty, error, and stale-data states.

## Rules

- Do not centralize state by default.
- Prefer feature-local hooks over global helpers unless the pattern is truly shared.
- Keep forms, data tables, and mutation flows isolated enough to test independently.
- Avoid components that both fetch broadly and render deeply nested UI trees.

## Cross-Stack Guidance

- Align data fetching and caching with backend consistency and invalidation semantics.
- Prefer typed API clients or adapters over ad hoc response handling in components.

## Deliverables

Produce:
- recommended module and component structure
- state ownership decisions
- data and mutation flow
- refactor or implementation changes
- notes on tradeoffs if the codebase already constrains the ideal structure
