---
name: validation
description: FluentValidation for request DTOs. Use with /validation or new/changed request DTOs.
disable-model-invocation: true
---

# /validation

## Purpose

Protect the API boundary with FluentValidation — one validator per request DTO before service or database access.

## When to Use

- `/validation` invoked
- New or changed `*Request` DTO
- New business rule enforceable at input boundary
- Review gate: validation

## Responsibilities

1. Create `{Request}Validator : AbstractValidator<{Request}>`
2. Rule every public property (or document justified omission)
3. Validate nested types: `SetValidator` / `RuleForEach`
4. Register in DI; ensure pipeline runs **before** service
5. Return 400/problem-details — no stack traces
6. Add tests: required fields + primary business rule + edge case

## Checklist

- [ ] Validator exists per touched request DTO
- [ ] Nested objects/collections covered
- [ ] Async DB rules use `MustAsync` + `CancellationToken`
- [ ] Messages non-leaky (no internal existence hints to unauthorized callers)
- [ ] Validated fields align with `/mapping` persistence intent

## Success Criteria

All request DTOs in scope have validators; invalid input never reaches `SaveChangesAsync`; tests pass.

## Failure Conditions

**Fail** if: missing validator · property without rule · validation after service · data annotations only · no tests on critical rules

```csharp
public sealed class CreateXRequestValidator : AbstractValidator<CreateXRequest>
{
    public CreateXRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Items).SetValidator(new ItemRequestValidator());
    }
}
```
