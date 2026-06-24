---
name: mapping
description: Mapster DTO mapping — no silent property loss. Use with /mapping.
disable-model-invocation: true
---

# /mapping

## Purpose

Guarantee zero silent data loss across DTO boundaries using Mapster — every property mapped, computed, or explicitly ignored.

## When to Use

- `/mapping` invoked
- New/changed request or response DTO
- New/changed entity fields affecting API
- Review gate: mapping
- After `/validation` for write paths

## Responsibilities

1. Define `IRegister` / `NewConfig` per module (centralized, not scattered)
2. Map request → entity/command (writes) and entity → response (reads)
3. Build **property matrix**: Mapped | Computed | Ignored (reason)
4. `.Ignore()` server-generated fields (Id, CreatedAt, RowVersion)
5. Reads: prefer `ProjectToType<T>()` / LINQ `Select`
6. Add mapping tests; re-verify `/persistence` on writes

## Checklist

- [ ] 100% property coverage both directions
- [ ] No secrets in response mappings
- [ ] Manual mapping only with documented exception
- [ ] EF projections used for list/detail reads where applicable

## Success Criteria

No unaccounted DTO properties; mapping tests pass; read-back shows mapped values persisted.

## Failure Conditions

**Fail** if: new DTO without config · validated field not mapped · response field always null · missing tests on changed pairs · large manual `new Response { }` as default pattern

```csharp
config.NewConfig<CreateXRequest, XEntity>()
    .Map(d => d.Title, s => s.Title)
    .Ignore(d => d.Id);
```
