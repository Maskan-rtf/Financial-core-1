---
name: test-suite-expander
description: Add high-value unit, integration, and end-to-end tests for production code changes. Use when shipping features, fixing regressions, or increasing confidence before refactors in ASP.NET Core and React or Next.js systems.
---

# Test Suite Expander

Add tests where they buy confidence, not where they only add noise. Match each assertion to the cheapest layer that can prove the behavior.

## Workflow

1. Identify the risk surface:
   - core business logic
   - API contract behavior
   - persistence behavior
   - user-critical UI flows
2. Place tests deliberately:
   - unit tests for deterministic business rules
   - integration tests for API, database, auth, and serialization boundaries
   - end-to-end tests for critical user journeys
3. Prefer a few high-signal scenarios over broad duplicated coverage.
4. After bug fixes, reproduce the prior failure in at least one test.

## Rules

- Do not mock what you can cheaply exercise for real in an integration test.
- Do not use end-to-end tests to cover basic pure logic.
- Keep test data intentional and minimal.
- Assert externally visible behavior before internal implementation details.

## Cross-Stack Guidance

- For frontend flows, combine typed API assumptions with realistic UI interaction states.
- For backend changes, include validation, auth, and persistence checks when those boundaries matter to the failure mode.

## Deliverables

Produce:
- added or updated tests
- rationale for test placement
- remaining gaps if full coverage is impractical in the current environment
