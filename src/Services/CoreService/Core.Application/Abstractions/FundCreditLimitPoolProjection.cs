using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record FundCreditLimitPoolProjection(
    Guid Id,
    FundModuleType ModuleType,
    decimal CreditLimitWithCheck,
    DateOnly PeriodStart,
    DateOnly ExpiresAt);
