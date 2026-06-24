---
name: cqrs
description: Justified command/query separation only. Use with /cqrs.
disable-model-invocation: true
---

# /cqrs

## Purpose

Split reads and writes into commands/queries **only when justified** — simplicity-first; CQRS is not mandatory.

## When to Use

- `/cqrs` invoked
- User requests command/query split
- Service mixes many unrelated reads/writes and justification exists
- **Decline** for trivial CRUD — document why layered service stays

## Responsibilities

1. Require written justification: business reason · technical reason · trade-offs · rollback plan
2. Classify operations: Command (mutates, SaveChanges) vs Query (read-only, no SaveChanges)
3. Commands: `/validation` → `/mapping` → handler → UoW → `/persistence`
4. Queries: `/ef` discipline — AsNoTracking + projection
5. Handlers thin; `/logging` per handler
6. Migrate incrementally; stable API contracts
7. Do not mandate MediatR — plain handlers unless already in use

## Checklist

- [ ] Justification documented
- [ ] No query handler mutates state or calls SaveChanges
- [ ] Commands pass persistence gate
- [ ] No blanket MediatR without need
- [ ] API contracts unchanged unless versioned

## Success Criteria

Clear command/query boundaries; gates pass; simpler than alternative or justified complexity.

## Failure Conditions

**Fail** if: CQRS on trivial surface without justification · query calls SaveChanges · command returns huge graphs · duplicated validation in handler · unjustified MediatR adoption
