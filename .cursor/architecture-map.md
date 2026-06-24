# Architecture Map — Financial-Core

Detailed reference for agents. Complements `AGENTS.md`.

## Request lifecycle

```
HTTP Request
  → Correlation middleware
  → Authentication (JWT/JWE)
  → Authorization (policy + permission handler)
  → Controller (thin)
  → *AppService (business orchestration, auth checks, validation)
  → [WorkflowCoordinator] (investment only)
  → *CaseStateManager (state machine + domain transition)
  → Persistence (UoW / ExecuteUpdate / AddAsync)
  → Elsa workflow signal (status-changed)
  → Result<T> envelope → HTTP response
```

## Project dependency graph

```
Core.API
  → Core.Application, Core.Infrastructure, Core.Persistence, Core.Workflow
Core.Application
  → Core.Domain, BuildingBlocks.*
Core.Infrastructure
  → Core.Application, Core.Persistence
Core.Persistence
  → Core.Domain
Core.Workflow
  → Core.Application (orchestrator interfaces)
Core.Domain
  → (none)
```

## Core.Application structure

```
Abstractions/           # I*AppService, I*Repository, I*StateManager, I*WorkflowCoordinator
Authorization/          # CasePermissions, *AuthorizationService, DepartmentPermissionEvaluator
Common/                 # Write extensions, document requirements, fund credit guards
DTOs/                   # Response shapes
Requests/               # API request bodies
Validators/             # FluentValidation
Mappers/                # Mapster *DtoMapper classes
Services/               # *AppService, *StateManager, InvestmentWorkflowCoordinator
Kanban/                 # *KanbanRules per module
Dashboard/              # Aggregation services
Identity/               # Permission service, OTP-related application code
Notifications/Sms/      # Workflow SMS notifiers
Logging/                # ApplicationLog helpers
```

## Core.Domain structure

```
Entities/
  Investment/           # InvestmentCase + subgraph (documents, payments, worksheet, …)
  Guarantee/            # GuaranteeCase + amendment history, approval form, …
  Loan/                 # LoanCase + installments, payments, …
  Fund/                 # FundCreditLimit
  Analytics/            # DashboardStatsSnapshot
Enums/                  # CaseStatus, GuaranteeCaseStatus, LoanCaseStatus, WorkflowAction, …
Identity/               # UserDepartment, role claim constants
Constants/              # UserRoleClaims, etc.
```

## Persistence patterns

### DbContext

`Core.Persistence/CoreDbContext.cs` — single context, schema-separated tables.

### Interceptors

- Audit timestamps
- Soft-delete global filters
- `InvestmentCaseUpdateSuppressorInterceptor` — reduces accidental parent updates during child writes

### Investment case state persistence

Status changes use `InvestmentCaseWriteExtensions.ApplyStateAsync` (ExecuteUpdate) + separate insert of workflow history/comments. See `InvestmentWorkflowCoordinator.PersistCaseTransitionAsync`.

### Repositories (`Core.Infrastructure/Persistence/`)

| Repository | Notes |
|------------|-------|
| `InvestmentCaseRepository` | `GetScopedForTransitionAsync`, list projections |
| `GuaranteeCaseRepository` | Includes amendment/cancellation queries |
| `LoanCaseRepository` | Installment/payment scoped loads |
| `GuaranteeRenewalCaseRepository` | Legacy table; renewal flow deprecated |

## Workflow (Elsa)

### Definitions (`Core.Workflow/Workflows/`)

| Workflow | Definition ID constant | Signal |
|----------|------------------------|--------|
| `InvestmentCaseWorkflow` | `InvestmentCaseWorkflow.DefinitionId` | `status-changed` (multi wait nodes) |
| `GuaranteeCaseWorkflow` | `GuaranteeCaseWorkflow.DefinitionId` | `status-changed` |
| `LoanCaseWorkflow` | `LoanCaseWorkflow.DefinitionId` | `status-changed` |

### Orchestrators (`Core.Workflow/Orchestration/`)

- `ElsaCaseWorkflowOrchestrator` — start + signal investment cases
- `ElsaGuaranteeWorkflowOrchestrator` — bookmark resume with reset fallback
- `ElsaLoanWorkflowOrchestrator` — same pattern as guarantee

Signal constant: `WorkflowSignals.StatusChanged` / `CoreWorkflowSignals.StatusChanged`.

### Investment coordinator (`InvestmentWorkflowCoordinator`)

Extracted orchestration:

1. `GetScopedForTransitionAsync`
2. `ICaseStateManager.TransitionAsync`
3. Optional internal comment (permission-gated)
4. Persist if history count increased
5. SMS notification on real transition
6. Elsa signal (always attempted after successful transition call — preserves legacy investment behavior)

## Authorization deep dive

### Policy registration

`Core.API/DependencyInjection/AuthorizationServiceCollectionExtensions.cs`

Examples: `AdminOnly`, `InternalOnly`, `InvestmentCases.Submit`, `GuaranteeCases.CeoApprove`, `LoanCases.CeoApprove`, `Dashboard.Ceo`, `Analytics.EmployeeKpi`.

### Permission handler

`Core.API/Authorization/PermissionAuthorizationHandler.cs`

Checks role → permissions map, then identity `PermissionService` fallback.

### Module-level authorization

| Service | File | Scoping |
|---------|------|---------|
| `CaseAuthorizationService` | `Authorization/CaseAuthorizationService.cs` | Applicant owns case OR internal + permission |
| `GuaranteeAuthorizationService` | `Authorization/GuaranteeAuthorizationService.cs` | + department subsets |
| `LoanAuthorizationService` | `Authorization/LoanAuthorizationService.cs` | + department subsets |

`DepartmentPermissionEvaluator` resolves permissions from role and/or `UserDepartment` mapping.

### Permission string locations

| Layer | Path |
|-------|------|
| Identity JWT permissions | `Identity/Authorization/Permissions.cs` |
| Investment runtime | `Authorization/CasePermissions.cs` |
| Guarantee runtime | `Authorization/GuaranteePermissions.cs` |
| Loan runtime | `Authorization/LoanPermissions.cs` |

## Result envelope

`BuildingBlocks.Application.Results.Result` / `Result<T>`:

- Controllers use `ApiControllerBase.Respond(result, successMessage, statusCode)`
- Errors: `Error.Validation`, `Error.NotFound`, `Error.Forbidden`, `Error.Conflict`

## External integrations

| Integration | Location |
|-------------|----------|
| S3-compatible storage (Liara) | `Core.Infrastructure` document storage |
| Redis | Permission cache, sessions |
| SMS gateway | `Notifications/Sms` |
| Kafka | Event publishing (BuildingBlocks) |
| Serilog | Structured logging, correlation IDs |

## Kanban

`KanbanAppService` aggregates cards from all modules.

Rules per module:

- `Kanban/CaseKanbanRules.cs` (investment)
- `Kanban/GuaranteeKanbanRules.cs` (includes amendment states)
- `Kanban/LoanKanbanRules.cs`

Each maps `(status, role) → action required | watching`.

## Dashboard & analytics

- Pre-aggregated snapshots in `Analytics` schema
- `DashboardAggregationService` + module aggregators
- Background refresh via hosted services in Infrastructure
- API: `DashboardController`, `EmployeeKpiAnalyticsController`

## Guarantee amendments & cancellation (recent)

- `AmendmentType`: Extension, Reduction, Cancellation
- Statuses: `AmendmentDraft` … `AmendmentRejected` on `GuaranteeCaseStatus`
- Entity: `GuaranteeAmendmentHistoryRecord`
- Helpers: `GuaranteeAmendmentCompleteness`, `GuaranteeCancellationSource`, `GuaranteeAmendmentMessages`
- API routes on `GuaranteeCasesController`: `/amendment/*`, `/amendment/cancellation/*`
- `IGuaranteeRenewalAppService` — stub returning deprecated conflict message

## Adding a new case module (checklist)

1. Domain entity + enums + `WorkflowAction` equivalent
2. `*CaseStateManager` with transition dictionary
3. `*CaseAppService` with `ApplyTransitionAsync` pattern
4. `I*CaseRepository` + implementation
5. EF configuration + migration
6. `*CasesController`
7. Elsa `*CaseWorkflow` + `Elsa*WorkflowOrchestrator`
8. `*KanbanRules`
9. Permissions + policies + authorization service
10. Frontend: `*-workflow-model.js`, `*-portal.js`, `cases-hub.js` module tab
