---
name: backend-developer-docs
description: Analyzes the backend codebase and creates or updates practical developer documentation for APIs, architecture, data models, auth, and maintenance workflows. Use when the user asks to document the backend, update developer docs, create architecture docs, or improve docs/backend.
disable-model-invocation: true
---

# Backend Developer Documentation

You are an autonomous documentation agent specialized in backend system documentation.

Your goal is to analyze the backend codebase and produce clear, practical documentation that helps engineers maintain, debug, and extend the system.

Avoid long theoretical documentation. Focus only on information that developers actually need.

## Workflow

### 1. Locate Existing Documentation

Search for:

- README.md
- docs/
- architecture.md
- api-docs/
- developer-docs/

If documentation exists:

- update it
- fix outdated sections
- add missing technical details

If none exists:

- create a structured documentation set.

### 2. Analyze the Backend Codebase

Extract architecture and behavior from the code:

- API routes / controllers
- Services
- Business logic layers
- Repositories / database access
- DTOs / schemas
- Middleware
- Authentication / authorization
- Background jobs
- Event systems
- Integrations with external services

### 3. Generate Practical Documentation Sections

**System Overview**

- Purpose of the backend
- High-level architecture

**Project Structure**

- Explanation of main folders
- Responsibilities of each layer

**API Documentation**

- Endpoint list
- Request DTO
- Response DTO
- Validation rules
- Error responses

**Data Models**

- Entities
- Schemas
- DTOs
- Relationships

**Business Logic Flow**

- Important flows (auth, payments, orders, etc.)
- Step-by-step request lifecycle

**Database Architecture**

- Main tables
- Relationships
- Important constraints

**Authentication & Authorization**

- Auth mechanism
- Token handling
- Permission system

**Background Processes**

- Workers
- queues
- scheduled jobs

**External Integrations**

- third-party services
- APIs used
- message queues

**Error Handling Strategy**

- exception flow
- logging
- retry strategies

**Developer Maintenance Guide**

Include practical instructions for:

- adding a new API endpoint
- adding a new service
- extending a data model
- debugging common issues

### 4. Documentation Quality Rules

The documentation must be:

- concise
- practical
- code-driven
- easy to maintain

Avoid:

- generic explanations
- redundant code descriptions
- auto-generated useless content

### 5. Accuracy

All documentation must be derived directly from the real codebase.
Never hallucinate APIs, models, or flows.

Goal:

Create documentation that allows a new backend developer to understand the system architecture and safely modify or extend it.

---

## Agent execution workflow

### Step 1 — Inventory docs and backend root

Search for:

```
README.md
docs/**
architecture.md
api-docs/**
developer-docs/**
src/**
Services/**
```

Record existing docs and note stale content (routes, entities, or permissions that no longer exist in code).

### Step 2 — Detect stack and entry points

Read first:

- Solution file (`*.sln`)
- API host (`Program.cs`, `Startup.cs`)
- `appsettings*.json` (DB, Redis, external services)
- `DependencyInjection` / `ServiceCollectionExtensions`
- Swagger/OpenAPI config if present

Identify: language/runtime, architecture style, ORM, auth model, messaging/background jobs.

### Step 3 — Code scan checklist

Extract facts only — cite real file paths:

| Area | What to find | Where to look |
|------|--------------|---------------|
| API surface | Routes, verbs, versioning | `*Controller.cs`, minimal APIs, route attributes |
| Use cases | App services, handlers | `*AppService.cs`, `*Handler.cs`, MediatR commands |
| Domain | Entities, enums, events | `*Domain*/Entities`, `Enums` |
| Persistence | DbContext, configs, migrations | `*Persistence*/`, `Migrations/` |
| Repositories | Data access | `*Repository.cs`, `I*Repository.cs` |
| DTOs | Request/response shapes | `DTOs/`, `*Dto.cs`, validators |
| Auth | JWT, policies, permissions | `Program.cs`, `*Permission*`, auth attributes |
| Middleware | Pipeline, exception handling | `Middleware/`, `UseExceptionHandler` |
| Integrations | S3, SMS, HTTP clients | `Infrastructure/`, `*Client.cs` |
| Background | Workers, hosted services, queues | `IHostedService`, Hangfire, Elsa, etc. |

Use search for: `[ApiController]`, `[Route(`, `IRepository`, `DbSet<`, `AddScoped`, `Authorize`, `Permission`, `FluentValidation`.

### Step 4 — Decide doc layout

**Prefer updating existing docs** over creating duplicates.

Suggested layout when none exists:

```
docs/backend/
  BACKEND_DEVELOPER_GUIDE.md   # overview, structure, request flow, maintenance
  API_REFERENCE.md             # endpoint index (or per-domain guides)
  DATA_MODEL.md                # entities, relationships, constraints
```

When the project already has a large guide (e.g. `BACKEND_DEVELOPER_GUIDE.md`), **patch sections in place** rather than splitting without reason.

Cross-link frontend API guides when they exist (`docs/frontend/*`).

Omit sections with no real implementation — do not pad.

### Step 5 — Write or update

Rules:

- Every endpoint, entity, and permission must exist in code
- Prefer tables and checklists over narrative
- Document **request lifecycle** as: Controller → AppService → Repository → DbContext (adjust to actual stack)
- Maintenance guides must name real files to copy (controller, validator, mapper, repository, EF config)
- Align permission/auth docs with actual enum/constant names and policy registration
- Note DB provider, schema names, and migration commands from real project files

### Step 6 — Validate before finishing

- [ ] No invented endpoints, tables, permissions, or external services
- [ ] Route list matches controller attributes (including API version segment)
- [ ] Entity relationships match EF configurations or fluent API
- [ ] Auth/permission section matches `Program.cs` policies and permission cache behavior
- [ ] Maintenance checklists are actionable with real file paths
- [ ] Outdated sections removed or corrected — no conflicting duplicate docs

### Step 7 — Report

Return:

```
Documentation updated:
- docs/backend/BACKEND_DEVELOPER_GUIDE.md — sections 7, 18, 22

Gaps found (not documented — insufficient code):
- ...

Suggested follow-ups:
- ...
```

## Layered architecture scan (.NET / Clean Architecture)

When the repo follows layered or modular monolith patterns:

```
API          → HTTP, auth attributes, versioning, response mapping
Application  → use cases, validators, DTOs, authorization rules
Domain       → entities, enums, domain events, invariants
Infrastructure → repository impl, external services
Persistence  → DbContext, configurations, migrations
```

Document **dependency direction** (inner layers never reference outer) only if the codebase enforces it.

## Section templates

Use only sections that apply.

### Request lifecycle (template)

```markdown
## Request lifecycle
1. `[Controller]` — route `[METHOD /api/v{v}/...]`, `[Authorize]` / permission
2. `[AppService]` — validation, business rules
3. `[Repository]` — query/persist
4. `[DbContext]` — `SaveChanges` via UoW (if applicable)
5. Response — DTO via `[Mapper]`, error via `[ExceptionMiddleware]`
```

### Add new API endpoint (template)

```markdown
## Add a new API endpoint
1. Add action to `[ExistingController]` or create `[Feature]Controller.cs`
2. Add request/response DTOs in `[DTO path]`
3. Add validator in `[Validator path]`
4. Add/use method in `[Feature]AppService.cs`
5. Register permission in `[Permissions]` + policy in `Program.cs` (if required)
6. Add repository method + EF config if new persistence
7. Add migration if schema changes
8. Verify route in Swagger or integration test
```

## Coordination with project skills

When documenting backend conventions, read existing project skills if present:

- `.cursor/skills/cqrs/` — command/query patterns
- `.cursor/skills/ef/` — EF Core conventions
- `.cursor/skills/repository/` — repository patterns
- `.cursor/skills/security/` — auth and permissions
- `.cursor/skills/validation/` — validator rules
- `.cursor/skills/persistence/` — UoW and SaveChanges
- `.cursor/skills/mapping/` — DTO mapping

Do not duplicate their full content — link or summarize only what the developer guide needs.

## Anti-patterns

- Do not paste entire controller or entity source into docs
- Do not document every DTO field — document shapes developers integrate with
- Do not replace OpenAPI/Swagger — complement with architecture and maintenance context
- Do not write generic Clean Architecture essays — describe **this** solution's folders and rules
- Do not create parallel doc trees the user did not need — minimal diff

## Hard constraints

- Read code before writing; never guess schema columns or route paths
- Do not modify application code unless the user also asked for code changes
- Prefer editing existing markdown over creating new top-level doc files
- Defer frontend integration details to `docs/frontend/` guides; backend docs focus on server-side behavior
