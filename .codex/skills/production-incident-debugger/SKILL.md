---
name: production-incident-debugger
description: Diagnose production issues from stack traces, structured logs, runtime errors, request context, and recent changes. Use when the main task is root-cause analysis, repro construction, or a safe production fix.
---

# Production Incident Debugger

Start from evidence, not intuition. Reconstruct the failing path, rank plausible causes, and produce the smallest safe fix.

## Workflow

1. Gather the strongest signals first:
   - exception type and stack trace
   - correlation or trace IDs
   - endpoint, route, or job name
   - timestamps
   - affected tenant, user, or entity IDs
   - recent code or config changes
2. Identify where the failure originates:
   - frontend rendering or interaction
   - API boundary
   - application logic
   - data access
   - infrastructure or configuration
3. Reconstruct the exact code path and required preconditions.
4. Separate proven facts from hypotheses.
5. If the root cause is still ambiguous, add the minimum instrumentation needed to disambiguate it.
6. Patch the narrowest cause, then add a regression test or deterministic repro.

## Investigation Rules

- Trust logs and code paths more than verbal summaries.
- Explain why each hypothesis fits or does not fit the evidence.
- Look for nullability mismatches, stale assumptions in mappings, auth context gaps, race conditions, and environment-specific configuration.
- If the issue is intermittent, focus on state transitions, concurrency, retries, and partial failure handling.

## Cross-Layer Guidance

- Correlate frontend error states with API status codes and payloads.
- Trace API failures through application services, EF queries, and transaction boundaries.
- Check whether production data shape violates assumptions that test data never exercised.

## Deliverables

Produce:
- ranked root-cause hypotheses
- failing path and trigger conditions
- fix recommendation or code patch
- added diagnostics if evidence is insufficient
- regression test or repro notes

## Refuse The Wrong Shortcut

Do not claim certainty when the evidence is partial. Do not broaden the fix before the failure mode is understood.
