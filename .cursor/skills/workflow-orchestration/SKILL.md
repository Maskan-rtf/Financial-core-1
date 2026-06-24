---
name: workflow-orchestration
description: Case workflow state transitions, coordinators, state managers, and Elsa signals for investment, guarantee, and loan modules. Use when changing workflow steps, status enums, ApplyTransitionAsync, coordinators, or Elsa integration.
disable-model-invocation: true
---

# Workflow Orchestration

## Purpose

Ensure case workflow changes follow the established state-machine pattern and do not bypass `*CaseStateManager` or break Elsa signal timing.

## When to Use

- Adding/removing workflow status or action
- Refactoring `ApplyTransitionAsync` or coordinators
- Elsa signal/orchestrator changes
- Aligning investment/guarantee/loan patterns
- Kanban ownership changes tied to status

## Read first

- `.cursor/modules-reference.md` — per-module files and enums
- `.cursor/architecture-map.md` — lifecycle diagram
- `.cursor/agent-playbook.md` — task recipes

## Module patterns

### Investment

```
InvestmentCaseAppService (auth, validation)
  → IInvestmentWorkflowCoordinator.ApplyTransitionAsync
  → ICaseStateManager.TransitionAsync
  → Persist (ExecuteUpdate + history)
  → SMS on real transition
  → ICaseWorkflowOrchestrator.SignalAsync(StatusChanged)
```

Files:
- `InvestmentWorkflowCoordinator.cs`
- `CaseStateManager.cs`
- `ElsaCaseWorkflowOrchestrator.cs`
- `InvestmentCaseWorkflow.cs`

### Guarantee / Loan

```
*CaseAppService.ApplyTransitionAsync (private)
  → *CaseStateManager.TransitionAsync
  → PersistTransitionAsync
  → Elsa signal ONLY if WorkflowHistory count increased
```

Files:
- `GuaranteeCaseAppService.cs` / `LoanCaseAppService.cs`
- `GuaranteeCaseStateManager.cs` / `LoanCaseStateManager.cs`
- `ElsaGuaranteeWorkflowOrchestrator.cs` / `ElsaLoanWorkflowOrchestrator.cs`

## Responsibilities

1. **All status changes** go through `*CaseStateManager.TransitionAsync` — never `entity.CurrentStatus = …` in AppService
2. Add transition tuple `(CurrentStatus, Action, Role) → NextStatus` in state manager
3. Add `ValidateBusinessRules` when documents, worksheet, or payments required
4. Persist workflow history + comments before Elsa signal (when history changes)
5. Use correct Elsa orchestrator per module
6. Update `*KanbanRules` when new status affects inbox
7. Update frontend `*-workflow-model.js` for allowed actions (if UI in scope)

## Checklist

- [ ] Enum added to `WorkflowAction` / `*CaseStatus` if needed
- [ ] Transition registered in `*CaseStateManager`
- [ ] Business rules validated in state manager (not controller)
- [ ] AppService exposes thin public method → `ApplyTransitionAsync`
- [ ] Controller has correct `[Authorize(Policy)]`
- [ ] Investment uses coordinator (not direct state manager from AppService)
- [ ] Elsa signal timing matches module convention
- [ ] No API response shape change unless requested
- [ ] Migration only if new persisted enum value requires schema change
- [ ] `dotnet build Maskan.Panel.sln` clean

## Safe refactor rules

- **Do not** change transition outcomes when refactoring orchestration layout
- **Do not** move business validation from state manager to controller
- Extracting coordinators (like investment) is allowed if behavior identical
- Guarantee/loan signal only on history increase; investment signals after every successful transition attempt — preserve each module's timing

## Failure conditions

**Fail** if:
- Direct status mutation outside state manager
- Elsa signaled before persistence
- Missing role gate on new transition
- Frontend action exposed without backend transition
- Legacy `Services.CoreService` old CaseService path reintroduced

## Success criteria

State machine is sole authority; persistence committed before signal; kanban rules updated; build clean; workflow model aligned.
