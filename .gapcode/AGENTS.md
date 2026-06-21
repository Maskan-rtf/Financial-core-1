# Repository Guidelines

This document provides essential guidance for contributors working on the Financial-Core backend. It complements the comprehensive Persian developer guide at `docs/backend/BACKEND_DEVELOPER_GUIDE.md`.

## Project Structure & Module Organization

This is a .NET 9.0 solution following Clean Architecture principles:

```
src/
├── BuildingBlocks/              # Shared infrastructure libraries
│   ├── BuildingBlocks.Domain
│   ├── BuildingBlocks.Application
│   ├── BuildingBlocks.Contracts
│   ├── BuildingBlocks.Persistence
│   ├── BuildingBlocks.Infrastructure
│   └── BuildingBlocks.Observability
└── Services/CoreService/        # Main financial service
    ├── Core.Domain             # Entities, enums, domain events
    ├── Core.Application        # Use cases, DTOs, validators
    ├── Core.Contracts          # Public contracts
    ├── Core.Persistence        # EF Core, configurations, migrations
    ├── Core.Infrastructure     # Repositories, external integrations
    ├── Core.Workflow           # Elsa workflow definitions
    └── Core.API                # HTTP controllers, middleware, auth
```

**Business modules:** Investment cases, Guarantees, Loans, Dashboards/KPI, Fund credit limits, Identity/OTP.

## Build, Test, and Development Commands

**Build the solution:**
```bash
dotnet build Maskan.Panel.sln
```

**Run the API locally:**
```bash
dotnet run --project src/Services/CoreService/Core.API/Core.API.csproj
```

**Create a new EF Core migration:**
```bash
dotnet ef migrations add MigrationName --project src/Services/CoreService/Core.Persistence --startup-project src/Services/CoreService/Core.API
```

**Apply migrations:**
```bash
dotnet ef database update --project src/Services/CoreService/Core.Persistence --startup-project src/Services/CoreService/Core.API
```

**Note:** This repository currently has no automated test projects.

## Coding Style & Naming Conventions

**Language & Framework:** .NET 9.0 with C# preview features enabled.

**Project settings (Directory.Build.props):**
- Nullable reference types: enabled
- Implicit usings: enabled
- Treat warnings as errors: true
- XML documentation generation: enabled (suppress 1591 warnings)

**File naming:**
- Entity: `EntityName.cs`
- Repository: `IEntityRepository.cs` and `EntityRepository.cs`
- Controller: `EntityController.cs`
- DTO: `EntityDto.cs`, `CreateEntityRequest.cs`
- Validator: `CreateEntityRequestValidator.cs`

**Naming patterns:**
- Use PascalCase for classes, methods, properties
- Use camelCase for local variables and parameters
- Suffix interfaces with `I` prefix
- Async methods should end with `Async`

**Architecture rules:**
- Domain layer has no external dependencies
- Application layer depends only on Domain
- Infrastructure/Persistence implement Application contracts
- API layer coordinates and handles HTTP concerns

## Database & Migrations

**Database:** PostgreSQL with schemas `Identity` and `Cases`.

**Entity Framework Core** configurations live in `Core.Persistence/Configurations/`. Each entity should have:
- A corresponding `IEntityTypeConfiguration<T>` class
- Proper indexes, foreign keys, and constraints
- Soft-delete filters where applicable

**After adding a new entity:**
1. Create entity in `Core.Domain/Entities/`
2. Add configuration in `Core.Persistence/Configurations/`
3. Register `DbSet<T>` in `CoreDbContext`
4. Create and apply migration
5. Add repository interface in `Core.Application/`
6. Implement repository in `Core.Infrastructure/`

## Commit & Pull Request Guidelines

**Commit message format** (based on recent history):

```
<type>(<scope>): <description>

Types: feat, fix, chore, docs, refactor
Scopes: module names (e.g., guarantee, loan, investment, dashboard, auth)
```

**Examples:**
- `feat(fund-credit): add paginated period limit listing`
- `fix(persistence): extend soft-delete filters to loan subgraph`
- `chore(config): enable employee KPI dashboard aggregation`
- `docs: update developer guides and add cursor agent skills`

**Pull requests:**
- Keep commits atomic and focused
- Reference related issue numbers if applicable
- Ensure the solution builds without warnings
- Update documentation when changing public APIs or architecture

## Authorization & Permissions

**Multi-layer authorization system:**
1. **Policy-based** (`[Authorize(Policy = "...")]`) in controllers
2. **Permission-based** checks in application services
3. **Resource-level** (e.g., case ownership, company membership)
4. **Workflow state** validation

**Permission changes:**
- Permissions defined in `Core.Domain/Authorization/`
- Role mappings in `Core.Infrastructure/Identity/`
- Policies registered in `Core.API/DependencyInjection/AuthorizationConfiguration.cs`
- **Important:** Permission cache (Redis) must be invalidated after role changes

**Reference:** See sections 9-13 in `docs/backend/BACKEND_DEVELOPER_GUIDE.md` for complete authorization workflow.

## Development Workflow Notes

**Configuration:** Settings in `Core.API/appsettings.json` and `appsettings.Development.json`.

**Logging:** Uses Serilog with structured logging. Correlation IDs tracked per request.

**Validation:** FluentValidation with Persian localization. Validators in `Core.Application/Validators/`.

**Mapping:** Uses Mapster for DTO mapping. Configurations in `Core.Application/Mapping/`.

**External integrations:**
- S3-compatible storage for documents
- Redis for caching and sessions
- Kafka for event publishing
- SMS gateway integration

**Workflow engine:** Elsa workflows for multi-step case processes (investment, guarantee, loan approval flows).

For detailed architectural patterns, workflow implementation, and complete file references, see `docs/backend/BACKEND_DEVELOPER_GUIDE.md` (Persian).
