---
name: refactor
description: Safe refactoring preserving invariants. Use with /refactor.
disable-model-invocation: true
---

# /refactor

## Purpose

Improve structure without changing observable behavior; all quality gates remain green.

## When to Use

- `/refactor` invoked
- Extract method/class/service
- Reduce duplication or reorganize namespaces
- `workflows/bug-fixing.md` when fix requires structural change

## Responsibilities

1. Document invariants: API contracts · persistence · permissions · log event names · query shapes
2. Add characterization tests if coverage missing
3. Refactor in small steps — build + test after each
4. Re-run applicable gates: `/validation` `/mapping` `/logging` `/persistence` `/security` `/ef`
5. No scope creep (no feature changes "while here")
6. Decline requests to remove UoW abstraction without architecture approval

## Checklist

- [ ] Invariants listed before changes
- [ ] Tests pass after each step
- [ ] No gate regression
- [ ] EF queries same or better
- [ ] Minimal diff — no drive-by formatting

## Success Criteria

Behavior unchanged (unless explicit); all tests green; review rules pass.

## Failure Conditions

**Fail** if: big-bang rewrite · refactor + feature same PR · auth/logging/validators removed · query shape worsened · behavior change without tests
