# Elsa BPMS Final Architecture Audit

Date: 2026-06-26

## Verdict

The workflow routing authority has been moved out of application route resolver classes and into Elsa workflow definitions.

The implementation is now Elsa-first for Investment, Guarantee, and Loan routing:

- `InvestmentCaseWorkflow`
- `GuaranteeCaseWorkflow`
- `LoanCaseWorkflow`

These workflow definitions now declare command-specific Elsa bookmark nodes through `WaitForCaseCommandActivity`. Each command node carries:

- command name
- allowed workflow role metadata
- target status metadata
- graph connection to the next workflow stage

`ElsaWorkflowRuntime` no longer computes transitions from status/action/role switch statements. It finds the active Elsa command bookmark, reads its workflow-defined target status, invokes the appropriate executor, burns stale sibling command bookmarks, and resumes the selected Elsa bookmark.

## Audit Evidence

### Removed Workflow Route Resolvers

The following routing-authority classes were removed:

- `Core.Workflow/Orchestration/InvestmentWorkflowRouteResolver.cs`
- `Core.Workflow/Orchestration/GuaranteeWorkflowRouteResolver.cs`
- `Core.Workflow/Orchestration/LoanWorkflowRouteResolver.cs`

Final source scan found no remaining references to:

- `WorkflowRouteResolver`
- `ResolveAdminRoute`
- workflow-action transition switch expressions
- workflow-action transition dictionaries

### Workflow Definitions Own Routing

The workflow graphs now contain the routing source:

- `Core.Workflow/Workflows/InvestmentCaseWorkflow.cs`
- `Core.Workflow/Workflows/GuaranteeCaseWorkflow.cs`
- `Core.Workflow/Workflows/LoanCaseWorkflow.cs`

Each module defines stages and explicit command branches using `WaitForCaseCommandActivity`.

Examples:

- Investment: draft, data entry, review, valuation, contract, worksheet, CEO approval, payment, completion, cancellation, rejection, archive.
- Guarantee: draft, data entry, credit review, approval form, CEO approvals, contracts, financial attachment review, issuance, amendment, cancellation/rejection/archive.
- Loan: draft, data entry, credit review, CEO approvals, legal review, financial review, final contract, payment, repayment, completion, archive.

### Runtime Executes, Does Not Route

`Core.Workflow/Orchestration/ElsaWorkflowRuntime.cs` now:

- parses the semantic command enum
- asks `ElsaCommandBookmarkReader` for matching active Elsa bookmark(s)
- reads `TargetStatus` from bookmark metadata produced by workflow activity nodes
- invokes:
  - `InvestmentWorkflowCommandExecutor`
  - `GuaranteeWorkflowCommandExecutor`
  - `LoanWorkflowCommandExecutor`
- resumes the selected Elsa bookmark after successful domain/application execution

It does not contain workflow transition tables.

The only remaining module switch in `ElsaWorkflowRuntime` dispatches by case module (`Investment`, `Guarantee`, `Loan`). That is adapter dispatch, not workflow routing.

### Route Resolver Logic Was Not Relocated

`ElsaCommandBookmarkReader` does not contain transition dictionaries or large routing switch statements.

It only:

- reads active `WaitForCaseCommandActivity` bookmarks from Elsa runtime persistence
- matches by `CaseId` and `CommandName`
- filters by bookmark role metadata
- returns bookmark metadata already emitted by Elsa workflow nodes

Allowed-action providers now project active Elsa bookmarks:

- `InvestmentWorkflowActionProvider`
- `GuaranteeWorkflowActionProvider`
- `LoanWorkflowActionProvider`

They no longer enumerate all enum values through transition resolvers.

### Domain Boundary

Business rules remain in application/domain execution services:

- `InvestmentWorkflowCommandExecutor`
- `GuaranteeWorkflowCommandExecutor`
- `LoanWorkflowCommandExecutor`

These executors still enforce completeness, document requirements, payment checks, amendment validations, repayment checks, aggregate mutations, history, comments, persistence, and notifications.

They do not decide the general workflow route. They execute the target selected by the active Elsa command bookmark.

One guarantee amendment branch remains intentionally domain-data-dependent: amendment data entry can proceed to credit review or directly to CEO approval based on amendment data. Elsa exposes both possible command branches; the domain executor persists the actual valid target, and runtime resumes the Elsa bookmark whose workflow-defined target matches the persisted status. This avoids duplicating amendment business rules in Elsa activities or route resolver code.

### Obsolete Signal Routing Removed

The old passive Elsa signal bookmark mechanism was removed from workflow routing:

- `WaitForCaseSignalActivity` deleted
- `CaseSignalStimulus` deleted
- `CoreWorkflowSignals` deleted

Legacy `StatusChanged` signal calls remain as no-op compatibility hooks in the orchestrators so old non-routing callers cannot reset or corrupt command bookmarks.

### Database

No additional schema migration was required for this final routing cleanup.

The existing process correlation migration remains valid:

- `20260626125010_AddProcessInstances`
- `Process.process_instances`

Existing aggregate status/history tables remain the business source of truth and frontend compatibility read model.

## Verification

Command executed:

```powershell
dotnet build D:\work\Maskan\Panel+\Financial-Core\Maskan.Panel.sln
```

Result:

- Build succeeded.
- 0 warnings.
- 0 errors.

Routing cleanup scan executed for:

- `WorkflowRouteResolver`
- `WaitForCaseSignalActivity`
- `CaseSignalStimulus`
- `CoreWorkflowSignals`
- `MirrorUnitManager`
- workflow-action transition dictionaries
- workflow-action transition switch expressions

Result:

- No remaining source hits.

## Final Assessment

The migration now satisfies the requested architectural rule:

```text
Elsa Workflow Definitions decide workflow routing.
Application/runtime code executes selected commands.
Domain/application services enforce business rules and persistence.
```

Public API routes, controllers, DTOs, authentication, authorization, and frontend contracts remain unchanged.
