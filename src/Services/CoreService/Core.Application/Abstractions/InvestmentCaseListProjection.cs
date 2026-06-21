using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public sealed record InvestmentCaseListProjection(
    Guid Id,
    string CaseNumber,
    string? Title,
    string ApplicantUserId,
    ApplicantType ApplicantType,
    CasePhase CurrentPhase,
    CaseStatus CurrentStatus,
    string? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    Guid? CompanyId,
    string? CompanyName,
    string? CompanyEconomicCode,
    string? CompanyRegistrationNumber,
    string? CompanyNationalId,
    string? CompanyPhoneNumber,
    string? CompanyAddress,
    string? CompanyCity,
    string? CompanyProvince,
    string? CompanyPostalCode,
    string? ApplicantFullName,
    string? ApplicantPhoneNumber,
    string? RepresentativeFullName,
    BusinessStage? BusinessStage,
    string? ContactEmail,
    decimal? RequestedAmount,
    string? InvestmentAttractionBasis);
