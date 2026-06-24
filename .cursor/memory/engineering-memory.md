# Engineering Memory

Durable facts for agents. Skills enforce checks; this file holds project-specific truth.

## Stack

.NET 9 · ASP.NET Core · EF Core · PostgreSQL · FluentValidation · Mapster · Serilog · Elsa Workflows · Vanilla JS test panel

## Solution

```
Maskan.Panel.sln
src/BuildingBlocks/          # Result, errors, observability, shared persistence helpers
src/Services/CoreService/
  Core.API                   # HTTP, auth, DI bootstrap (Program.cs)
  Core.Application           # App services, state managers, DTOs, validators
  Core.Domain                # Entities, enums (Investment, Guarantee, Loan, Fund, Analytics)
  Core.Persistence           # EF Core, migrations, schemas
  Core.Infrastructure        # Repositories, SMS, S3 storage, dashboard jobs
  Core.Workflow              # Elsa definitions + orchestrators
Frontend/                    # Non-prod RTL Persian API test panel
docs/                        # backend + frontend developer guides
.cursor/                     # Agent docs (AGENTS.md, architecture-map, modules-reference, …)
```

## Database schemas

`Identity`, `Investment`, `Guarantee`, `Loan`, `Analytics`, `Fund` — see `Core.Persistence/DbSchemas.cs`.

## Workflow orchestration

| Module | Pattern |
|--------|---------|
| Investment | AppService → **InvestmentWorkflowCoordinator** → CaseStateManager → Elsa |
| Guarantee | AppService → GuaranteeCaseStateManager → Elsa (inline ApplyTransitionAsync) |
| Loan | AppService → LoanCaseStateManager → Elsa (inline ApplyTransitionAsync) |

**Rule:** Never mutate `CurrentStatus` outside `*CaseStateManager`.

Elsa signal: `WorkflowSignals.StatusChanged` (`status-changed`).

## Data access patterns

- App services use `ICoreUnitOfWork` for scoped loads + `SaveChangesAsync`
- `ICoreDbContext` also injected for `ExecuteUpdate`, cross-aggregate queries, child entity adds
- Investment status persistence: `InvestmentCaseWriteExtensions.ApplyStateAsync` + separate history insert
- Repositories in `Core.Infrastructure/Persistence/`
- Reads often `AsNoTracking`; transition loads use `GetScopedForTransitionAsync`

## Active vs legacy application code

**Active:** `Core.Application` namespace, `*CaseAppService`, modern `CaseStateManager`.

**Excluded from compile:** `Services.CoreService.Core.Application.*` (old CaseService, ReviewService, PaymentService, old phase-based state manager). Do not wire back without migration plan.

## Authorization layers

1. Controller `[Authorize(Policy)]`
2. JWT permissions — `Identity/Authorization/Permissions.cs`
3. Module permissions — `CasePermissions`, `GuaranteePermissions`, `LoanPermissions`
4. `DepartmentPermissionEvaluator` + `UserDepartment`
5. Workflow state machine

## External services

S3-compatible storage (documents) · Redis (permission cache) · SMS gateway · Kafka (events)

## Frontend test panel

- Entry: `Frontend/index.html` + `app.js` → `window.TestPanel`
- All API via `TestPanel.apiRequest` + `unwrapEnvelope`
- Multi-session OTP auth in localStorage
- Workflow models: `workflow-model.js`, `guarantee-workflow-model.js`, `loan-workflow-model.js`

## Guarantee domain notes

- Amendments replace standalone renewals (`AmendmentType`, `GuaranteeAmendmentHistoryRecord`)
- `IGuaranteeRenewalAppService` is deprecated stub

## Build

```bash
dotnet build Maskan.Panel.sln
# Warnings are errors — must build clean
```

No automated test projects in repo (as of last update).

## EF migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Services/CoreService/Core.Persistence \
  --startup-project src/Services/CoreService/Core.API
```

## DI registration hotspots

- `Core.API/DependencyInjection/ApplicationServiceCollectionExtensions.cs` — app services, state managers, coordinators
- `Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` — repos, integrations
- `Core.Workflow/DependencyInjection/ServiceCollectionExtensions.cs` — Elsa + orchestrators

## Commit scopes

`investment`, `guarantee`, `loan`, `dashboard`, `auth`, `fund-credit`, `persistence`, `frontend`, `workflow`

## Agent doc index

| File | Purpose |
|------|---------|
| `AGENTS.md` | Main entry |
| `architecture-map.md` | Layers, auth, persistence, Elsa |
| `modules-reference.md` | Per-module files and enums |
| `frontend-guide.md` | Test panel structure |
| `agent-playbook.md` | Task recipes |

## Definition of Done

Build clean · Validators on new DTOs · Mapster on DTO boundaries · Auth on protected endpoints · State via state manager · No API contract drift unless requested · Run applicable `.cursor/skills/*` gates
