---
name: repository
description: UnitOfWork and generic repository review. Use with /repository.
disable-model-invocation: true
---

# /repository

## Purpose

Keep data access consistent via Generic Repository + UnitOfWork — avoid repository explosion.

## When to Use

- `/repository` invoked
- New UoW accessor or repository method
- Service persistence changes
- Review gate: data access layer

## Responsibilities

1. Services inject `ICoreUnitOfWork` — never `DbContext`
2. Use `_unitOfWork.Users`, `_unitOfWork.Orders`, `_unitOfWork.Products` pattern for entities
3. `EfRepository<T>` for CRUD; business-named query methods only when needed
4. `SaveChangesAsync` on UoW only — repositories never commit
5. Reads: `asNoTracking: true` default
6. All methods async + `CancellationToken`
7. Interfaces in Application; implementations in Infrastructure
8. No naked `IQueryable` to API layer

## Checklist

- [ ] UoW accessor before new repo interface
- [ ] Scoped DI (DbContext, UoW, repos)
- [ ] Write path traceable to SaveChanges
- [ ] Custom queries bounded and named by intent

## Success Criteria

Layer boundaries respected; no redundant repos; write chain complete.

## Failure Conditions

**Fail** if: DbContext in Application · SaveChanges in repository · sync EF · unbounded query exposure · business rules in repository layer

Pair with `/ef` for query performance.
