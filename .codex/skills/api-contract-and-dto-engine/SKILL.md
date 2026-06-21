---
name: api-contract-and-dto-engine
description: Design or normalize REST API contracts, request and response DTOs, validation boundaries, and mapping strategy for ASP.NET Core backends and typed frontend consumers. Use when adding or reshaping endpoints or cleaning up inconsistent payloads.
---

# API Contract And DTO Engine

Start at the contract boundary. Shape operations so backend intent, validation, and frontend consumption remain explicit and stable.

## Workflow

1. Clarify the operation:
   - command, query, or mixed workflow
   - caller identity and permissions
   - validation and error semantics
   - paging, filtering, or sorting needs
2. Design the external contract first:
   - request DTO
   - response DTO
   - error envelope or failure shape
   - status codes
3. Keep DTOs independent from EF entities and UI component state.
4. Define mapping boundaries and validation ownership.
5. Update consumers to the new typed contract.

## Rules

- Separate write models from read models when the operation shapes differ.
- Keep request DTOs narrow; do not accept fields the caller should not control.
- Prefer response models that reflect use cases, not table layout.
- Version or stage breaking changes if existing consumers depend on the old contract.
- Make nullable and optional semantics explicit.

## Frontend Integration Guidance

- Generate or maintain accurate TypeScript types from the final DTO shape.
- Keep frontend adapters thin; do not recreate business semantics in the UI.
- Surface validation errors in forms and business errors in user-visible messages.

## Deliverables

Produce:
- endpoint design
- request and response DTOs
- validation and mapping guidance
- consumer update notes
- compatibility notes when changing existing contracts
