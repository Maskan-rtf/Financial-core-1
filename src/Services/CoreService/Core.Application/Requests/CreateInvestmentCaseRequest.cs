using Core.Domain.Enums;

namespace Core.Application.Requests;

public sealed record CreateInvestmentCaseRequest(ApplicantType ApplicantType, Guid? CompanyId);