# Modules Reference — Financial-Core

Per-module file map, enums, and workflow notes for coding agents.

---

## Investment cases

### Purpose

Startup investment attraction workflow: two data-entry phases, expert reviews, valuations, legal contract flow, financial worksheet, CEO approval, payments, completion.

### Key files

| Layer | Path |
|-------|------|
| Controller | `Core.API/Controllers/InvestmentCasesController.cs` |
| App service | `Core.Application/Services/InvestmentCaseAppService.cs` |
| Coordinator | `Core.Application/Services/InvestmentWorkflowCoordinator.cs` |
| State manager | `Core.Application/Services/CaseStateManager.cs` |
| Interface | `Core.Application/Abstractions/ICaseStateManager.cs` |
| Repository | `Core.Infrastructure/Persistence/InvestmentCaseRepository.cs` |
| Entity | `Core.Domain/Entities/Investment/InvestmentCase.cs` |
| Workflow | `Core.Workflow/Workflows/InvestmentCaseWorkflow.cs` |
| Orchestrator | `Core.Workflow/Orchestration/ElsaCaseWorkflowOrchestrator.cs` |
| Kanban | `Core.Application/Kanban/CaseKanbanRules.cs` |
| Auth | `Core.Application/Authorization/CaseAuthorizationService.cs` |
| Permissions | `Core.Application/Authorization/CasePermissions.cs` |
| Frontend model | `Frontend/js/workflow-model.js` |
| Frontend portal | `Frontend/js/portal.js` |
| API guide | `docs/frontend/INVESTMENT_CASE_API_GUIDE.md` |

### Enums

- `CaseStatus` — `Draft`, `DataEntry1`, `ReviewDataEntry1`, … `WaitingPayment`, `Completed`, `Rejected`, `Cancelled`, `Archived`
- `CasePhase` — parallel phase dimension (`DataEntry`, `ExpertReview`, `Legal`, `Finance`, …)
- `WorkflowAction` — `Submit`, `Approve`, `RequestRevision`, `Reject`, `Cancel`, `UploadPreliminaryContract`, `FinalizeContractDraft`, `ConfirmSignature`, `UploadSignedContract`, `SubmitFinancialWorksheet`, `ApproveFinancialWorksheet`, `CompletePayment`, `Archive`, …

### Transition flow

```
InvestmentCaseAppService.ApplyTransitionAsync (auth + validation)
  → InvestmentWorkflowCoordinator.ApplyTransitionAsync
  → CaseStateManager.TransitionAsync
  → PersistCaseTransitionAsync (ExecuteUpdate + history)
  → SMS (on real transition)
  → ElsaCaseWorkflowOrchestrator.SignalAsync(StatusChanged)
```

### Special behaviors

- **Auto-advance on document upload:** preliminary contract, signed contract (`TryAdvance*WorkflowAsync`)
- **Auto-complete on payments:** when confirmed payments ≥ approved amount (`TryAutoCompleteCaseAfterPaymentsAsync` → `CompletePayment`)
- **Valuation recording:** does not transition state — only adds `InvestmentCaseValuation` rows
- **Internal comments:** on approve actions at review stages (permission `cases:create_internal_comment`)

---

## Guarantee cases

### Purpose

Corporate guarantee issuance: credit review, approval form, CEO approvals (initial/final), legal contracts, financial attachment review, issuance documents. Supports **amendments** (extension/reduction) and **cancellation** subflows.

### Key files

| Layer | Path |
|-------|------|
| Controller | `Core.API/Controllers/GuaranteeCasesController.cs` |
| App service | `Core.Application/Services/GuaranteeCaseAppService.cs` |
| State manager | `Core.Application/Services/GuaranteeCaseStateManager.cs` |
| Repository | `Core.Infrastructure/Persistence/GuaranteeCaseRepository.cs` |
| Entity | `Core.Domain/Entities/Guarantee/GuaranteeCase.cs` |
| Amendment history | `Core.Domain/Entities/Guarantee/GuaranteeAmendmentHistoryRecord.cs` |
| Workflow | `Core.Workflow/Workflows/GuaranteeCaseWorkflow.cs` |
| Orchestrator | `Core.Workflow/Orchestration/ElsaGuaranteeWorkflowOrchestrator.cs` |
| Kanban | `Core.Application/Kanban/GuaranteeKanbanRules.cs` |
| Auth | `Core.Application/Authorization/GuaranteeAuthorizationService.cs` |
| Amendment helpers | `Core.Application/Common/GuaranteeAmendment*.cs`, `GuaranteeCancellationSource.cs` |
| Frontend | `Frontend/js/guarantee-workflow-model.js`, `guarantee-portal.js` |
| API guide | `docs/frontend/GUARANTEE_CASE_API_GUIDE.md` |

### Enums

- `GuaranteeCaseStatus` — main flow + `AmendmentDraft` … `AmendmentRejected` + `Cancelled`
- `GuaranteeWorkflowAction` — `Submit`, `Approve`, `Reject`, `RequestRevision`, `UploadDraftContract`, `SubmitSignedPackage`, `ApproveAttachments`, `UploadFinalContract`, `UploadIssuanceDocuments`, `BeginAmendment`, `Cancel`, `Archive`
- `AmendmentType` — `Extension`, `Reduction`, `Cancellation`
- `GuaranteeDocumentType`, `GuaranteeType`, `GuaranteeCasePhase`

### Transition flow

```
GuaranteeCaseAppService.ApplyTransitionAsync
  → GuaranteeCaseStateManager.TransitionAsync
  → PersistTransitionAsync
  → (optional) EnsureApprovalFormSeededAsync
  → ElsaGuaranteeWorkflowOrchestrator.SignalGuaranteeCaseAsync (only when history increased)
```

### Amendment API surface (controller)

- `POST .../amendment` — begin amendment
- `POST .../amendment/submit`, `approve`, `reject`, `request-revision`
- `GET .../amendment/details`
- Cancellation: `.../amendment/cancellation/*`

### Deprecated

- `IGuaranteeRenewalAppService` — returns conflict; use amendments
- `GuaranteeRenewalsController` — deleted

---

## Loan cases

### Purpose

Loan facility workflow: application, credit review, CEO approval, legal raw/final contract, applicant signature, financial review, payment registration, completion.

### Key files

| Layer | Path |
|-------|------|
| Controller | `Core.API/Controllers/LoanCasesController.cs` |
| App service | `Core.Application/Services/LoanCaseAppService.cs` |
| State manager | `Core.Application/Services/LoanCaseStateManager.cs` |
| Repository | `Core.Infrastructure/Persistence/LoanCaseRepository.cs` |
| Workflow | `Core.Workflow/Workflows/LoanCaseWorkflow.cs` |
| Orchestrator | `Core.Workflow/Orchestration/ElsaLoanWorkflowOrchestrator.cs` |
| Kanban | `Core.Application/Kanban/LoanKanbanRules.cs` |
| Frontend | `Frontend/js/loan-workflow-model.js`, `loan-portal.js` |
| API guide | `docs/frontend/LOAN_CASE_API_GUIDE.md` |

### Enums

- `LoanCaseStatus` — `Draft`, `DataEntry`, `PendingCreditReview`, … `Completed`, `CanceledByCeo`, `Archived`
- `LoanWorkflowAction` — `Submit`, `Approve`, `RequestRevision`, `SubmitInstallments`, `SubmitSignedPackage`, `UploadFinalContract`, `RegisterPayment`, …
- `LoanDocumentType`, `LoanFacilityType`, `LoanCasePhase`

### Transition flow

Same as guarantee (inline in AppService, signal only when history increases).

### Special behaviors

- Auto-advance after final contract document upload
- Payment registration may trigger `RegisterPayment` transition when installments satisfied

---

## Kanban (cross-module)

| File | Role |
|------|------|
| `KanbanController.cs` | `action-required`, `watching` endpoints |
| `KanbanAppService.cs` | Aggregates all modules |
| `KanbanDtoMapper.cs` | Card DTO mapping |
| `Frontend/js/kanban.js` | Inbox UI tab |

Investment-specific kanban also exposed on `InvestmentCasesController` (`kanban/action-required`, `kanban/watching`).

---

## Fund credit limits

| File | Role |
|------|------|
| `FundCreditLimitsController.cs` | Global period limits CRUD |
| `FundCreditLimitAppService.cs` | Business logic |
| `GuaranteeCasesController` | Case-scoped fund credit checks |
| `GuaranteeFundCreditGuard.cs` | Credit guard helpers |
| `Frontend/js/fund-credit-limits.js` | CEO limits UI |
| `Frontend/js/fund-credit-capacity-ui.js` | Per-case widget |

---

## Identity & companies

| File | Role |
|------|------|
| `UserController.cs` | OTP auth, sessions, user admin |
| `CompaniesController.cs` | Applicant companies + admin CRUD |
| `ICompanyAppService` | Company management |
| `Permissions.cs` | JWT permission strings |
| `Frontend/js/admin-users.js`, `admin-companies.js` | Admin tabs |

Tokens are **JWE** — roles from login response, not client decode.

---

## Dashboard & analytics

| File | Role |
|------|------|
| `DashboardController.cs` | Role dashboards (`me`, `ceo`, `board`, …) |
| `EmployeeKpiAnalyticsController.cs` | KPI queries |
| `DashboardAggregationService.cs` | Snapshot aggregation |
| `Frontend/js/home-dashboard.js` | Home cockpit |
| `Frontend/js/admin-dashboard.js` | Admin charts |
| `Frontend/js/employee-kpi-dashboard.js` | KPI sub-tab |

---

## State manager change checklist

When adding a workflow step to any module:

1. Add enum value if new status (requires migration if persisted — check if status is stored as string/int).
2. Add `WorkflowAction` (or module equivalent) if new action.
3. Add transition tuple to `*CaseStateManager` dictionary.
4. Add `ValidateBusinessRules` case if documents/data required.
5. Add AppService public method → `ApplyTransitionAsync`.
6. Add controller endpoint with correct `[Authorize]` policy.
7. Update `*KanbanRules` for inbox ownership.
8. Update frontend `*-workflow-model.js` allowed actions + portal UI.
9. Signal Elsa after persist (follow module pattern).

**Do not** mutate `entity.CurrentStatus` directly outside `*CaseStateManager`.
