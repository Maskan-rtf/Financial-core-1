using Core.Domain.Enums;

namespace Core.Application.Requests;

public sealed record SaveCompanyRequest(
    string Name,
    string? EconomicCode,
    string? RegistrationNumber,
    string? NationalId,
    string? PhoneNumber,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    CompanyLegalType LegalType,
    CompanyTechnologyType TechnologyType,
    TechnologyDomain TechnologyDomain,
    string RepresentativeName,
    string RepresentativePhone,
    string CeoName,
    string CeoPhone);

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? EconomicCode,
    string? RegistrationNumber,
    string? NationalId,
    string? PhoneNumber,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    CompanyLegalType? LegalType = null,
    CompanyTechnologyType? TechnologyType = null,
    TechnologyDomain? TechnologyDomain = null,
    string? RepresentativeName = null,
    string? RepresentativePhone = null,
    string? CeoName = null,
    string? CeoPhone = null);

public sealed record CompanyAdminDto(
    Guid Id,
    string Name,
    string? EconomicCode,
    string? RegistrationNumber,
    string? NationalId,
    string? PhoneNumber,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    Guid OwnerUserId,
    string? OwnerFullName,
    string? OwnerPhoneNumber,
    CompanyLegalType? LegalType = null,
    CompanyTechnologyType? TechnologyType = null,
    TechnologyDomain? TechnologyDomain = null,
    string? RepresentativeName = null,
    string? RepresentativePhone = null,
    string? CeoName = null,
    string? CeoPhone = null);
