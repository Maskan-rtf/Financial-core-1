# Engineering Memory

Durable facts. Rules enforce invariants; skills run checks. Read when context needed — not duplicated in rules.

## Stack

.NET 9 · ASP.NET Core · EF Core · PostgreSQL · FluentValidation · Mapster · Serilog · Generic Repository · UnitOfWork · React/Next.js/Vue

## Solution

```
src/Services/CoreService/
  Core.API · Core.Application · Core.Domain · Core.Persistence · Core.Infrastructure
src/BuildingBlocks/
Frontend/          — non-prod API test panel
Maskan.Panel.sln
```

## Data access

```csharp
// Services: ICoreUnitOfWork only — never DbContext
await _unitOfWork.InvestmentCases.AddAsync(entity, ct);
await _unitOfWork.SaveChangesAsync(ct);
```

- UoW accessors: `_unitOfWork.Users`, `_unitOfWork.Orders`, `_unitOfWork.Products` (pattern for all entities)
- `EfRepository<T>` — reads default `AsNoTracking`
- Writes: explicit `SaveChangesAsync`; verify read-back (`/persistence`)

## Philosophy

| Prefer | Over |
|--------|------|
| Simplicity | Complexity |
| Maintainability | Cleverness |
| Correctness | Speed |
| Production-ready | Rapid stubs |

Advanced patterns (DDD, CQRS, MediatR, event sourcing, microservices) only with business + technical justification and rollback plan.

## Mandatory practices

- FluentValidation on every request DTO
- Mapster on every DTO boundary (mapped, computed, or explicit ignore)
- Structured Serilog + `BusinessProcess` / `BusinessStep` / correlation id
- Persistence chain verified on writes
- Security deny-by-default

## Definition of Done

Build · Run · `/validation` · `/mapping` · `/logging` · `/persistence` (writes) · `/security` · `/ef` · Review pass

## Commands

```bash
dotnet build && dotnet test
dotnet ef migrations add <Name> \
  --project src/Services/CoreService/Core.Persistence \
  --startup-project src/Services/CoreService/Core.API \
  --context CoreDbContext
```

## Index

| Skill | Command |
|-------|---------|
| validation.md | `/validation` |
| mapping.md | `/mapping` |
| logging.md | `/logging` |
| persistence.md | `/persistence` |
| security.md | `/security` |
| repository.md | `/repository` |
| ef.md | `/ef` |
| cqrs.md | `/cqrs` |
| refactor.md | `/refactor` |
