---
name: ef
description: EF Core and PostgreSQL performance review. Use with /ef.
disable-model-invocation: true
---

# /ef

## Purpose

Prevent EF Core performance regressions on PostgreSQL.

## When to Use

- `/ef` invoked
- New/changed queries, lists, reports
- Schema/migration with new filters
- Review gate: performance

## Responsibilities

1. Review each query: tracking, projection, bounds, cancellation
2. Enforce `AsNoTracking()` on read-only paths
3. Prefer `Select` / `ProjectToType` over full `Include` graphs
4. Detect N+1 (loop + per-item DB call)
5. Require pagination or explicit limits on lists
6. Recommend indexes via migration for new filter/join/sort columns
7. Filter/sort in SQL — not memory after `ToList()`
8. Flag sync-over-async

## Checklist

- [ ] Reads untracked unless update requires tracking
- [ ] Lists paginated
- [ ] No N+1
- [ ] `CancellationToken` on async EF calls
- [ ] Index migration for hot filters
- [ ] Frontend requests bounded page sizes

## Success Criteria

All queries pass checklist; no Critical performance anti-patterns ship.

## Failure Conditions

**Fail** if: unbounded `ToListAsync` · N+1 introduced · full table load + memory filter · missing index on new hot filter · unnecessary Include graph · `Task.Result`/`Wait` on EF

Schema: EF migration required; use `EXPLAIN ANALYZE` in staging for suspicious LINQ.
