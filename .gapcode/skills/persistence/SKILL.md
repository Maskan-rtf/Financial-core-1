---
name: persistence
description: Verify DTO to PostgreSQL chain with read-back. Use with /persistence.
disable-model-invocation: true
---

# /persistence

## Purpose

Prove writes persist end-to-end and no DTO property is lost between layers.

## When to Use

- `/persistence` invoked
- Any create/update/delete
- Mapping changes on persisted fields
- Review gate: persistence

## Responsibilities

1. Trace chain: **DTO → Validator → Mapster → Service → Repository → UoW → SaveChangesAsync → PostgreSQL**
2. Field matrix: DTO property → entity column → read-back value
3. Confirm validator runs before service
4. Confirm `SaveChangesAsync` on UoW (repo never commits)
5. Read-back: integration test (PostgreSQL) or fresh `AsNoTracking` query
6. Schema changes: migration in PR; `/ef` for new indexes
7. Log persistence boundary per `/logging`

## Checklist

- [ ] Every hop documented with file reference
- [ ] All persisted DTO fields in read-back assertion
- [ ] No `DbContext` in Application layer
- [ ] Update/delete semantics correct (soft vs hard delete)
- [ ] Concurrency tokens handled if applicable

## Success Criteria

Verdict **Pass**; evidence attached (test or verified scenario); no null columns for mapped validated fields.

## Failure Conditions

**Fail** if: missing SaveChanges · unmapped validated field · InMemory-only proof · 201 but column null · read-back from tracked session only
