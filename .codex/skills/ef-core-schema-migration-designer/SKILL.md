---
name: ef-core-schema-migration-designer
description: Design EF Core entity changes, relational schema updates, and production-safe migrations for SQL-backed ASP.NET Core systems. Use when changing tables, relationships, constraints, indexes, nullability, or backfill behavior.
---

# EF Core Schema Migration Designer

Design the schema change for real production constraints, not just for local correctness. Treat migration safety and query impact as first-class concerns.

## Workflow

1. Define the desired domain and persistence shape.
2. Compare it against the existing entity configuration and database assumptions.
3. Decide whether the change is:
   - additive
   - destructive
   - data-transforming
   - performance-sensitive
4. Design the migration sequence:
   - schema change
   - backfill or data repair
   - app compatibility window
   - cleanup step if needed
5. Review query and index impact before finalizing.
6. Verify the new model with realistic read and write paths.

## Rules

- Prefer additive migrations when a zero-downtime rollout matters.
- Treat nullability changes, unique constraints, and enum reshaping as risk areas.
- Add indexes intentionally based on query shape, not habit.
- If a migration may lock hot tables or rewrite large data volumes, call that out explicitly.
- Keep EF configuration explicit for keys, lengths, relationships, conversions, and delete behavior.

## Cross-Layer Guidance

- Confirm DTOs and frontend expectations match new nullability and field semantics.
- If the schema rolls out in stages, keep API and UI tolerant of both old and new states during the compatibility window.

## Deliverables

Produce:
- updated entities and configurations
- migration design or migration files
- backfill notes where needed
- query and index considerations
- rollout and verification guidance
