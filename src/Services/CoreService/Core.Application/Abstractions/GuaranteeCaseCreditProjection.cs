using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record GuaranteeCaseCreditProjection(
    GuaranteeCaseStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal? Amount,
    DateOnly? IssuanceDate);
