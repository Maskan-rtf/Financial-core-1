using Core.Application.Requests;
using Core.Domain.Identity.Entities;

namespace Core.Application.Mappers;

public sealed class CompanyDtoMapper : ICompanyDtoMapper
{
    public CompanyDto? Map(Company? company)
        => company is null
            ? null
            : new CompanyDto(
                company.Id,
                company.Name,
                company.EconomicCode,
                company.RegistrationNumber,
                company.NationalId,
                company.PhoneNumber,
                company.Address,
                company.City,
                company.Province,
                company.PostalCode,
                company.LegalType,
                company.TechnologyType,
                company.TechnologyDomain,
                company.RepresentativeName,
                company.RepresentativePhone,
                company.CeoName,
                company.CeoPhone,
                company.RegistrationDate,
                company.RegistrationPlace,
                company.InitialCapitalMillions,
                company.CurrentCapitalMillions,
                company.ActivitySubject,
                company.Website,
                company.CompanyEmail,
                !string.IsNullOrWhiteSpace(company.LogoS3Key));

    public CompanyDto? MapFlat(
        Guid? companyId,
        string? name,
        string? economicCode,
        string? registrationNumber,
        string? nationalId,
        string? phoneNumber,
        string? address,
        string? city,
        string? province,
        string? postalCode)
        => companyId is null
            ? null
            : new CompanyDto(
                companyId.Value,
                name ?? string.Empty,
                economicCode ?? string.Empty,
                registrationNumber,
                nationalId,
                phoneNumber,
                address,
                city,
                province,
                postalCode);
}
