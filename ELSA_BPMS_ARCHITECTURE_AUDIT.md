# Elsa BPMS Architecture Audit

Date: 2026-06-26

## Executive Finding

The migration is **not yet a true Elsa-first BPMS architecture**.

The old `*CaseStateManager` classes were removed, but their workflow-routing responsibility was largely relocated into code-level route resolver classes:

- `InvestmentWorkflowRouteResolver`
- `GuaranteeWorkflowRouteResolver`
- `LoanWorkflowRouteResolver`

Elsa workflow definitions currently model broad wait-stage flowcharts, but they do not decide the next business status for a command. The effective next-state decision happens before Elsa is signaled, inside `ElsaWorkflowRuntime`, by calling the route resolvers.

This means workflow ownership is still code-owned, not workflow-definition-owned.

## Audit Checklist

| Check | Result |
|---|---|
| Workflow definitions are the source of routing decisions | Failed |
| Route resolver classes do not contain transition dictionaries or large switch statements | Failed |
| Elsa determines the next process state | Failed |
| Workflow command executors only execute commands and never decide routing | Partially failed |
| Domain owns business rules only | Partially satisfied |
| No workflow logic is duplicated between Elsa and application/workflow code | Failed |

## Evidence

### 1. Workflow Definitions Are Not the Real Routing Source

The workflow definitions contain stage flowcharts and generic wait activities:

- `Core.Workflow/Workflows/InvestmentCaseWorkflow.cs`
- `Core.Workflow/Workflows/GuaranteeCaseWorkflow.cs`
- `Core.Workflow/Workflows/LoanCaseWorkflow.cs`

Examples:

- `InvestmentCaseWorkflow` creates `WaitForCaseSignalActivity` nodes and `Flowchart` connections around lines 24-43 and 76-100.
- `GuaranteeCaseWorkflow` creates broad wait stages and connections around lines 51-65.
- `LoanCaseWorkflow` creates broad wait stages and connections around lines 49-61.

These flowcharts do not map a semantic command such as `Approve`, `RequestRevision`, `UploadFinalContract`, or `RegisterPayment` to a next domain status. They wait for `CoreWorkflowSignals.StatusChanged`, which is still a generic compatibility signal.

The workflows therefore track movement after a decision is made elsewhere; they are not the source of the decision.

### 2. Route Resolver Classes Contain Workflow Transition Tables

The route resolvers contain large switch expressions that map:

`current status + action + role -> next status`

That is workflow-routing logic.

Locations:

- `Core.Workflow/Orchestration/InvestmentWorkflowRouteResolver.cs`
  - `Resolve(...)` starts at line 11.
  - Main routing switch starts at line 24.
  - Admin fallback routing switch starts at line 73.

- `Core.Workflow/Orchestration/GuaranteeWorkflowRouteResolver.cs`
  - `Resolve(...)` starts at line 11.
  - Main routing switch starts at line 27.
  - Admin fallback recursively probes role routes around lines 77-84.

- `Core.Workflow/Orchestration/LoanWorkflowRouteResolver.cs`
  - `Resolve(...)` starts at line 11.
  - Main routing switch starts at line 28.
  - Admin fallback routing switch starts at line 94.

These classes are effectively the deleted state-manager transition dictionaries rewritten as switch expressions.

### 3. Elsa Runtime Resolves Routes Before Signaling Elsa

`Core.Workflow/Orchestration/ElsaWorkflowRuntime.cs` calls route resolvers before it signals the Elsa workflow instance:

- Loan: calls `LoanWorkflowRouteResolver.Resolve(...)` around line 129.
- Guarantee: calls `GuaranteeWorkflowRouteResolver.Resolve(...)` around line 169.
- Investment: calls `InvestmentWorkflowRouteResolver.Resolve(...)` around line 209.

The runtime then calls the corresponding command executor with the already-decided `TargetStatus`.

This sequence means:

1. Runtime reads current domain status.
2. Runtime resolves next status in code.
3. Executor mutates domain state.
4. Runtime signals Elsa with `StatusChanged`.

That is not Elsa-first. It is code-first routing with Elsa notification.

### 4. AllowedActions Projection Also Depends on Route Resolvers

The action providers derive allowed actions by enumerating every workflow action and calling the route resolver:

- `InvestmentWorkflowActionProvider.cs`
- `GuaranteeWorkflowActionProvider.cs`
- `LoanWorkflowActionProvider.cs`

These providers are then used by:

- `ProcessReadModelProjector`
- `KanbanAppService`

This means the frontend-compatible `AllowedActions` model is also based on resolver switch tables, not Elsa workflow metadata/bookmarks.

### 5. Command Executors Mostly Execute, But Some Still Adjust Route Outcomes

The command executors generally perform application/domain validation and mutation. That is the right direction.

However, there are still route-affecting decisions inside executors:

- `GuaranteeWorkflowCommandExecutor`
  - `ValidateBusinessRules(..., ref targetStatus)` starts around line 100.
  - Amendment cancellation and amendment approval logic can change `targetStatus`.
  - Examples:
    - Cancellation amendment submit can choose `AmendmentCreditReview` or `AmendmentCeoApproval`.
    - CEO approval of cancellation can change the target to `Cancelled`.
    - Legal approval of amendment can change target to `AmendmentApproved` or `Cancelled`.

Some of these decisions are business-rule dependent and should stay in domain/application services, but Elsa should own the process branch. The clean model would be:

1. Elsa reaches a branch.
2. Activity asks domain/application for a business outcome, such as `RequiresCreditReview`, `CancellationApproved`, or `AmendmentContractPresent`.
3. Elsa branches based on the activity outcome.
4. Executor persists the selected status without recalculating process routing.

Currently, the executor is still participating in route selection.

## Duplicated Workflow Logic

### Duplicate 1: Elsa Flowchart Connections vs Route Resolver Transitions

Workflow definitions contain stage connections, while route resolvers contain actual next-state mappings.

Example:

- `LoanCaseWorkflow.cs` connects `waitCreditReview -> waitCeoApproval`.
- `LoanWorkflowRouteResolver.cs` maps `PendingCreditReview + Approve + CreditExpert -> PendingCeoInitialApproval`.

The workflow diagram and resolver encode the same conceptual process in two separate forms. The resolver is authoritative; the workflow definition is approximate.

### Duplicate 2: Revision Loops Exist in Both Workflow Definitions and Resolvers

Examples:

- Investment workflow definition connects `waitReview1 -> waitDataEntry1`, `waitReview2 -> waitDataEntry2`, and `waitFinanceReview -> waitFinanceUpload`.
- `InvestmentWorkflowRouteResolver` independently maps revision actions back to `DataEntry1`, `DataEntry2`, and `WaitingFinancialWorksheet`.

### Duplicate 3: Terminal/Archive Rules Remain in Route Resolvers

Each route resolver has terminal-state and archive/admin checks. These are process-routing guard rules, not financial invariants.

They should be modeled as:

- Elsa terminal states.
- Elsa branch/guard activities.
- Authorization activity outcomes.

### Duplicate 4: AllowedActions Rebuilds Workflow Capability Outside Elsa

Allowed actions are derived by probing route resolvers, not by asking Elsa what bookmarks/tasks/actions are active for the process instance.

That duplicates human-task availability outside the workflow engine.

## Why This Exists

This happened because the migration chose a safe intermediate implementation:

- Remove state managers.
- Preserve APIs and frontend contracts.
- Keep domain persistence working.
- Move transition tables from application state managers into workflow-layer resolver classes.

That reduced application-layer ownership but did not make Elsa definitions the routing authority.

The current structure is an internal code state machine in `Core.Workflow`, with Elsa used as a signal/bookmark tracker.

## Required Remediation

### 1. Remove `*WorkflowRouteResolver` as Routing Authority

The route resolver classes should not map `current status + action + role -> next status`.

They should be removed or reduced to thin translation helpers that do not choose the next process state.

### 2. Make Workflows Command-Aware

Replace generic `StatusChanged` waits with command-specific waits/bookmarks or human task activities.

Examples:

- `SubmitApplication`
- `ApproveCreditReview`
- `RequestCreditRevision`
- `UploadFinalContract`
- `RegisterPayment`
- `CompleteRepayment`

The active Elsa workflow node should determine which commands are accepted.

### 3. Move Branching Into Elsa Definitions

Workflow definitions should contain explicit branches for:

- approval
- rejection
- cancellation
- revision loops
- guarantee amendment type
- guarantee cancellation flow
- loan legal/financial revision loops
- investment finance/CEO revision loops

Activities may return outcomes such as:

- `Approved`
- `Rejected`
- `RevisionRequested`
- `RequiresCreditReview`
- `CreditReviewSkipped`
- `Valid`
- `Invalid`
- `PaymentComplete`
- `PaymentIncomplete`

Elsa should select the next activity/status branch from those outcomes.

### 4. Make Executors Persistence-Only

Executors should receive an explicit target selected by Elsa and persist it.

They should not:

- calculate next status,
- rewrite target status,
- contain status transition switches,
- determine allowed actions.

They may:

- load aggregate,
- call domain/application validation services,
- call aggregate mutation methods,
- save status/history/audit,
- return business success/failure outcomes.

### 5. Derive `AllowedActions` From Elsa Runtime State

`AllowedActions` should be projected from active Elsa bookmarks/tasks for the case instance, filtered by authorization.

Recommended shape:

```text
IProcessReadModelProjector
  -> IWorkflowRuntime.GetAvailableCommands(caseId, module, actorRole)
  -> active Elsa bookmarks/tasks
  -> existing DTO-compatible AllowedActions strings
```

### 6. Keep Business Rules in Domain/Application

The existing validation logic in command executors should be extracted into named domain/application services where it is not already reusable.

Examples:

- `InvestmentApplicationCompleteness`
- `InvestmentFinancialWorksheetRules`
- `GuaranteeAmendmentRules`
- `GuaranteeDocumentRequirements`
- `LoanRepaymentRules`

Elsa activities should call those services and branch on their outcomes.

## Final Audit Verdict

The migration is only **partially complete**.

Completed:

- Public APIs remain compatible.
- `*CaseStateManager` classes were removed.
- Application services now dispatch process commands.
- Domain aggregates still persist status/history/audit.
- PostgreSQL remains the business source of truth.

Not complete:

- Elsa workflow definitions are not the source of routing decisions.
- Route resolver classes are replacement state machines.
- Allowed actions are calculated from route resolvers instead of Elsa runtime state.
- Some command executors still influence route outcomes.
- Workflow logic exists in both flowchart definitions and resolver code.

The next migration step should remove `*WorkflowRouteResolver` and make Elsa command-aware with explicit activities and outcomes.
