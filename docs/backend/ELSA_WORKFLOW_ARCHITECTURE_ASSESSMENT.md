# Elsa Workflow Architecture Assessment

**Document type:** Read-only architectural review  
**Scope:** Financial-Core (`Maskan.Panel.sln`) — Investment, Guarantee, Loan modules  
**Elsa version:** 3.6.1 (`Core.Workflow`)  
**Date:** June 2026  
**Audience:** Senior engineers, architects, technical leadership

---

# 1. Executive Summary

Financial-Core implements **three parallel, application-owned state machines** (Investment, Guarantee, Loan). Each case has a `CurrentStatus`, derived `CurrentPhase`, and append-only `*CaseWorkflowHistory`. All meaningful workflow decisions — who may act, what action is valid, business preconditions, persistence, SMS, and Kanban inbox — are handled by **our Application and Domain layers**.

**Elsa Workflows is integrated as a passive companion.** On case creation, an Elsa instance is started with `InstanceId = CaseId`. After the application commits a status change, it sends a single signal (`status-changed`) to resume a custom bookmark activity (`WaitForCaseSignalActivity`). Elsa advances an internal flowchart position. If Elsa fails to resume, the API still succeeds; orchestrators log warnings and attempt instance reset.

Elsa does **not** decide transitions, enforce roles, validate documents, or write case status. PostgreSQL domain tables are the **source of truth**. Elsa tables (non-dev) store instance/bookmark state only.

The investment module has a richer Elsa flowchart (16 wait nodes, revision back-edges). Guarantee and loan use a minimal graph (Start → single wait loop → End). In all modules, the Elsa graph is **informational** rather than driving business logic.

**Migration to an Elsa-first architecture (Elsa as transition authority) is technically possible but would be a large, high-risk rewrite** touching state managers, entities, APIs, Kanban, SMS, frontend models, rollback, and fund-credit guards. **Recommendation: retain the current hybrid pattern (Option C)** — application-owned state machine with optional Elsa orchestration — and only expand Elsa where it adds clear value (waiting, timers, visual process documentation), not as the status authority.

---

# 2. Current Workflow Responsibilities

## 2.1 What the application controls

| Responsibility | Owner | Key files |
|----------------|-------|-----------|
| Transition rules `(Status, Action, Role) → NextStatus` | `*CaseStateManager` | `CaseStateManager.cs`, `GuaranteeCaseStateManager.cs`, `LoanCaseStateManager.cs` |
| Business preconditions (documents, worksheets, payments) | `ValidateBusinessRules` in state managers + AppService guards | Same + `GuaranteeFundCreditGuard.cs`, `*Completeness` / `*DocumentRequirements` in `Core.Application/Common/` |
| Status mutation on entity | Domain aggregates | `InvestmentCase.cs`, `GuaranteeCase.cs`, `LoanCase.cs` — `TransitionTo`, `RequestRevision`, `RollbackTo` |
| Workflow history audit trail | Domain + Persistence | `*CaseWorkflowHistory` entities; configs in `Core.Persistence/Configurations/` |
| Authorization (API policies + module permissions) | API + `*AuthorizationService` | `AuthorizationServiceCollectionExtensions.cs`, `CasePermissions.cs`, etc. |
| Role resolution for transitions | App services / coordinator | `ResolveActorRole()` in `*CaseAppService`, `InvestmentWorkflowCoordinator.cs` |
| Persistence of status + history | App services | `InvestmentWorkflowCoordinator`, `GuaranteeCaseAppService.ApplyTransitionAsync`, `LoanCaseAppService.ApplyTransitionAsync` |
| SMS notifications | App layer → Infrastructure | `IWorkflowSmsNotifier`, `WorkflowSmsNotifier.cs`, `WorkflowSmsCatalog.cs` |
| Kanban ownership & allowed actions | Application | `CaseKanbanRules.cs`, `GuaranteeKanbanRules.cs`, `LoanKanbanRules.cs`, `KanbanAppService.cs` |
| Admin stage rollback | App service (bypasses state manager) | `CaseStageRollbackAppService.cs`, `CaseStageRollbackEvaluator.cs` |
| API surface (semantic routes) | Controllers | `InvestmentCasesController.cs`, `GuaranteeCasesController.cs`, `LoanCasesController.cs` |
| Frontend action visibility | Test panel JS | `workflow-model.js`, `loan-workflow-model.js`, `guarantee-workflow-model.js`, `portal.js`, etc. |

## 2.2 What Elsa controls

| Responsibility | Owner | Key files |
|----------------|-------|-----------|
| Workflow instance lifecycle (start, delete, restart) | Elsa orchestrators | `ElsaCaseWorkflowOrchestrator.cs`, `ElsaGuaranteeWorkflowOrchestrator.cs`, `ElsaLoanWorkflowOrchestrator.cs` |
| Bookmark-based wait/resume | Custom activity | `WaitForCaseSignalActivity.cs`, `CaseSignalStimulus.cs` |
| Internal flowchart position | Workflow definitions | `InvestmentCaseWorkflow.cs`, `GuaranteeCaseWorkflow.cs`, `LoanCaseWorkflow.cs` |
| Instance/bookmark persistence (non-dev) | Elsa EF Core | `ServiceCollectionExtensions.cs` → `UseWorkflowManagement` / `UseWorkflowRuntime` + PostgreSQL |

## 2.3 Source of truth

| Data | Authority | Storage |
|------|-----------|---------|
| `CurrentStatus`, `CurrentPhase` | **Application / Domain** | `Investment.*`, `Guarantee.*`, `Loan.*` schemas |
| `*CaseWorkflowHistory` | **Application / Domain** | Same |
| `WorkflowInstanceId` on case | **Application** (written at create) | Case entity column |
| Elsa instance state, bookmarks | **Elsa** (secondary) | Elsa EF tables (production only) |

Evidence: orchestrators explicitly log *"domain state was still updated"* when Elsa resume fails (`ElsaCaseWorkflowOrchestrator.cs`). Transition paths wrap Elsa signals in `try/catch` and return success after domain persist (`InvestmentWorkflowCoordinator.cs`, `LoanCaseAppService.cs`).

## 2.4 How transitions happen

```
HTTP POST (semantic route)
  → *CasesController
  → *CaseAppService method
  → [InvestmentWorkflowCoordinator.ApplyTransitionAsync]   (investment only)
  → *CaseStateManager.TransitionAsync
       1. Idempotency check (correlationId in history)
       2. CanTransition(status, action, role)
       3. ValidateBusinessRules
       4. entity.RequestRevision(...) OR entity.TransitionTo(...)
  → Persist status + history (+ comments)
  → SMS (module-specific timing)
  → Elsa orchestrator.Signal*(WorkflowSignals.StatusChanged)
```

**Investment** signals Elsa after every successful coordinator call. **Loan/Guarantee** signal only when `WorkflowHistory.Count` increased. **Guarantee** signals via background `Task.Run` (`GuaranteeWorkflowBackgroundSignaler.cs`).

## 2.5 Workflow instance management

| Event | Behavior |
|-------|----------|
| Case create | `StartAsync` / `StartGuaranteeCaseAsync` / `StartLoanCaseAsync` → dispatch definition with `Input["CaseId"]`, `InstanceId = caseId.ToString("D")` |
| Status change | Resume bookmark matching `CaseSignalStimulus { CaseId, Signal }` |
| Bookmark mismatch | Delete bookmarks + instance → restart workflow → poll up to 40×150ms → fallback resume any wait bookmark |
| Dev environment | In-memory Elsa (no PostgreSQL persistence) |
| Production | Same Postgres connection as app for Elsa management + runtime |

Registration: `Core.Workflow/DependencyInjection/ServiceCollectionExtensions.cs`, wired from `Core.API/Program.cs`.

---

# 3. Current Coupling Analysis

| Coupling surface | Description | Files | Rating |
|------------------|-------------|-------|--------|
| **State managers ↔ Domain entities** | Only path for normal transitions; entities expose `TransitionTo` / `RequestRevision` | `*CaseStateManager.cs`, `*Case.cs` | **High** |
| **State managers ↔ Business validators** | Document/completeness checks invoked inside `ValidateBusinessRules` | `CaseStateManager.cs` + `Core.Application/Common/*` | **High** |
| **App services ↔ State managers** | Every workflow endpoint funnels to `TransitionAsync` | `*CaseAppService.cs`, `InvestmentWorkflowCoordinator.cs` | **High** |
| **App services ↔ Elsa orchestrators** | Explicit post-persist signal calls | Orchestrators + callers listed in §2.4 | **Medium** |
| **Kanban ↔ CurrentStatus** | Status → owner role, action/watch sets; uses `GetAllowedActions` from state manager | `*KanbanRules.cs`, `KanbanAppService.cs` | **High** |
| **SMS ↔ toStatus** | Catalog keyed by module + destination status int | `WorkflowSmsCatalog.cs`, `WorkflowSmsNotifier.cs` | **High** |
| **Controllers ↔ WorkflowAction enums** | Semantic routes map 1:1 to actions | `*CasesController.cs`, `WorkflowAction.cs`, etc. | **High** |
| **Frontend ↔ Status enums** | `STATUS_BY_KEY`, `STEPS`, status switches in portals | `Frontend/js/*-workflow-model.js`, `portal.js` | **High** |
| **Frontend ↔ Allowed actions** | Kanban only uses server `GetAllowedActions`; portals use heuristics | `kanban.js` vs `portal.js` | **Medium** (split authority) |
| **Rollback ↔ Domain (bypass SM)** | `RollbackTo` on entity, not via transition table | `CaseStageRollbackAppService.cs` | **High** |
| **Fund credit ↔ Guarantee transitions** | Pre-transition callbacks in AppService | `GuaranteeFundCreditGuard.cs`, `GuaranteeCaseAppService.cs` | **Medium** |
| **Elsa workflow graphs ↔ Process shape** | Investment graph mirrors intended phases; not synchronized with status enum programmatically | `InvestmentCaseWorkflow.cs` | **Medium** (conceptual drift risk) |
| **Domain events ↔ Workflow** | `CasePhaseChangedDomainEvent`, `RevisionRequestedDomainEvent` raised on investment only; **no handlers** | `InvestmentCase.cs`, `Core.Domain/Events/` | **Low** (unused) |
| **Legacy CaseService path** | Old `ReviewService` / `CaseService` still reference deprecated state manager | `Core.Application/State/CaseStateManager.cs` (excluded compile in places) | **Low** (legacy) |
| **CQRS / MediatR** | MediatR registered for domain events only; **no workflow command handlers** | `MediatRDomainEventDispatcher.cs` | **Low** |

**Overall coupling:** Application workflow logic is **highly cohesive and centrally coupled** around `CurrentStatus`, `*CaseStateManager` transition tables, and status-derived side effects. Elsa coupling is **medium and one-directional** (app → Elsa, best-effort).

---

# 4. Elsa Dependency Analysis

## Classification: **Optional + Passive** (not Critical, not Authoritative)

| Criterion | Assessment | Evidence |
|-----------|------------|----------|
| **Critical?** | **No** | Main transition paths catch Elsa failures and still return success after domain persist. API does not read status from Elsa. |
| **Optional?** | **Yes** | System can commit transitions without Elsa resume succeeding. No feature reads Elsa instance state for business decisions. |
| **Passive?** | **Yes** | Elsa waits on bookmarks and advances when signaled. It never initiates transitions or rejects actions. |
| **Authoritative?** | **No** | `CurrentStatus` on domain entity is always written by state manager / entity methods first. |

**Start is semi-critical:** If `StartAsync` throws during case creation (before `SaveChanges`), case creation may fail (`InvestmentCaseAppService`, `GuaranteeCaseAppService`, `LoanCaseAppService`). After case is saved, Elsa is non-blocking.

**Production dependency:** Elsa PostgreSQL tables are required for instance continuity across restarts in non-dev environments, but domain workflow remains functional if Elsa is degraded (with drift between `WorkflowInstanceId` and actual bookmark state — mitigated by reset logic).

**Features loaded but unused:** Elsa Studio, HTTP API, timers, human tasks, conditional branching on payload, multiple signal types (`WorkflowSignals` defines 7 constants; only `StatusChanged` is bound in graphs).

---

# 5. Migration Feasibility

## Overall rating: **Difficult**

A full Elsa-first migration (Elsa decides next status, drives branching, replaces state managers) is **not a configuration change** — it is a **cross-cutting platform rewrite**.

### Why not Easy

- ~76+ base transition tuples across three modules (37 + 39 + 20), expanded by `WorkflowRoleExpander` mirroring.
- ~63 status enum values combined (`CaseStatus` 22, `GuaranteeCaseStatus` 23, `LoanCaseStatus` 18).
- Business rules embed document checks, amendment routing (`ref nextStatus` in guarantee), fund credit, payment completion.
- Kanban, SMS, frontend, and API contracts all assume application-owned `CurrentStatus`.

### Why not Moderate

- No CQRS command layer to swap — transitions are embedded in fat AppServices.
- Rollback bypasses state machine today; would need Elsa compensation or parallel rollback model.
- Guarantee amendment subgraph with dynamic routing is hard to express purely in Elsa without custom activities calling back into domain.
- **Zero automated test projects** in repo — migration would lack safety net.
- Investment Elsa graph already **drifts** from guarantee/loan (detailed vs minimal) — suggests Elsa is not yet a unified orchestration model.

### What would make partial migration Moderate

- **Orchestration-only expansion:** timers, reminders, escalation, richer graphs — while keeping state managers as authority.
- **Single module pilot** (e.g. loan — smallest state manager) with dual-write period.

---

# 6. Migration Challenges

| # | Challenge | Affected files | Impact | Complexity |
|---|-----------|----------------|--------|------------|
| 1 | **Transition tables in C# dictionaries** | `CaseStateManager.cs` (~320 LOC), `GuaranteeCaseStateManager.cs` (~412 LOC), `LoanCaseStateManager.cs` (~265 LOC) | Must re-express as Elsa flow + custom activities or duplicate logic | **Very High** |
| 2 | **Status enums as domain concept** | `CaseStatus.cs`, `GuaranteeCaseStatus.cs`, `LoanCaseStatus.cs`; entity properties | API, DB, Kanban, SMS, frontend all use ints/enums | **Very High** |
| 3 | **ValidateBusinessRules** | State managers + `GuaranteeApplicationCompleteness`, `LoanDocumentRequirements`, etc. | Rules must stay in domain or callable from Elsa activities | **High** |
| 4 | **Role-based authorization in transitions** | State managers, `WorkflowRoleExpander.cs`, `ResolveActorRole()` | Elsa human tasks don't replace JWT/module permission model | **High** |
| 5 | **Guarantee amendment dynamic routing** | `GuaranteeCaseStateManager.ValidateBusinessRules`, `GuaranteeCaseAppService` | Cancellation skips credit review via `ref nextStatus` | **Very High** |
| 6 | **Fund credit guard** | `GuaranteeFundCreditGuard.cs`, `GuaranteeCaseAppService.cs` | Financial invariant outside state table | **High** |
| 7 | **Persistence / dual write** | `*CaseAppService`, `InvestmentCaseWriteExtensions.ApplyStateAsync`, UoW | Elsa-first needs transactional story: who writes `CurrentStatus`? | **Very High** |
| 8 | **WorkflowHistory audit** | `*CaseWorkflowHistory`, domain `TransitionTo` | Must remain legal/audit source; Elsa history ≠ business history today | **High** |
| 9 | **Stage rollback** | `CaseStageRollbackAppService.cs`, `CaseStageRollbackEvaluator.cs`, entity `RollbackTo` | No Elsa compensation today | **High** |
| 10 | **Payment auto-complete** | `InvestmentCase.CheckPaymentCompletion` | Domain-side transition bypassing state manager | **Medium** |
| 11 | **Kanban rules duplication** | `CaseKanbanRules.cs`, `GuaranteeKanbanRules.cs`, `LoanKanbanRules.cs` | Must derive from Elsa state or keep sync job | **High** |
| 12 | **SMS keyed on toStatus** | `WorkflowSmsCatalog.cs`, notifiers | Trigger model tied to domain status change events | **Medium** |
| 13 | **API semantic routes** | `InvestmentCasesController.cs` (many routes), guarantee/loan controllers | Routes imply actions, not Elsa bookmarks | **High** |
| 14 | **Frontend workflow models** | `Frontend/js/workflow-model.js`, `loan-workflow-model.js`, `guarantee-workflow-model.js`, portals | Hard-coded status → UI; not Elsa-driven | **High** |
| 15 | **Signal timing inconsistency** | Investment vs loan/guarantee; guarantee background signal | Elsa-first must unify event model | **Medium** |
| 16 | **Legacy services** | `CaseService.cs`, `ReviewService.cs`, `PaymentService.cs` | Some paths signal Elsa without swallowing errors | **Medium** |
| 17 | **No automated tests** | Entire solution | Regression risk on any migration | **High** (process) |
| 18 | **Dev/prod Elsa persistence split** | `ServiceCollectionExtensions.cs` | Local dev loses instances; migration testing harder | **Medium** |
| 19 | **CQRS absence** | App services directly orchestrate | No natural seam for Elsa activity → command dispatch | **Medium** |
| 20 | **Aggregate boundaries** | `InvestmentCase`, `GuaranteeCase`, `LoanCase` | Rich aggregates with comments, docs, payments — Elsa should not own aggregate state | **High** |

---

# 7. Responsibility Matrix

| Concern | Current owner | Future owner (Elsa-first) | Future owner (recommended hybrid) | Recommendation rationale |
|---------|---------------|---------------------------|-----------------------------------|--------------------------|
| **Valid transitions (status, action, role)** | Application (`*CaseStateManager`) | Elsa flow + custom activities | **Application** | Role matrix is security-sensitive; already tested in production pattern |
| **Business validation (docs, amounts)** | Application / Domain | Domain (called from Elsa activities) | **Domain / Application** | Financial rules must not live only in workflow JSON |
| **CurrentStatus source of truth** | Domain entity + DB | Elsa variable (risky) | **Domain entity + DB** | API, reports, Kanban, SMS depend on SQL status |
| **WorkflowHistory audit** | Domain | Dual-write or Elsa + export | **Domain** | Compliance / audit trail is first-class domain data |
| **Authorization (JWT, permissions)** | API + App services | API + App services | **API + App services** | Elsa does not replace ASP.NET auth |
| **Kanban inbox rules** | Application | Derived from Elsa or synced | **Application** (derived from `CurrentStatus`) | Keep aligned with state manager |
| **SMS notifications** | Application (on status change) | Elsa activity or domain event | **Application** (on status change) | Already keyed on `toStatus`; decouple from engine |
| **Process graph / branching visualization** | Partially in `InvestmentCaseWorkflow` | **Elsa** | **Elsa** (documentation + wait points) | Only module where graph has real structure |
| **Wait for external event** | Custom bookmark activity | **Elsa** | **Elsa** | Already implemented |
| **Timers / reminders / escalation** | Not implemented | **Elsa** | **Elsa** (new capability) | Clear value-add without moving authority |
| **Human task UI** | Test panel + API | Elsa Studio (if adopted) | **Application UI** | Existing Persian RTL panel and policies |
| **Instance lifecycle / recovery** | Elsa orchestrators | **Elsa** | **Elsa** | Already owned |
| **Stage rollback** | `CaseStageRollbackAppService` | Elsa compensation (complex) | **Application** | Admin operation outside normal graph |
| **Fund credit enforcement** | `GuaranteeFundCreditGuard` | Domain service | **Domain / Application** | Financial invariant |
| **API contracts** | Semantic REST routes | Would need redesign or Elsa HTTP | **Unchanged** | Stable external contract |

---

# 8. What Should NEVER Move Into Elsa

These must remain in **Domain / Application** even if Elsa scope expands:

| Rule / concern | Why | Evidence |
|----------------|-----|----------|
| **Fund credit capacity checks** | Financial invariant tied to fund aggregates | `GuaranteeFundCreditGuard.cs` |
| **Document completeness & required attachments** | Domain knowledge, storage integration | `ValidateBusinessRules` in all state managers |
| **Payment sum ≥ approved amount** | Money calculation on aggregate | `InvestmentCase.CheckPaymentCompletion`, `CompletePayment` validation |
| **Amendment amount/date bounds** | Guarantee business rules with entity mutation | `GuaranteeCaseStateManager`, `ApplyApprovedAmendment()` |
| **Authorization / role gates** | Security boundary; JWT + permission model | `CanTransition`, `[Authorize(Policy)]`, `*AuthorizationService` |
| **Audit history (`*CaseWorkflowHistory`)** | Legal/operational audit, reporting | Domain entities, SQL schema |
| **Applicant vs internal data scope** | Repository scoping on transition load | `GetScopedForTransitionAsync` on repositories |
| **Idempotency (correlationId)** | Duplicate-request safety | `TransitionAsync` in state managers |
| **Kanban ownership semantics** | Product rules for inbox, not process engine | `*KanbanRules.cs` |

**Principle:** Elsa is a **process coordinator**; the **case aggregate** owns business state and invariants.

---

# 9. What Could Move Into Elsa

| Capability | Feasibility | Why | Current gap |
|------------|-------------|-----|-------------|
| **Explicit process graph / branching** | High (investment already partial) | `InvestmentCaseWorkflow` has revision loops; could reflect real paths if fed richer signals | Only `status-changed` signal; payload discarded |
| **Waiting / bookmarks** | Already in use | `WaitForCaseSignalActivity` | — |
| **Timers / SLA escalation** | High | Not implemented; Elsa native strength | No reminder/escalation today |
| **Parallel approvals (fork/join)** | Medium | Could model multi-unit sign-off | Sequential state machine today |
| **Workflow versioning** | Medium | Elsa supports definition versions | App enums versioned via migrations only |
| **Operational visibility** | Medium | Instance/bookmark inspection in prod DB | No Studio/dashboard |
| **Reminders / scheduled nudges** | High | SMS infra exists; trigger could be Elsa timer → app activity | SMS only on transition |
| **Process documentation** | High | Code-first graphs document intended flow | Guarantee/loan graphs are placeholders |

**Caution:** Moving **branching** into Elsa only works if **signals carry semantic meaning** (e.g. `revision-requested` vs `approved`) and graphs stay synchronized with `*CaseStateManager` — today they are not (`WorkflowSignals.RevisionRequested` unused in graphs).

---

# 10. Risks

| Risk area | Description |
|-----------|-------------|
| **Testing** | No test projects in solution. Elsa-first migration cannot be validated by automated regression. State managers are implicit spec — lossy if migrated. |
| **Debugging** | Today: trace `CurrentStatus` + `WorkflowHistory` in SQL. Elsa-first: split brain between Elsa instance variables and domain columns. Existing orchestrator reset logic shows bookmark drift already occurs. |
| **Maintainability** | Three parallel state managers + three Elsa definitions + three Kanban rule files + three frontend models — migration adds fourth copy of rules unless single source established. |
| **Performance** | Signal path polls up to 6s (40×150ms) on reset. Guarantee uses fire-and-forget `Task.Run`. More Elsa activities increase runtime DB churn (bookmarks, instances). |
| **Versioning** | Production workflow definition changes affect in-flight instances. App uses enum migrations for status changes — two versioning mechanisms to coordinate. |
| **Production rollout** | Dual-write period required for any authority shift. Rollback of migration is hard if Elsa becomes write path for status. |
| **Transaction consistency** | Today: single domain transaction then best-effort Elsa. Elsa-first: risk of Elsa committed but domain rollback (or reverse) unless outbox/saga introduced — **not present today**. |
| **Dev/prod parity** | Dev uses in-memory Elsa; prod uses PostgreSQL. Migration testing may not reproduce bookmark recovery behavior locally. |
| **Team skills** | Business rules live in C# state managers readable by .NET devs. Elsa-first spreads logic across activities, designer, and persistence. |
| **Frontend contract** | Test panel and API clients expect `currentStatus` int and semantic POST routes — unchanged in partial migration; broken in full Elsa-first without API redesign. |

---

# 11. Final Recommendation

## **Option C — Adopt a hybrid architecture (evolve current model, do not migrate to Elsa-first)**

### Recommendation summary

| Option | Verdict |
|--------|---------|
| **A) Keep current architecture** | **Acceptable** — already works; Elsa adds limited value today for guarantee/loan |
| **B) Move completely to Elsa** | **Not recommended** — Difficult, high risk, duplicates or relocates ~1000+ LOC of battle-tested rules |
| **C) Hybrid (recommended)** | **Recommended** — Application remains authority; selectively deepen Elsa for orchestration features |

### Evidence-based rationale

1. **Elsa is already hybrid by design.** Orchestrators treat it as best-effort after domain commit. Code comments and logs confirm domain-first semantics.

2. **State managers are the real workflow engine.** Combined ~1000 LOC of transitions + validation across three modules, with guarantee amendment dynamics that exceed typical Elsa declarative flows.

3. **Elsa graphs are underutilized.** Guarantee and loan workflows are `Start → Wait → End`. Investment graph does not receive differentiated signals. Full Elsa-first would require rebuilding what state managers already do — without removing them.

4. **Downstream systems bind to `CurrentStatus`, not Elsa.** Kanban (`KanbanAppService`), SMS (`WorkflowSmsCatalog`), DTOs, dashboards, and frontend models all read domain status.

5. **No test safety net.** `.cursor/AGENTS.md` notes no automated test projects. A authority migration is unsafe without investment in characterization tests first.

6. **Partial wins available without migration.** Timers, escalations, richer signals, and investment graph alignment can be added while keeping `*CaseStateManager` as gatekeeper.

### Suggested direction (architectural, not implementation)

| Priority | Action |
|----------|--------|
| 1 | **Keep `*CaseStateManager` as sole transition authority** |
| 2 | **Treat Elsa as optional orchestration layer** — improve observability, do not block API on resume failure (already mostly true) |
| 3 | **Align investment Elsa graph with real signals** if graph position matters for reporting (use distinct signals or pass status in payload) |
| 4 | **Add characterization tests** for transition tables before any engine migration |
| 5 | **Consider Elsa for new cross-cutting concerns** (SLA timers, reminders) via custom activities that call application services — not for status ownership |
| 6 | **Do not pursue Elsa-first** unless product requires external workflow designer ownership and team accepts multi-year dual-model cost |

### When to revisit Option B

Re-evaluate full Elsa-first only if:

- Product owners require **non-developers** to change workflow graphs in production without deploys.
- Multiple new modules need **shared long-running orchestration** (sagas across services) beyond case status.
- Team commits to **test coverage**, **dual-write migration**, and **API versioning** as prerequisites.

Until then, the codebase evidence supports **strengthening the existing hybrid** rather than inverting control to Elsa.

---

## Appendix: Key file index

| Area | Path |
|------|------|
| Elsa DI | `src/Services/CoreService/Core.Workflow/DependencyInjection/ServiceCollectionExtensions.cs` |
| Orchestrators | `src/Services/CoreService/Core.Workflow/Orchestration/Elsa*.cs` |
| Workflows | `src/Services/CoreService/Core.Workflow/Workflows/*CaseWorkflow.cs` |
| Custom activity | `src/Services/CoreService/Core.Workflow/Activities/WaitForCaseSignalActivity.cs` |
| Investment coordinator | `src/Services/CoreService/Core.Application/Services/InvestmentWorkflowCoordinator.cs` |
| State managers | `src/Services/CoreService/Core.Application/Services/*CaseStateManager.cs` |
| App services | `src/Services/CoreService/Core.Application/Services/*CaseAppService.cs` |
| Rollback | `src/Services/CoreService/Core.Application/Services/CaseStageRollbackAppService.cs` |
| Kanban | `src/Services/CoreService/Core.Application/Kanban/*KanbanRules.cs` |
| Domain entities | `src/Services/CoreService/Core.Domain/Entities/*/` |
| Controllers | `src/Services/CoreService/Core.API/Controllers/*CasesController.cs` |
| Frontend models | `Frontend/js/*-workflow-model.js` |
| Existing integration doc | `docs/backend/ELSA_WORKFLOW_INTEGRATION.md` |

---

*End of assessment — read-only; no source code was modified.*
