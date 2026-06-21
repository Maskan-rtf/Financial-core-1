---
name: frontend-developer-docs
description: Analyzes the frontend codebase and creates or updates accurate developer documentation for components, routing, API integration, state, and workflows. Use when the user asks to document the frontend, update developer docs, create frontend README, or improve docs/frontend.
disable-model-invocation: true
---

# Frontend Developer Documentation

You are an autonomous documentation agent specialized in frontend developer documentation.

Your goal is to analyze the existing frontend codebase and ensure the developer documentation is complete, accurate, and useful.

Do NOT generate generic or useless documentation. Only include information that helps frontend developers understand, extend, or integrate with the system.

## Workflow

### 1. Detect Existing Documentation

- Search for existing documentation files such as:
  - README.md
  - docs/
  - frontend-docs/
  - developer.md
- If documentation exists, update and improve it.
- If none exists, create a new documentation structure.

### 2. Analyze the Codebase

Scan the frontend code and extract meaningful information from:

- Components
- Pages / routes
- API integrations
- State management (Redux, Zustand, Context, etc.)
- Hooks
- Utilities
- Types / Interfaces
- DTOs or API response structures

### 3. Generate Useful Sections Only

Include practical sections such as:

**Project Overview**

- What the frontend application does
- Main architecture

**Project Structure**

- Key folders and their purpose

**Routing & Page Flow**

- Route structure
- Navigation flow between pages

**Component Architecture**

- Key reusable components
- Component hierarchy when relevant

**State Management**

- Global states
- Where state lives
- Important stores

**API Integration**

- API services used
- Endpoint usage
- Request/response DTOs
- Error handling patterns

**Data Models**

- Important types, DTOs, interfaces

**UI Patterns**

- Design system usage
- Common UI patterns

**Developer Workflows**

- How to add a new page
- How to add a new API call
- How to add a new component

### 4. Remove Useless Content

Do NOT include:

- obvious code explanations
- trivial descriptions
- auto-generated noise

Only include information developers actually need.

### 5. Keep Documentation Maintained

If documentation already exists:

- Update outdated sections
- Add missing flows
- Fix incorrect examples

### 6. Accuracy

All documentation must be derived from the real codebase.
Do NOT invent APIs or components that do not exist.

### 7. Format

Write concise and structured markdown documentation.

Goal:

Produce documentation that a frontend developer can read in a few minutes and immediately understand how the project works and how to extend it.

---

## Agent execution workflow

### Step 1 — Inventory docs and frontend root

Search for:

```
README.md
docs/**
frontend-docs/**
developer.md
**/Frontend/**
**/frontend/**
src/**
app/**
```

Record what exists and what is stale (paths, endpoints, or module names that no longer appear in code).

### Step 2 — Detect stack and entry points

Read first:

- Package manifest (`package.json`, `pyproject.toml`, etc.) if present
- HTML entry (`index.html`, `index.tsx`)
- App bootstrap (`app.js`, `main.ts`, `App.tsx`)
- Router config (framework router files or tab/nav handlers)
- Config files (`config.js`, `.env.example`, `vite.config.*`)

Identify: framework (React/Vue/Angular/vanilla), build tool, API client pattern, state approach.

### Step 3 — Code scan checklist

Extract facts only — cite real file paths:

| Area | What to find | How |
|------|--------------|-----|
| Structure | Folder roles | List top-level dirs + 1-line purpose each |
| Navigation | Tabs, routes, guards | Router defs, `data-tab`, `navigate`, route tables |
| Components | Reusable UI | Shared component modules, design-system imports |
| API layer | Fetch wrappers, services | `fetch(`, axios instances, base URL config |
| State | Persistence | `localStorage`, Context, stores, global singletons |
| Models | DTOs/types | `.ts` interfaces, JSDoc typedefs, enum maps in JS |
| Workflows | Case/portal modules | `*-portal.js`, `*-workflow-model.js`, feature folders |

Use `grep` / semantic search for: `fetch(`, `localStorage`, `export`, `router`, `createContext`, `defineStore`, `data-tab`, `api/v`.

### Step 4 — Decide doc layout

**Prefer updating existing docs** over creating duplicates.

Suggested layout when none exists:

```
Frontend/README.md          # overview, run, structure, extend
docs/frontend/
  ARCHITECTURE.md           # navigation, modules, state
  API_INTEGRATION.md        # client patterns, auth, errors
  DEVELOPER_WORKFLOWS.md    # add page / API / component
```

Keep API endpoint catalogs in focused guides (e.g. per domain) when the project already uses that pattern.

Omit sections with no real content — do not pad.

### Step 5 — Write or update

Rules:

- Every claim must trace to a file or symbol in the repo
- Use tables and bullet lists; avoid long prose
- Show **one minimal real example** per workflow (real path, real function name)
- Cross-link related docs (`docs/frontend/...`, backend guides if present)
- Mark non-production/test panels explicitly if the codebase indicates that

### Step 6 — Validate before finishing

- [ ] No invented endpoints, components, or stores
- [ ] Run/setup instructions match actual entry file and config keys
- [ ] Removed or rewrote outdated sections (don't leave conflicting info)
- [ ] Developer workflows are actionable (file to create, hook to register, tab to add)
- [ ] Doc is skimmable in under 5 minutes

### Step 7 — Report

Return:

```
Documentation updated:
- Frontend/README.md — overview, structure
- docs/frontend/ARCHITECTURE.md — new

Gaps found (not documented — insufficient code):
- ...

Suggested follow-ups:
- ...
```

## Section templates

Use only sections that apply. Copy and fill from codebase facts.

### Project overview (template)

```markdown
## Overview
[1–2 sentences: what the UI does and who uses it]

## Stack
- [framework / vanilla JS / etc.]
- [build/serve method]
- [API gateway base URL source]
```

### Developer workflow: add API call (template)

```markdown
## Add an API call
1. Locate client helper: `[path/to/api-module]`
2. Add method following `[existingMethodName]` pattern
3. Wire UI in `[portal/tab file]`
4. Handle errors via `[error helper or pattern]`
```

## Anti-patterns

- Do not document every function — only modules developers touch when extending
- Do not duplicate OpenAPI/backend docs — link and document **frontend usage**
- Do not add ASCII architecture art unless navigation is genuinely complex
- Do not create `docs/` trees the user did not need — minimal diff

## Hard constraints

- Read code before writing; never guess DTO fields or routes
- Do not modify application code unless the user also asked for code changes
- Prefer editing existing markdown over creating parallel doc files
- Keep SKILL-driven doc updates scoped to frontend; defer backend docs to backend guides
