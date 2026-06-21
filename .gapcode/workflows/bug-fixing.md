# Bug Fixing Workflow

Minimal fix with regression evidence.

## Trigger

- Incorrect behavior or production defect
- Failing test in CI
- User invokes bug investigation

## Steps

1. **Reproduce**
   - Exact inputs, role, environment
   - Capture `CorrelationId` from logs or response header

2. **Locate layer**
   - Reconstruct timeline from `/logging` events
   - Narrow: validation · authz · mapping · service · repository · DB · frontend state

3. **Trace data** (if data-related)
   - Run `/persistence` — DTO → DB chain
   - Read-back query to confirm actual DB state

4. **Hypothesis & fix**
   - Minimal targeted change — no drive-by refactor
   - Run `/refactor` only if structural change required

5. **Regression test**
   - Integration test for persistence/auth bugs
   - Validator/mapping test for boundary bugs

6. **Side effects**
   - `/security` if auth touched
   - `/ef` if query touched

7. **Verify**
   - Repro fails before / passes after
   - Logs show correct path
   - `workflows/code-review.md` for fix PR

## Required Skills

`/logging` (always) · `/persistence` (data bugs) · `/validation` `/mapping` (boundary bugs) · `/security` `/ef` (as applicable)

## Completion Criteria

- [ ] Root cause documented with evidence
- [ ] Regression test added
- [ ] Fix is minimal; gates still pass
- [ ] No new Critical security/performance regressions
- [ ] Review **Approved**
