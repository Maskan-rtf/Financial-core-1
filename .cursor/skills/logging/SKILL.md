---
name: logging
description: Serilog structured and business-process logging. Use with /logging.
disable-model-invocation: true
---

# /logging

## Purpose

Make every user-facing workflow reconstructable from logs via structured Serilog events and mandatory business-process logging.

## When to Use

- `/logging` invoked
- New/changed endpoint, service, or write path
- Auth/session flows
- Review gate: logging

## Responsibilities

1. Define `BusinessProcess` + `BusinessStep` per workflow
2. Emit at boundaries: request received · validation outcome · authz decision · persistence committed · response completed
3. Include `CorrelationId` (accept `X-Correlation-Id` from React/Next/Vue)
4. Log durations on I/O steps (`DurationMs`)
5. Log auth denials — never silent
6. After `SaveChangesAsync`: log entity id
7. Redact secrets — never log passwords, tokens, OTPs, full bodies

## Checklist

- [ ] Stable event names (`{Process}.{Step}`)
- [ ] Structured properties (not concat-only messages)
- [ ] Correlation flows HTTP → service → DB
- [ ] `ILogger<T>` / Serilog — no `Console.WriteLine`
- [ ] Failure paths logged with outcome

## Success Criteria

Full journey replayable by correlation id; no secret/PII leakage; operators can audit without debugger.

## Failure Conditions

**Fail** if: workflow lacks BusinessProcess events · only catch-block logs · credentials in logs · happy path invisible · missing persistence log after write

```csharp
_logger.LogInformation(
    "CreateX.Persisted {BusinessProcess} {BusinessStep} {EntityId} {DurationMs}",
    "CreateX", "SaveChanges", entity.Id, elapsed);
```
