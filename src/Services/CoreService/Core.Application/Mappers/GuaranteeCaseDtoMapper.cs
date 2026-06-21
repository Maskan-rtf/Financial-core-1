using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Identity.Entities;

namespace Core.Application.Mappers;

public interface IGuaranteeCaseDtoMapper
{
    GuaranteeCaseDto MapCase(
        GuaranteeCase entity,
        bool isInternalView,
        Company? company = null,
        GuaranteeApplicantCreditSnapshotDto? applicantCreditSnapshot = null,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null);
    GuaranteeCaseDto MapFromListProjection(GuaranteeCaseListProjection projection, bool isInternalView);
    GuaranteeCaseDto MapFromDetailProjection(
        GuaranteeCaseDetailProjection projection,
        bool isInternalView,
        GuaranteeApplicantCreditSnapshotDto? applicantCreditSnapshot = null,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null);
    GuaranteeApplicationDto? MapApplication(GuaranteeCaseApplication? application);
    GuaranteeApprovalFormDto? MapApprovalForm(GuaranteeApprovalForm? form);
    GuaranteeCaseDocumentDto MapDocument(GuaranteeCaseDocument document);
    GuaranteeCaseCommentDto MapComment(GuaranteeCaseComment comment, string? senderFullName = null);
    GuaranteeCaseCommentDto MapComment(GuaranteeCaseCommentListProjection projection);
    GuaranteeWorkflowHistoryDto MapHistory(GuaranteeCaseWorkflowHistory history, string? changedByFullName = null);
    GuaranteeWorkflowHistoryDto MapHistory(GuaranteeWorkflowHistoryListProjection projection);
    GuaranteeRenewalDto MapRenewal(
        GuaranteeRenewalCase renewal,
        string? parentBeneficiaryName = null,
        string? parentCompanyName = null,
        string? applicantFullName = null);
    GuaranteeRenewalDto MapRenewal(GuaranteeRenewalCase renewal, GuaranteeRenewalContextProjection context);
    GuaranteeFundCreditLimitDto MapFundCreditLimit(
        GuaranteeApplicantCreditSnapshotDto snapshot,
        decimal creditLimitWithCheck,
        DateOnly periodStart,
        DateOnly expiresAt,
        string? lastSetByUserId,
        string? lastSetByFullName,
        DateTimeOffset? updatedAt);
}

public sealed class GuaranteeCaseDtoMapper(ICompanyDtoMapper companyDtoMapper) : IGuaranteeCaseDtoMapper
{
    public GuaranteeCaseDto MapCase(
        GuaranteeCase entity,
        bool isInternalView,
        Company? company = null,
        GuaranteeApplicantCreditSnapshotDto? applicantCreditSnapshot = null,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null)
    {
        var companyDto = companyDtoMapper.Map(company);

        if (isInternalView)
        {
            return new GuaranteeCaseInternalDto(
                entity.Id,
                entity.CaseNumber,
                entity.Title,
                entity.ApplicantUserId,
                applicantFullName,
                applicantPhoneNumber,
                entity.ApplicantType,
                entity.CurrentPhase,
                entity.CurrentStatus,
                entity.WorkflowInstanceId,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.CompletedAt,
                companyDto,
                MapApplication(entity.Application),
                MapApprovalForm(entity.ApprovalForm),
                applicantCreditSnapshot,
                fundCreditCapacity);
        }

        return new GuaranteeCaseApplicantDto(
            entity.Id,
            entity.CaseNumber,
            entity.Title,
            entity.ApplicantType,
            entity.CurrentPhase,
            entity.CurrentStatus,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CompletedAt,
            companyDto,
            MapApplication(entity.Application),
            MapApprovalForm(entity.ApprovalForm),
            applicantCreditSnapshot,
            fundCreditCapacity);
    }

    public GuaranteeApplicationDto? MapApplication(GuaranteeCaseApplication? application)
    {
        if (application is null) return null;

        return new GuaranteeApplicationDto(
            application.GuaranteeType,
            application.ContractSubject,
            application.IsKnowledgeBasedProduct,
            application.BeneficiaryName,
            application.BeneficiaryNationalId,
            application.BeneficiaryCompanyType,
            application.ApplicantCategory,
            application.ApplicantCategoryOther,
            application.ApplicantLegalForm,
            application.BaseContractNumber,
            application.BaseContractAmount,
            application.BaseContractAmountInWords,
            application.PriceAdjustmentRatePercent,
            application.ExecutionProvince,
            application.RequestedGuaranteeAmount,
            application.InitialValidityDays,
            application.ValidityFrom,
            application.ValidityTo,
            application.CollateralDescription,
            application.FacilitySubject);
    }

    public GuaranteeApprovalFormDto? MapApprovalForm(GuaranteeApprovalForm? form)
    {
        if (form is null) return null;

        return new GuaranteeApprovalFormDto(
            form.CreditLimitWithCheck,
            form.FundIssuedGuaranteesTotal,
            form.ActiveCommitments,
            form.RemainingCredit,
            form.GuaranteeType,
            form.GuaranteeAmount,
            form.GuaranteeAmountInWords,
            form.ContractSubject,
            form.Beneficiary,
            form.IssuanceDate,
            form.ExpiryDate,
            form.ActiveDurationDays,
            form.DepositRatePercent,
            form.DepositAmount,
            form.AnnualCommissionRatePercent,
            form.CommissionAmount,
            form.CollateralDescription,
            form.GuarantorsDescription,
            form.OtherNotes);
    }

    public GuaranteeCaseDto MapFromDetailProjection(
        GuaranteeCaseDetailProjection projection,
        bool isInternalView,
        GuaranteeApplicantCreditSnapshotDto? applicantCreditSnapshot = null,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null)
    {
        var companyDto = companyDtoMapper.MapFlat(
            projection.CompanyId,
            projection.CompanyName,
            projection.CompanyEconomicCode,
            projection.CompanyRegistrationNumber,
            projection.CompanyNationalId,
            projection.CompanyPhoneNumber,
            projection.CompanyAddress,
            projection.CompanyCity,
            projection.CompanyProvince,
            projection.CompanyPostalCode);

        var application = projection.GuaranteeType is null
            && projection.RequestedGuaranteeAmount is null
            && string.IsNullOrWhiteSpace(projection.BeneficiaryName)
            ? null
            : new GuaranteeApplicationDto(
                projection.GuaranteeType,
                projection.ContractSubject,
                projection.IsKnowledgeBasedProduct,
                projection.BeneficiaryName,
                projection.BeneficiaryNationalId,
                projection.BeneficiaryCompanyType,
                projection.ApplicantCategory,
                projection.ApplicantCategoryOther,
                projection.ApplicantLegalForm,
                projection.BaseContractNumber,
                projection.BaseContractAmount,
                projection.BaseContractAmountInWords,
                projection.PriceAdjustmentRatePercent,
                projection.ExecutionProvince,
                projection.RequestedGuaranteeAmount,
                projection.InitialValidityDays,
                projection.ValidityFrom,
                projection.ValidityTo,
                projection.CollateralDescription,
                projection.FacilitySubject);

        var approvalForm = projection.ApprovalGuaranteeAmount is null
            && projection.ApprovalGuaranteeType is null
            && string.IsNullOrWhiteSpace(projection.ApprovalBeneficiary)
            ? null
            : new GuaranteeApprovalFormDto(
                projection.ApprovalCreditLimitWithCheck,
                projection.ApprovalFundIssuedGuaranteesTotal,
                projection.ApprovalActiveCommitments,
                projection.ApprovalRemainingCredit,
                projection.ApprovalGuaranteeType,
                projection.ApprovalGuaranteeAmount,
                projection.ApprovalGuaranteeAmountInWords,
                projection.ApprovalContractSubject,
                projection.ApprovalBeneficiary,
                projection.ApprovalIssuanceDate,
                projection.ApprovalExpiryDate,
                projection.ApprovalActiveDurationDays,
                projection.ApprovalDepositRatePercent,
                projection.ApprovalDepositAmount,
                projection.ApprovalAnnualCommissionRatePercent,
                projection.ApprovalCommissionAmount,
                projection.ApprovalCollateralDescription,
                projection.ApprovalGuarantorsDescription,
                projection.ApprovalOtherNotes);

        if (isInternalView)
        {
            return new GuaranteeCaseInternalDto(
                projection.Id,
                projection.CaseNumber,
                projection.Title,
                projection.ApplicantUserId,
                projection.ApplicantFullName,
                projection.ApplicantPhoneNumber,
                projection.ApplicantType,
                projection.CurrentPhase,
                projection.CurrentStatus,
                projection.WorkflowInstanceId,
                projection.CreatedAt,
                projection.UpdatedAt,
                projection.CompletedAt,
                companyDto,
                application,
                approvalForm,
                applicantCreditSnapshot,
                fundCreditCapacity);
        }

        return new GuaranteeCaseApplicantDto(
            projection.Id,
            projection.CaseNumber,
            projection.Title,
            projection.ApplicantType,
            projection.CurrentPhase,
            projection.CurrentStatus,
            projection.CreatedAt,
            projection.UpdatedAt,
            projection.CompletedAt,
            companyDto,
            application,
            approvalForm,
            applicantCreditSnapshot,
            fundCreditCapacity);
    }

    public GuaranteeCaseDto MapFromListProjection(GuaranteeCaseListProjection projection, bool isInternalView)
    {
        var companyDto = companyDtoMapper.MapFlat(
            projection.CompanyId,
            projection.CompanyName,
            projection.CompanyEconomicCode,
            projection.CompanyRegistrationNumber,
            projection.CompanyNationalId,
            projection.CompanyPhoneNumber,
            projection.CompanyAddress,
            projection.CompanyCity,
            projection.CompanyProvince,
            projection.CompanyPostalCode);

        var application = projection.GuaranteeType is null
            && projection.RequestedGuaranteeAmount is null
            && string.IsNullOrWhiteSpace(projection.BeneficiaryName)
            ? null
            : new GuaranteeApplicationDto(
                projection.GuaranteeType,
                projection.ContractSubject,
                projection.IsKnowledgeBasedProduct,
                projection.BeneficiaryName,
                projection.BeneficiaryNationalId,
                projection.BeneficiaryCompanyType,
                projection.ApplicantCategory,
                projection.ApplicantCategoryOther,
                projection.ApplicantLegalForm,
                projection.BaseContractNumber,
                projection.BaseContractAmount,
                projection.BaseContractAmountInWords,
                projection.PriceAdjustmentRatePercent,
                projection.ExecutionProvince,
                projection.RequestedGuaranteeAmount,
                projection.InitialValidityDays,
                projection.ValidityFrom,
                projection.ValidityTo,
                projection.CollateralDescription,
                projection.FacilitySubject);

        if (isInternalView)
        {
            return new GuaranteeCaseInternalDto(
                projection.Id,
                projection.CaseNumber,
                projection.Title,
                projection.ApplicantUserId,
                projection.ApplicantFullName,
                projection.ApplicantPhoneNumber,
                projection.ApplicantType,
                projection.CurrentPhase,
                projection.CurrentStatus,
                projection.WorkflowInstanceId,
                projection.CreatedAt,
                projection.UpdatedAt,
                projection.CompletedAt,
                companyDto,
                application);
        }

        return new GuaranteeCaseApplicantDto(
            projection.Id,
            projection.CaseNumber,
            projection.Title,
            projection.ApplicantType,
            projection.CurrentPhase,
            projection.CurrentStatus,
            projection.CreatedAt,
            projection.UpdatedAt,
            projection.CompletedAt,
            companyDto,
            application);
    }

    public GuaranteeCaseDocumentDto MapDocument(GuaranteeCaseDocument document)
        => new(
            document.Id,
            document.DocumentType,
            document.FileName,
            document.MimeType,
            document.FileSize,
            document.Version,
            document.UploadedAt);

    public GuaranteeCaseCommentDto MapComment(GuaranteeCaseComment comment, string? senderFullName = null)
        => new(
            comment.Id,
            comment.Phase,
            comment.SenderUserId,
            senderFullName,
            comment.SenderRole,
            comment.Message,
            comment.IsRevisionRequest,
            comment.IsInternal,
            comment.CreatedAt);

    public GuaranteeCaseCommentDto MapComment(GuaranteeCaseCommentListProjection projection)
        => new(
            projection.Id,
            projection.Phase,
            projection.SenderUserId,
            projection.SenderFullName,
            projection.SenderRole,
            projection.Message,
            projection.IsRevisionRequest,
            projection.IsInternal,
            projection.CreatedAt);

    public GuaranteeWorkflowHistoryDto MapHistory(GuaranteeCaseWorkflowHistory history, string? changedByFullName = null)
        => new(
            history.Id,
            history.FromPhase,
            history.ToPhase,
            history.FromStatus,
            history.ToStatus,
            history.ChangedByUserId,
            changedByFullName,
            history.Action,
            history.ActorRole,
            history.Comment,
            history.CreatedAt);

    public GuaranteeWorkflowHistoryDto MapHistory(GuaranteeWorkflowHistoryListProjection projection)
        => new(
            projection.Id,
            projection.FromPhase,
            projection.ToPhase,
            projection.FromStatus,
            projection.ToStatus,
            projection.ChangedByUserId,
            projection.ChangedByFullName,
            projection.Action,
            projection.ActorRole,
            projection.Comment,
            projection.CreatedAt);

    public GuaranteeRenewalDto MapRenewal(
        GuaranteeRenewalCase renewal,
        string? parentBeneficiaryName = null,
        string? parentCompanyName = null,
        string? applicantFullName = null)
        => new(
            renewal.Id,
            renewal.CaseNumber,
            renewal.ParentGuaranteeCaseId,
            renewal.ParentGuaranteeCase.CaseNumber,
            parentBeneficiaryName,
            parentCompanyName,
            applicantFullName,
            renewal.RenewalKind,
            renewal.CurrentStatus,
            renewal.RequestedExpiryDate,
            renewal.RequestedAmount,
            renewal.ApprovedExpiryDate,
            renewal.CreatedAt,
            renewal.UpdatedAt,
            renewal.CompletedAt);

    public GuaranteeRenewalDto MapRenewal(GuaranteeRenewalCase renewal, GuaranteeRenewalContextProjection context)
        => MapRenewal(
            renewal,
            context.ParentBeneficiaryName,
            context.ParentCompanyName,
            context.ApplicantFullName);

    public GuaranteeFundCreditLimitDto MapFundCreditLimit(
        GuaranteeApplicantCreditSnapshotDto snapshot,
        decimal creditLimitWithCheck,
        DateOnly periodStart,
        DateOnly expiresAt,
        string? lastSetByUserId,
        string? lastSetByFullName,
        DateTimeOffset? updatedAt)
        => new(
            creditLimitWithCheck,
            periodStart,
            expiresAt,
            snapshot.FundIssuedGuaranteesTotal ?? 0m,
            snapshot.ActiveCommitments ?? 0m,
            snapshot.RemainingCredit,
            lastSetByUserId,
            lastSetByFullName,
            updatedAt);
}
