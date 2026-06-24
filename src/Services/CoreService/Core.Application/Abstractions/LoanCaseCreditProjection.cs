using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record LoanCaseCreditProjection(
    LoanCaseStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal? Amount);
