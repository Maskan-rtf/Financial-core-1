using System.IO;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using BuildingBlocks.Domain.Abstractions;
using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Logging;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Domain.Identity;
using Core.Domain.Identity.Entities;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Core.Application.Services;

public interface ICompanyAppService
{
    Task<Result<IReadOnlyList<CompanyDto>>> GetMyCompaniesAsync(CancellationToken cancellationToken);
    Task<Result<PagedResult<CompanyAdminDto>>> GetAllCompaniesAsync(int take, int skip, CancellationToken cancellationToken);
    Task<Result<CompanyDto>> CreateAsync(SaveCompanyRequest request, CancellationToken cancellationToken);
    Task<Result<CompanyDto>> UpdateAsync(Guid companyId, SaveCompanyRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Result<PresignUploadResponse>> PresignLogoUploadAsync(Guid companyId, PresignCompanyLogoRequest request, CancellationToken cancellationToken);
    Task<Result<CompanyDto>> ConfirmLogoUploadAsync(Guid companyId, ConfirmCompanyLogoRequest request, CancellationToken cancellationToken);
    Task<Result<PresignDownloadResponse>> PresignLogoDownloadAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Result<DocumentDownloadFileResult>> DownloadLogoFileAsync(Guid companyId, CancellationToken cancellationToken);
}

public sealed class CompanyAppService(
    ICoreUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    IUserContext userContext,
    IDocumentStorage documentStorage,
    IMapper mapper,
    ILogger<CompanyAppService> logger) : ICompanyAppService
{
    #region Public API

    public async Task<Result<IReadOnlyList<CompanyDto>>> GetMyCompaniesAsync(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            ApplicationLog.Blocked(logger, "GetMyCompanies", "user is not authenticated");
            return Result<IReadOnlyList<CompanyDto>>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "GetMyCompanies", userId.ToString());

        var companies = await unitOfWork.Companies.GetOwnedByUserAsync(userId, cancellationToken);
        var list = companies.Select(c => mapper.Map<CompanyDto>(c)).ToArray();

        ApplicationLog.Completed(logger,
            "User {UserId} loaded {Count} owned company(ies)",
            userId, list.Length);

        return Result<IReadOnlyList<CompanyDto>>.Ok(list);
    }

    public async Task<Result<PagedResult<CompanyAdminDto>>> GetAllCompaniesAsync(int take, int skip, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            ApplicationLog.Blocked(logger, "GetAllCompanies", "user is not authenticated");
            return Result<PagedResult<CompanyAdminDto>>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        if (!await CanListAllCompaniesAsync(userId, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "GetAllCompanies", "applicant cannot list all companies", userId.ToString());
            return Result<PagedResult<CompanyAdminDto>>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));
        }

        ApplicationLog.Started(logger, "GetAllCompanies", userId.ToString());

        var pageSize = Math.Clamp(take, 1, 200);
        var normalizedSkip = Math.Max(0, skip);
        var companies = await unitOfWork.Companies.GetPagedListAsync(pageSize, normalizedSkip, cancellationToken);
        var total = await unitOfWork.Companies.CountAllAsync(cancellationToken);

        var items = companies.Select(c => new CompanyAdminDto(
            c.Id,
            c.Name,
            c.EconomicCode,
            c.RegistrationNumber,
            c.NationalId,
            c.PhoneNumber,
            c.Address,
            c.City,
            c.Province,
            c.PostalCode,
            c.OwnerUserId,
            FormatOwnerName(c.OwnerUser),
            c.OwnerUser?.PhoneNumber,
            c.LegalType,
            c.TechnologyType,
            c.TechnologyDomain,
            c.RepresentativeName,
            c.RepresentativePhone,
            c.CeoName,
            c.CeoPhone,
            c.RegistrationDate,
            c.RegistrationPlace,
            c.InitialCapitalMillions,
            c.CurrentCapitalMillions,
            c.ActivitySubject,
            c.Website,
            c.CompanyEmail,
            !string.IsNullOrWhiteSpace(c.LogoS3Key))).ToArray();

        ApplicationLog.Completed(logger,
            "User {UserId} listed {Count} of {Total} companies",
            userId, items.Length, total);

        return Result<PagedResult<CompanyAdminDto>>.Ok(new PagedResult<CompanyAdminDto>(items, normalizedSkip, pageSize, total));
    }

    public async Task<Result<CompanyDto>> CreateAsync(SaveCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            ApplicationLog.Blocked(logger, "CreateCompany", "user is not authenticated");
            return Result<CompanyDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "CreateCompany", userId.ToString());

        var company = new Company { OwnerUserId = userId };
        ApplyProfile(company, request);

        await unitOfWork.Companies.AddAsync(company, cancellationToken);

        var user = await unitOfWork.Users.GetByIdAsync(userId, disableTracking: false);
        if (user is not null && user.CompanyId is null)
        {
            user.CompanyId = company.Id;
            await unitOfWork.Users.UpdateAsync(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} created company {CompanyId} ({CompanyName})",
            userId, company.Id, company.Name);

        return Result<CompanyDto>.Ok(mapper.Map<CompanyDto>(company));
    }

    public async Task<Result<CompanyDto>> UpdateAsync(Guid companyId, SaveCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            ApplicationLog.Blocked(logger, "UpdateCompany", "user is not authenticated");
            return Result<CompanyDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "UpdateCompany", userId.ToString());

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: false, cancellationToken);
        if (company is null)
        {
            ApplicationLog.Blocked(logger, "UpdateCompany", "company not found", userId.ToString());
            return Result<CompanyDto>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));
        }

        if (company.OwnerUserId != userId && !await CanManageAllCompaniesAsync(userId, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "UpdateCompany", "user is not the owner", userId.ToString());
            return Result<CompanyDto>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));
        }

        ApplyProfile(company, request);

        unitOfWork.Companies.Update(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} updated company {CompanyId} ({CompanyName})",
            userId, companyId, company.Name);

        return Result<CompanyDto>.Ok(mapper.Map<CompanyDto>(company));
    }

    public async Task<Result> DeleteAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            ApplicationLog.Blocked(logger, "DeleteCompany", "user is not authenticated");
            return Result.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));
        }

        ApplicationLog.Started(logger, "DeleteCompany", userId.ToString());

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: false, cancellationToken);
        if (company is null)
        {
            ApplicationLog.Blocked(logger, "DeleteCompany", "company not found", userId.ToString());
            return Result.Fail(Error.NotFound(ApiMessages.CompanyNotFound));
        }

        if (await unitOfWork.Companies.HasLinkedCasesAsync(companyId, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "DeleteCompany", "company has linked cases", userId.ToString());
            return Result.Fail(Error.Conflict(ApiMessages.CompanyHasLinkedCases));
        }

        company.IsDeleted = true;
        company.UpdateDate = DateTime.UtcNow;
        unitOfWork.Companies.Update(company);

        var linkedUsers = await unitOfWork.Users.GetAllAsync(u => u.CompanyId == companyId, disableTracking: false);
        foreach (var linkedUser in linkedUsers)
        {
            linkedUser.CompanyId = null;
            await unitOfWork.Users.UpdateAsync(linkedUser);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplicationLog.Completed(logger,
            "User {UserId} deleted company {CompanyId} ({CompanyName}) and cleared {UserCount} linked user(s)",
            userId, companyId, company.Name, linkedUsers.Count);

        return Result.Ok();
    }

    public async Task<Result<PresignUploadResponse>> PresignLogoUploadAsync(
        Guid companyId,
        PresignCompanyLogoRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Result<PresignUploadResponse>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: true, cancellationToken);
        if (company is null)
            return Result<PresignUploadResponse>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

        if (company.OwnerUserId != userId && !await CanManageAllCompaniesAsync(userId, cancellationToken))
            return Result<PresignUploadResponse>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

        var ext = GetSafeImageExtension(request.FileName, request.MimeType);
        if (ext is null)
            return Result<PresignUploadResponse>.Fail(Error.Validation(ApiMessages.FileTypeNotAllowed));

        var s3Key = $"companies/{companyId}/logo/{Guid.NewGuid():N}{ext}";
        var (url, expiresAt) = await documentStorage.PresignUploadAsync(s3Key, request.MimeType, TimeSpan.FromMinutes(15), cancellationToken);

        return Result<PresignUploadResponse>.Ok(new PresignUploadResponse(s3Key, url, expiresAt, 1));
    }

    public async Task<Result<CompanyDto>> ConfirmLogoUploadAsync(
        Guid companyId,
        ConfirmCompanyLogoRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Result<CompanyDto>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        if (string.IsNullOrWhiteSpace(request.S3Key))
            return Result<CompanyDto>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: false, cancellationToken);
        if (company is null)
            return Result<CompanyDto>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

        if (company.OwnerUserId != userId && !await CanManageAllCompaniesAsync(userId, cancellationToken))
            return Result<CompanyDto>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

        var expectedPrefix = $"companies/{companyId}/logo/";
        if (!request.S3Key.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return Result<CompanyDto>.Fail(Error.Validation(ApiMessages.InvalidDocumentKey));

        if (!await documentStorage.ExistsAsync(request.S3Key, cancellationToken))
            return Result<CompanyDto>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        company.LogoS3Key = request.S3Key.Trim();
        unitOfWork.Companies.Update(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CompanyDto>.Ok(mapper.Map<CompanyDto>(company));
    }

    public async Task<Result<PresignDownloadResponse>> PresignLogoDownloadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Result<PresignDownloadResponse>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: true, cancellationToken);
        if (company is null)
            return Result<PresignDownloadResponse>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

        if (company.OwnerUserId != userId && !await CanManageAllCompaniesAsync(userId, cancellationToken))
            return Result<PresignDownloadResponse>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

        if (string.IsNullOrWhiteSpace(company.LogoS3Key))
            return Result<PresignDownloadResponse>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        var (url, expiresAt) = await documentStorage.PresignDownloadAsync(company.LogoS3Key, TimeSpan.FromMinutes(10), cancellationToken);
        var fileName = Path.GetFileName(company.LogoS3Key);
        return Result<PresignDownloadResponse>.Ok(new PresignDownloadResponse(url, expiresAt, fileName));
    }

    public async Task<Result<DocumentDownloadFileResult>> DownloadLogoFileAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Result<DocumentDownloadFileResult>.Fail(Error.Unauthorized(ApiMessages.AuthenticationRequired));

        var company = await unitOfWork.Companies.GetByIdAsync(companyId, asNoTracking: true, cancellationToken);
        if (company is null)
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.CompanyNotFound));

        if (company.OwnerUserId != userId && !await CanManageAllCompaniesAsync(userId, cancellationToken))
            return Result<DocumentDownloadFileResult>.Fail(Error.Forbidden(ApiMessages.CompanyAccessDenied));

        if (string.IsNullOrWhiteSpace(company.LogoS3Key))
            return Result<DocumentDownloadFileResult>.Fail(Error.NotFound(ApiMessages.DocumentNotFound));

        var metadata = await documentStorage.GetMetadataAsync(company.LogoS3Key, cancellationToken);
        var contentType = metadata?.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/octet-stream";

        var stream = await documentStorage.OpenReadAsync(company.LogoS3Key, cancellationToken);
        var fileName = Path.GetFileName(company.LogoS3Key);
        return Result<DocumentDownloadFileResult>.Ok(new DocumentDownloadFileResult(stream, contentType, fileName));
    }

    #endregion

    #region Private

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    private async Task<bool> CanListAllCompaniesAsync(Guid userId, CancellationToken _)
    {
        if (HasNonApplicantRole())
            return true;

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        return user is not null && user.Role != UserRole.Applicant;
    }

    private bool HasNonApplicantRole()
    {
        foreach (var role in userContext.Roles)
        {
            var normalized = UserRoleClaims.Normalize(role);
            if (normalized.Equals("CEO", StringComparison.OrdinalIgnoreCase))
                return true;
            if (UserRoleClaims.TryParse(normalized, out var parsed) && parsed != UserRole.Applicant)
                return true;
        }

        return false;
    }

    private async Task<bool> CanManageAllCompaniesAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (HasCompanyListAllRole())
            return true;

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user is null) return false;
        return user.Role is UserRole.Admin or UserRole.Ceo or UserRole.TechnicalExpert or UserRole.TechnicalManager or UserRole.InvestmentManager;
    }

    private bool HasCompanyListAllRole()
    {
        foreach (var role in userContext.Roles)
        {
            var normalized = UserRoleClaims.Normalize(role);
            if (normalized.Equals(UserRoleClaims.Admin, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(UserRoleClaims.Ceo, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("CEO", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(UserRoleClaims.TechnicalExpert, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(UserRoleClaims.TechnicalManager, StringComparison.OrdinalIgnoreCase)
                || normalized.Equals(UserRoleClaims.InvestmentManager, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyProfile(Company company, SaveCompanyRequest request)
    {
        company.Name = request.Name.Trim();
        company.EconomicCode = string.IsNullOrWhiteSpace(request.EconomicCode) ? string.Empty : request.EconomicCode.Trim();
        company.RegistrationNumber = request.RegistrationNumber?.Trim();
        company.NationalId = request.NationalId?.Trim();
        company.PhoneNumber = request.PhoneNumber?.Trim();
        company.Address = request.Address?.Trim();
        company.City = request.City?.Trim();
        company.Province = request.Province?.Trim();
        company.PostalCode = request.PostalCode?.Trim();
        company.LegalType = request.LegalType;
        company.TechnologyType = request.TechnologyType;
        company.TechnologyDomain = request.TechnologyDomain;
        company.RepresentativeName = request.RepresentativeName.Trim();
        company.RepresentativePhone = request.RepresentativePhone.Trim();
        company.CeoName = request.CeoName.Trim();
        company.CeoPhone = request.CeoPhone.Trim();
        company.RegistrationDate = request.RegistrationDate;
        company.RegistrationPlace = string.IsNullOrWhiteSpace(request.RegistrationPlace) ? null : request.RegistrationPlace.Trim();
        company.InitialCapitalMillions = request.InitialCapitalMillions;
        company.CurrentCapitalMillions = request.CurrentCapitalMillions;
        company.ActivitySubject = string.IsNullOrWhiteSpace(request.ActivitySubject) ? null : request.ActivitySubject.Trim();
        company.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        company.CompanyEmail = string.IsNullOrWhiteSpace(request.CompanyEmail) ? null : request.CompanyEmail.Trim();
    }

    private static string? GetSafeImageExtension(string fileName, string mimeType)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => null
            };
        }

        if (ext is null) return null;

        ext = ext.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" ? ext : null;
    }

    private static string? FormatOwnerName(User? owner)
    {
        if (owner is null) return null;
        var name = $"{owner.FirstName} {owner.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    #endregion
}
