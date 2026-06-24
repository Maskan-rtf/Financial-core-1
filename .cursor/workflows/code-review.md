# Code Review Workflow

Enforce quality gates with explicit verdict.

## Trigger

- PR ready for merge
- User requests review
- End of feature-development or bug-fixing workflow
- Reviewer agent role

## Steps

1. **Scope** — identify changed files; classify feature fix vs refactor

2. **Build evidence**
   - Confirm `dotnet build` and `dotnet test` run (or run them)
   - Require commands in PR for non-trivial changes

3. **Run gates** (invoke skills; record Pass/Fail)

   | Gate | Skill |
   |------|-------|
   | Validation | `/validation` |
   | Mapping | `/mapping` |
   | Logging | `/logging` |
   | Persistence | `/persistence` |
   | Security | `/security` |
   | Data access | `/repository` |
   | Performance | `/ef` |
   | Workflow (case modules) | `/workflow-orchestration` |

4. **Trace paths** (feature PRs)
   - One write path end-to-end
   - One read path end-to-end

5. **Architecture** — `.cursor/AGENTS.md`, `.cursor/architecture-map.md`, layer rules in `memory/engineering-memory.md`

6. **Frontend** (if changed) — `.cursor/frontend-guide.md`; loading/error/empty; `TestPanel.apiRequest` only

7. **Verdict**
   - **Rejected**: any gate Fail, blockers with `file:line` + fix
   - **Approved with comments**: all pass, minor suggestions
   - **Approved**: all pass

## Required Skills

All applicable from gate table. `/refactor` for refactor-only PRs.

## Completion Criteria

- [ ] Verdict issued (exactly one)
- [ ] Gate matrix attached to review
- [ ] No open blockers
- [ ] Rejected PRs list required fixes — no "fix later" for blockers
- [ ] Approved PRs meet Definition of Done
