# Feature Development Workflow

End-to-end delivery for new API capabilities and optional React/Next/Vue UI.

## Trigger

- New user-facing capability
- New endpoint module or workflow step
- Full-stack feature request

## Scope check

Read `.cursor/AGENTS.md` and `.cursor/modules-reference.md` to identify the target module (investment / guarantee / loan / identity / dashboard / fund-credit).

For **workflow steps**, also read `.cursor/skills/workflow-orchestration/SKILL.md` and `.cursor/agent-playbook.md`.

## Steps

1. **Design** (Architect role)
   - Define request/response DTOs and status codes
   - Plan validators, Mapster mappings, UoW/repository changes
   - Plan migrations/indexes if schema changes
   - For workflow: plan `*CaseStateManager` transition + coordinator/AppService path + Elsa signal
   - Define `BusinessProcess` logging steps (`ApplicationLog` in app services)
   - Write test plan

2. **Contracts**
   - Implement DTOs
   - Run `/validation` — validators + tests
   - Run `/mapping` — config + property matrix + tests

3. **Persistence**
   - Service + repository/UoW changes
   - EF migration if needed (`/ef` for indexes)
   - Run `/persistence` — chain trace + read-back integration test

4. **Observability & security**
   - Run `/logging` — boundary events
   - Run `/security` — auth matrix + IDOR

5. **API surface**
   - Thin controller/endpoint; propagate `CancellationToken`
   - `[Authorize]` policies applied

6. **Frontend** (if in scope)
   - Read `.cursor/frontend-guide.md`
   - Update `*-workflow-model.js` + portal for workflow features
   - Use `TestPanel.apiRequest` + `unwrapEnvelope` (`skills/frontend-api`)
   - Loading, error, empty states on every async call
   - Pass `X-Correlation-Id` when supported

7. **Verify**
   - `dotnet build && dotnet test`
   - Run `workflows/code-review.md`

## Required Skills

`/validation` · `/mapping` · `/persistence` · `/logging` · `/security` · `/repository` · `/ef`

Workflow features: `/workflow-orchestration`

Frontend: `/frontend-api` · `/frontend-ui`

Use `/cqrs` only if justified.

## Completion Criteria

- [ ] All applicable skills report **Pass**
- [ ] Definition of Done met (`memory/engineering-memory.md`)
- [ ] Code review workflow **Approved**
- [ ] PR includes build/test commands and verification notes
- [ ] No TODO stubs or deferred gates
