---
name: frontend-workflow
description: Case workflow portals and models — investment, guarantee, loan step flows. Use with /frontend-workflow or when changing workflow steps in Frontend/.
disable-model-invocation: true
---

# /frontend-workflow

## Purpose

Implement and extend case workflow UIs following the established model + portal pattern for investment, guarantee, and loan cases.

## When to Use

- `/frontend-workflow` invoked
- New workflow step or role action in a case portal
- New case type workflow
- Review gate: workflow UI correctness

## Architecture

Each case type has a **workflow model** (step definitions, roles, units) and a **portal** (UI that renders steps):

| Case type | Model | Portal |
|-----------|-------|--------|
| Investment | `js/workflow-model.js` | `js/portal.js` |
| Guarantee | `js/guarantee-workflow-model.js` | `js/guarantee-portal.js` |
| Loan | `js/loan-workflow-model.js` | `js/loan-portal.js` |

Shared infrastructure:
- `js/cases-hub.js` — cases list + detail shell
- `js/kanban.js` — inbox (action-required / watch)
- `workflow-runner.js` — headless E2E runner (not loaded by index.html)

## Responsibilities

1. Define steps in workflow model: id, label, role, unit, allowed actions, API endpoints
2. Portal renders current step UI, calls API via `/frontend-api` patterns
3. Step transitions match backend workflow state machine
4. Role-based visibility: only show actions the current user role can perform
5. Document upload steps use presign upload/download patterns
6. Comments and revision history via shared ui-components
7. Fund credit widgets: `fund-credit-capacity-ui.js`, `fund-credit-limits.js`

## Checklist

- [ ] Step added to workflow model with correct role/unit
- [ ] Portal renders step with loading/error/empty (`/frontend-ui`)
- [ ] API calls use `TestPanel.apiRequest` (`/frontend-api`)
- [ ] Step transition calls correct backend endpoint
- [ ] Role guard prevents unauthorized actions in UI (server still enforces)
- [ ] Script registered in `index.html` load order if new file
- [ ] E2E runner updated if headless testing needed

## Success Criteria

Workflow step works end-to-end: correct role sees action, API call succeeds, UI advances to next step.

## Failure Conditions

**Fail** if: step in portal but missing from model · wrong API endpoint · no role guard · missing script in index.html · step transition doesn't match backend state · upload step without presign flow

## Workflow model shape

```javascript
// Typical step in workflow-model.js
{
  id: "review",
  label: "بررسی",
  role: "Reviewer",
  unit: "Investment",
  actions: ["approve", "reject", "request-revision"]
}
```

## Reference

- `Frontend/README.md` — full file map
- `docs/frontend/DEVELOPER_WORKFLOWS.md`
- `docs/frontend/INVESTMENT_CASE_API_GUIDE.md`
