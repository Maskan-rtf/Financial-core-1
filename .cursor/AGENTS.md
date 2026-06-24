# Financial-Core — Agent Guide

Primary entry point for AI coding agents working on **Maskan Panel / Financial-Core**.

This is a **.NET 9 monorepo** with a vanilla-JS **non-production test panel** (`Frontend/`). The backend is a single service (`CoreService`) covering investment, guarantee, and loan case workflows plus identity, dashboards, fund credit limits, and analytics.

## Read first (by task)

| Task | Read |
|------|------|
| Any backend change | This file → `architecture-map.md` → `modules-reference.md` |
| Case workflow / state transition | `modules-reference.md` § Workflow orchestration + `skills/workflow-orchestration/SKILL.md` |
| New API endpoint | `workflows/feature-development.md` + `skills/validation/SKILL.md` |
| Frontend / test panel | `frontend-guide.md` + `skills/frontend-api/SKILL.md` |
| Auth / permissions | `architecture-map.md` § Authorization + `skills/security/SKILL.md` |
| EF / migrations | `skills/ef/SKILL.md` + `skills/persistence/SKILL.md` |
| Refactor without behavior change | `skills/refactor/SKILL.md` |

**Human docs (Persian/English):** `docs/backend/BACKEND_DEVELOPER_GUIDE.md`, `docs/frontend/*.md`

---

## Solution layout

```
Maskan.Panel.sln
src/
  BuildingBlocks/           # Shared: Result, errors, UoW patterns, observability
  Services/CoreService/
    Core.Domain             # Entities, enums, domain rules (no external deps)
    Core.Application        # App services, state managers, DTOs, validators, mappers
    Core.Contracts          # Public contracts
    Core.Persistence        # EF Core, configurations, migrations
    Core.Infrastructure     # Repository implementations, SMS, storage, identity infra
    Core.Workflow           # Elsa workflow definitions + orchestrators
    Core.API                # Controllers, auth, middleware, DI bootstrap
Frontend/                   # RTL Persian test panel (vanilla JS, no bundler)
docs/                       # Developer guides per domain
.cursor/                    # Agent rules, skills, workflows (this tree)
```

**Business modules:** Investment cases · Guarantee cases (incl. amendments/cancellation) · Loan cases · Kanban inbox · Dashboards/KPI · Fund credit limits · Identity/OTP.

---

## Layer rules (non-negotiable)

1. **Domain** has zero infrastructure dependencies.
2. **Application** depends on Domain + abstractions; no direct HTTP/EF in domain entities.
3. **Controllers** are thin — delegate to `*AppService`, return `Result<T>` envelopes.
4. **State transitions** go through `*CaseStateManager` only — never mutate `CurrentStatus` in controllers or bypass the state machine.
5. **Workflow signal** (`WorkflowSignals.StatusChanged`) fires **after** transition persistence (see module-specific notes in `modules-reference.md`).
6. **Warnings as errors** — solution must build clean (`TreatWarningsAsErrors`).

---

## Workflow orchestration pattern

```
Controller → *CaseAppService → [Coordinator?] → *CaseStateManager → Persist → Elsa signal
```

| Module | Coordinator | State manager | Elsa orchestrator |
|--------|-------------|---------------|-------------------|
| Investment | `InvestmentWorkflowCoordinator` | `CaseStateManager` | `ElsaCaseWorkflowOrchestrator` |
| Guarantee | *(inline in AppService)* | `GuaranteeCaseStateManager` | `ElsaGuaranteeWorkflowOrchestrator` |
| Loan | *(inline in AppService)* | `LoanCaseStateManager` | `ElsaLoanWorkflowOrchestrator` |

When adding investment transitions: extend `WorkflowAction` + `CaseStateManager` transitions, then call via `IInvestmentWorkflowCoordinator` — not direct entity mutation.

---

## API surface (v1)

All routes: `api/v{version}/...` (default `v1`).

| Controller | Route | App service |
|------------|-------|-------------|
| `InvestmentCasesController` | `investmentcases` | `IInvestmentCaseAppService` |
| `GuaranteeCasesController` | `guaranteecases` | `IGuaranteeCaseAppService` |
| `LoanCasesController` | `loancases` | `ILoanCaseAppService` |
| `KanbanController` | `kanban` | `IKanbanAppService` |
| `FundCreditLimitsController` | `fund-credit-limits` | `IFundCreditLimitAppService` |
| `DashboardController` | `dashboard` | Dashboard aggregation services |
| `EmployeeKpiAnalyticsController` | `analytics` | KPI queries |
| `UserController` | `identity/users` | Identity application |
| `CompaniesController` | `identity/companies` | `ICompanyAppService` |

**Deprecated:** `GuaranteeRenewalsController` removed — renewals → guarantee **amendments** (`AmendmentType.Extension`).

---

## Database

- **PostgreSQL** with schemas: `Identity`, `Investment`, `Guarantee`, `Loan`, `Analytics`, `Fund` (`Core.Persistence/DbSchemas.cs`).
- Configurations: `Core.Persistence/Configurations/`.
- Migrations: `Core.Persistence/Migrations/`.
- Investment case writes often use `ExecuteUpdate` (`InvestmentCaseWriteExtensions.ApplyStateAsync`) to avoid legacy xmin concurrency issues.

---

## Authorization (summary)

Four layers — all may apply on a single request:

1. **ASP.NET policies** — `[Authorize(Policy = "...")]` on controllers (`AuthorizationServiceCollectionExtensions.cs`).
2. **JWT permission strings** — `Core.Application/Identity/Authorization/Permissions.cs` + `RolePermissions`.
3. **Module permissions** — `CasePermissions`, `GuaranteePermissions`, `LoanPermissions` checked in `*AuthorizationService`.
4. **Workflow state** — `*CaseStateManager.CanTransition` + business rules.

**Department permissions:** `UserDepartment` enum + `DepartmentPermissionEvaluator` — used by module auth services for internal users.

**Redis:** permission cache must be invalidated after role changes.

---

## DI registration (where to wire new services)

| Concern | File |
|---------|------|
| App services, state managers, coordinators | `Core.API/DependencyInjection/ApplicationServiceCollectionExtensions.cs` |
| Repositories, SMS, storage, dashboard | `Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` |
| EF Core, interceptors | `Core.Persistence/DependencyInjection/ServiceCollectionExtensions.cs` |
| Elsa workflows, orchestrators | `Core.Workflow/DependencyInjection/ServiceCollectionExtensions.cs` |
| Auth policies | `Core.API/DependencyInjection/AuthorizationServiceCollectionExtensions.cs` |
| API entry | `Core.API/Program.cs` |

---

## Build & run

```bash
dotnet build Maskan.Panel.sln
dotnet run --project src/Services/CoreService/Core.API/Core.API.csproj
```

```bash
# EF migration
dotnet ef migrations add MigrationName \
  --project src/Services/CoreService/Core.Persistence \
  --startup-project src/Services/CoreService/Core.API

dotnet ef database update \
  --project src/Services/CoreService/Core.Persistence \
  --startup-project src/Services/CoreService/Core.API
```

**Frontend test panel:**
```powershell
cd Frontend
python -m http.server 5500
# Open http://localhost:5500/index.html — configure API base in تنظیمات
```

**Tests:** No automated test projects in repo yet. Verify with `dotnet build` and manual/API testing via test panel.

---

## Legacy / excluded code (do not resurrect without intent)

Files under `Core.Application/` in namespace `Services.CoreService.Core.Application.*` are **excluded from compile** (`Core.Application.csproj`):

- Old `CaseService`, `ReviewService`, `PaymentService`, `DataEntryService`, etc.
- Old phase-based `State/CaseStateManager.cs`

Active path: `*CaseAppService` + modern `CaseStateManager` (status-based, not legacy `CasePhase` enum in old namespace).

---

## Commit format

```
<type>(<scope>): <description>

Types: feat, fix, chore, docs, refactor
Scopes: investment, guarantee, loan, dashboard, auth, fund-credit, persistence, frontend
```

---

## Skills & workflows

| Type | Path |
|------|------|
| Quality gates | `.cursor/skills/{validation,mapping,persistence,logging,security,ef,repository}/SKILL.md` |
| Workflow changes | `.cursor/skills/workflow-orchestration/SKILL.md` |
| Frontend | `.cursor/skills/frontend-{api,ui,workflow,developer-docs}/SKILL.md` |
| Backend docs | `.cursor/skills/backend-developer-docs/SKILL.md` |
| Feature delivery | `.cursor/workflows/feature-development.md` |
| Bug fix | `.cursor/workflows/bug-fixing.md` |
| Code review | `.cursor/workflows/code-review.md` |
| Durable facts | `.cursor/memory/engineering-memory.md` |

---

## Agent constraints

- **Do not** change API response shapes or frontend contracts unless explicitly requested.
- **Do not** bypass `*CaseStateManager` for workflow transitions.
- **Do not** add database migrations when task says refactor-only.
- **Do not** commit unless user asks.
- **Prefer** minimal diffs; match existing naming and file placement.
- **Use** `ICoreUnitOfWork` / repository abstractions in app services; `ICoreDbContext` is also injected where `ExecuteUpdate` or cross-aggregate queries are needed (existing pattern in `*CaseAppService`).
- **Persian** user-facing API messages live in `ApiMessages`, `CaseSuccessMessages`, module-specific message classes.
