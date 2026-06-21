using Core.Domain.Enums;

namespace Core.Application.Requests;

public sealed record CaseSearchRequest(
    string? CaseNumber,
    string? ApplicantUserId,
    CasePhase? Phase,
    CaseStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Skip = 0,
    int Take = 10);
