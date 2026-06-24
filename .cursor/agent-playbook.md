# Agent Playbook — Common Tasks

Step-by-step pointers for frequent agent tasks. Always read `AGENTS.md` first.

---

## Add investment workflow transition

1. `Core.Domain/Enums/WorkflowAction.cs` — new action if needed
2. `Core.Domain/Enums/CaseStatus.cs` — new status if needed (+ migration)
3. `Core.Application/Services/CaseStateManager.cs` — transition tuple + `ValidateBusinessRules`
4. `Core.Application/Services/InvestmentCaseAppService.cs` — public method → `ApplyTransitionAsync`
5. Transitions auto-route through `InvestmentWorkflowCoordinator` — **do not** call `CaseStateManager` from AppService directly
6. `InvestmentCasesController.cs` — endpoint + policy
7. `Kanban/CaseKanbanRules.cs` — inbox ownership
8. `Frontend/js/workflow-model.js` + `portal.js`
9. `dotnet build Maskan.Panel.sln`

---

## Add guarantee workflow transition

1. `GuaranteeWorkflowAction` + `GuaranteeCaseStatus` enums
2. `GuaranteeCaseStateManager.cs` — transitions + business rules
3. `GuaranteeCaseAppService.cs` — `ApplyTransitionAsync` wrapper method
4. `GuaranteeCasesController.cs`
5. `GuaranteeKanbanRules.cs`
6. `guarantee-workflow-model.js` + `guarantee-portal.js`

Signal Elsa **inside** `ApplyTransitionAsync` only when workflow history increases (match existing guarantee pattern).

---

## Add loan workflow transition

Same as guarantee, using `LoanCaseStateManager` / `LoanCaseAppService` / `LoanCasesController`.

---

## Add new API endpoint (non-workflow)

1. Request DTO in `Core.Application/Requests/`
2. Validator in `Core.Application/Validators/`
3. Response DTO in `Core.Application/DTOs/`
4. Mapper in `Core.Application/Mappers/`
5. Method on `I*AppService` + implementation
6. Controller action — thin, `[Authorize]`, `CancellationToken`
7. Run quality gates per `workflows/feature-development.md`

---

## Add EF entity

1. Entity in `Core.Domain/Entities/{Module}/`
2. `IEntityTypeConfiguration<T>` in `Core.Persistence/Configurations/`
3. `DbSet<T>` in `CoreDbContext`
4. Migration (name clearly scoped: `AddGuaranteeX`, not generic)
5. `I*Repository` in Application + implementation in Infrastructure
6. Register in `Core.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

---

## Add permission

1. String constant in module `*Permissions.cs` or `Permissions.cs`
2. Map to role in `RolePermissions` and/or `DepartmentPermissionMappings`
3. Check in `*AuthorizationService` or AppService
4. Register policy in `AuthorizationServiceCollectionExtensions.cs` if controller-level
5. Invalidate Redis permission cache on role change (document in PR)
6. Frontend: gate UI by role from session (workflow models)

---

## Refactor workflow orchestration (safe)

1. Read `skills/refactor/SKILL.md` + `skills/workflow-orchestration/SKILL.md`
2. List invariants: API contracts, status outcomes, signal timing, DB writes
3. Extract to coordinator only if pattern matches investment module
4. Never bypass `*CaseStateManager`
5. `dotnet build` — no warnings

---

## Debug failed transition

Check in order:

1. `*CaseStateManager.CanTransition` — role + status + action tuple exists?
2. `ValidateBusinessRules` — missing documents/worksheet/payments?
3. Authorization — `*AuthorizationService` blocking?
4. Correlation id dedup — same request retried?
5. EF concurrency — investment uses `ApplyStateAsync`; check interceptors
6. Elsa signal failure is logged as warning — domain state may still be saved

---

## Debug frontend API error

1. Browser network tab — status code + envelope
2. `unwrapEnvelope` — `success: false` message / `validationErrors`
3. Active session role matches required workflow action
4. `casesVersion` in config matches API
5. CORS / presigned upload issues for S3 documents

---

## Files to never edit without explicit request

- `Core.Persistence/Migrations/*` (except adding new migration)
- `Frontend/config.js` production URLs
- Legacy excluded `Services.CoreService.*` implementations
- Unrelated modules when scoped to one module

---

## Quality gate commands (skills)

Invoke skill workflows documented in `.cursor/skills/*/SKILL.md`:

| Gate | Skill |
|------|-------|
| Validators | `validation` |
| Mapster | `mapping` |
| DB writes | `persistence` |
| Serilog | `logging` |
| Auth matrix | `security` |
| Indexes/migrations | `ef` |
| Repository pattern | `repository` |
| Frontend API | `frontend-api` |
| Frontend UI states | `frontend-ui` |

Definition of Done: `.cursor/memory/engineering-memory.md`
