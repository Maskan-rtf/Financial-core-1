using Core.Application.Abstractions;
using Core.Application.Common;
using Core.Application.Logging;
using Core.Application.Requests;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
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
}

public sealed class CompanyAppService(
    ICoreUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    IMapper mapper,
    ILogger<CompanyAppService> logger) : ICompanyAppService
{
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

        if (!await CanManageAllCompaniesAsync(userId, cancellationToken))
        {
            ApplicationLog.Blocked(logger, "GetAllCompanies", "user lacks privilege", userId.ToString());
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
            c.OwnerUser?.PhoneNumber)).ToArray();

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

        var company = new Company
        {
            OwnerUserId = userId,
            Name = request.Name.Trim(),
            EconomicCode = request.EconomicCode.Trim(),
            RegistrationNumber = request.RegistrationNumber,
            NationalId = request.NationalId,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            City = request.City,
            Province = request.Province,
            PostalCode = request.PostalCode
        };

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

        company.Name = request.Name.Trim();
        company.EconomicCode = request.EconomicCode.Trim();
        company.RegistrationNumber = request.RegistrationNumber;
        company.NationalId = request.NationalId;
        company.PhoneNumber = request.PhoneNumber;
        company.Address = request.Address;
        company.City = request.City;
        company.Province = request.Province;
        company.PostalCode = request.PostalCode;

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

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    private async Task<bool> CanManageAllCompaniesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user is null) return false;
        return user.Role is UserRole.Admin or UserRole.Ceo or UserRole.TechnicalExpert;
    }

    private static string? FormatOwnerName(User? owner)
    {
        if (owner is null) return null;
        var name = $"{owner.FirstName} {owner.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
