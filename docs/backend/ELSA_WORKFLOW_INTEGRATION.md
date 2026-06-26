# Elsa Workflow Integration

How Financial-Core uses [Elsa Workflows](https://elsa-workflows.github.io/elsa-core/) across **Investment**, **Guarantee**, and **Loan** modules.

---

## Summary

| Layer | Owner | Responsibility |
|-------|-------|----------------|
| **Business logic** | Our services | Status rules, roles, validation, persistence, SMS, Kanban, API |
| **Workflow engine** | Elsa | Instance lifecycle, wait/resume via bookmarks, flowchart position |

**Elsa is not the source of truth for case status.** Domain state lives in PostgreSQL (`CurrentStatus`, `WorkflowHistory`). Elsa runs a companion instance per case and advances when we send a signal after a successful transition.

---

## Request Flow (all modules)

```
API Controller
  → *CaseAppService
  → [InvestmentWorkflowCoordinator]   (investment only)
  → *CaseStateManager.TransitionAsync
  → Persist status + WorkflowHistory (+ comments)
  → SMS / side effects
  → Elsa orchestrator.Signal*(StatusChanged)
```

On **case create**, the app service starts an Elsa instance and stores `WorkflowInstanceId` on the case (instance id = case GUID).

---

## Module Comparison

| | Investment | Guarantee | Loan |
|---|------------|-----------|------|
| **State machine** | `CaseStateManager` | `GuaranteeCaseStateManager` | `LoanCaseStateManager` |
| **Transition entry** | `InvestmentWorkflowCoordinator` | `GuaranteeCaseAppService` (inline) | `LoanCaseAppService` (inline) |
| **Elsa orchestrator** | `ElsaCaseWorkflowOrchestrator` | `ElsaGuaranteeWorkflowOrchestrator` | `ElsaLoanWorkflowOrchestrator` |
| **Workflow definition** | `InvestmentCaseWorkflow` | `GuaranteeCaseWorkflow` | `LoanCaseWorkflow` |
| **Signal timing** | After every successful transition | Only when `WorkflowHistory` count increases | Only when `WorkflowHistory` count increases |
| **Signal delivery** | Synchronous | Background task (`GuaranteeWorkflowBackgroundSignaler`) | Synchronous |

---

## What Our Services Handle

- **Allowed actions & next status** — `*CaseStateManager` transition tables
- **Authorization** — policies, module permissions, role gates on transitions
- **Business rules** — documents, worksheets, payments, amendment/cancellation logic
- **Persistence** — case entity, `*CaseWorkflowHistory`, comments
- **Notifications** — workflow SMS (`IWorkflowSmsNotifier`)
- **Kanban / inbox** — status-based ownership rules (not Elsa)
- **Frontend actions** — `*-workflow-model.js` mirrors backend transitions

Key files: `Core.Application/Services/*CaseAppService.cs`, `*CaseStateManager.cs`, `InvestmentWorkflowCoordinator.cs`.

---

## What Elsa Actually Does

1. **Start** — dispatch workflow definition with `CaseId` input; instance/correlation id = case GUID.
2. **Wait** — custom `WaitForCaseSignalActivity` creates a bookmark for signal `status-changed`.
3. **Resume** — orchestrator calls `IWorkflowResumer` with `CaseSignalStimulus` `{ CaseId, Signal }`.
4. **Advance** — flowchart moves to the next wait node (investment has many nodes + revision loops; loan/guarantee use a single wait loop).
5. **Recovery** — if bookmark mismatch: delete instance/bookmarks, restart workflow (investment/loan/guarantee orchestrators).

Elsa does **not** decide approve/reject, validate data, or write to our case tables.

---

## Elsa Features We Use

| Feature | Usage |
|---------|--------|
| Code-first workflows (`WorkflowBase`) | 3 definitions in `Core.Workflow/Workflows/` |
| Flowchart model | Investment: multi-step graph; Loan/Guarantee: Start → Wait → End |
| Custom activity | `WaitForCaseSignalActivity` (bookmark + resume) |
| Workflow dispatch | `IWorkflowDispatcher.DispatchAsync` on case create |
| Bookmarks & resume | `IBookmarkStore`, `IWorkflowResumer` |
| Instance management | `IWorkflowInstanceManager` (reset on failure) |
| EF Core + PostgreSQL | Production/non-dev persistence for definitions & instances |
| Definition lookup | `IWorkflowDefinitionService.FindWorkflowDefinitionAsync` |

Registration: `Core.Workflow/DependencyInjection/ServiceCollectionExtensions.cs`.

---

## Elsa Features We Do Not Use

| Feature | Notes |
|---------|--------|
| Elsa Studio / visual designer | Workflows are C# code only |
| Elsa HTTP API / dashboard | Not exposed; app talks via orchestrators |
| Timers, delays, cron | No scheduled Elsa activities |
| Human tasks / Elsa approval UI | Approvals are our API + state manager |
| External activity integrations | No HTTP/email/Script steps in Elsa |
| Workflow-driven business rules | No status branching inside Elsa based on payload |
| Multiple signal types in graphs | All wait nodes listen for `status-changed` only |
| Elsa as status authority | Case status always from domain entity |

Defined but unused with Elsa: `WorkflowSignals` entries like `case-submitted`, `approved`, `rejected`, `payment-completed`, `contract-signed`, and `revision-requested` (workflows only bind `status-changed`).

---

## Workflow Definitions (shape)

**Investment** — long flowchart: data entry → review → valuation → legal → contract → finance → payment, with revision loops between paired steps.

**Guarantee / Loan** — minimal:

```
Start → WaitForCaseSignal (status-changed, self-loop) → End
```

Graph structure documents intended process order for investment; execution is still signal-driven after our state machine commits.

---

## Development vs Production

- **Development:** Elsa runs without PostgreSQL persistence (in-memory).
- **Production:** `UseWorkflowManagement` + `UseWorkflowRuntime` with PostgreSQL connection string.

---

## Related Code

| Area | Path |
|------|------|
| Orchestrators | `Core.Workflow/Orchestration/Elsa*WorkflowOrchestrator.cs` |
| Custom activity | `Core.Workflow/Activities/WaitForCaseSignalActivity.cs` |
| Abstractions | `Core.Application/Abstractions/I*WorkflowOrchestrator.cs` |
| Agent skill | `.cursor/skills/workflow-orchestration/SKILL.md` |
