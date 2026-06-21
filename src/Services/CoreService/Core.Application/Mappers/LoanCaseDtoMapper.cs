using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Identity.Entities;

namespace Core.Application.Mappers;

public sealed class LoanCaseDtoMapper(ICompanyDtoMapper companyDtoMapper) : ILoanCaseDtoMapper
{
    public LoanCaseDto MapCase(
        LoanCase entity,
        bool isInternalUser,
        Company? company,
        bool includeInstallments = false,
        bool includePayments = false,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null,
        IReadOnlyDictionary<string, UserDisplayDto>? userLookup = null)
    {
        var companyDto = companyDtoMapper.Map(company);

        IReadOnlyList<LoanInstallmentDto>? installments = includeInstallments
            ? entity.Installments.OrderBy(x => x.RowNumber).Select(MapInstallment).ToList()
            : null;

        IReadOnlyList<LoanPaymentDto>? payments = includePayments
            ? entity.Payments
                .OrderBy(x => x.StageNumber)
                .Select(p => MapPayment(
                    p,
                    userLookup is null ? null : userLookup.GetValueOrDefault(p.CreatedByUserId)?.FullName))
                .ToList()
            : null;

        if (isInternalUser)
        {
            return new LoanCaseInternalDto(
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
                entity.Application is null ? null : MapApplication(entity.Application),
                entity.ApprovalDetail is null ? null : MapApprovalDetail(entity.ApprovalDetail),
                installments,
                payments,
                fundCreditCapacity);
        }

        return new LoanCaseApplicantDto(
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
            entity.Application is null ? null : MapApplication(entity.Application),
            entity.ApprovalDetail is null ? null : MapApprovalDetail(entity.ApprovalDetail),
            installments,
            payments,
            fundCreditCapacity);
    }

    public LoanCaseDto MapFromDetailProjection(
        LoanCaseListProjection projection,
        bool isInternalUser,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        IReadOnlyList<LoanInstallmentListProjection>? installments = null,
        IReadOnlyList<LoanPaymentListProjection>? payments = null,
        IReadOnlyDictionary<string, UserDisplayDto>? userLookup = null)
    {
        var installmentDtos = installments?.Select(MapInstallment).ToList();
        var paymentDtos = payments?
            .Select(p => MapPayment(
                p,
                userLookup is null ? null : userLookup.GetValueOrDefault(p.CreatedByUserId)?.FullName))
            .ToList();

        if (isInternalUser)
        {
            var baseDto = (LoanCaseInternalDto)MapFromListProjection(projection, isInternalUser: true);
            return baseDto with
            {
                Installments = installmentDtos,
                Payments = paymentDtos,
                FundCreditCapacity = fundCreditCapacity
            };
        }

        var applicantDto = (LoanCaseApplicantDto)MapFromListProjection(projection, isInternalUser: false);
        return applicantDto with
        {
            Installments = installmentDtos,
            Payments = paymentDtos,
            FundCreditCapacity = fundCreditCapacity
        };
    }

    public LoanCaseDto MapFromListProjection(LoanCaseListProjection projection, bool isInternalUser)
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

        var application = projection.RequestedAmount is null
            && string.IsNullOrWhiteSpace(projection.FacilitySubject)
            ? null
            : new LoanApplicationDto(
                projection.RequestedAmount,
                projection.RequestedAmountInWords,
                projection.FacilitySubject,
                projection.OfferedGuarantees,
                projection.ApplicantCategory,
                projection.ApplicantCategoryOther,
                projection.RepresentativePosition);

        var approvalDetail = projection.ApprovedAmount is null
            && projection.FacilityType is null
            && projection.RepaymentMonths is null
            ? null
            : new LoanApprovalDetailDto(
                projection.DebtToAssetRatio,
                projection.CurrentRatio,
                projection.ProfitabilityRatioPercent,
                projection.CreditLimitWithCheck,
                projection.IsCreditLineActive,
                projection.RemainingCreditAfterGrant,
                projection.FacilityType,
                projection.ContractSubject,
                projection.BrokerageAndRelatedContract,
                projection.ApprovedAmount,
                projection.ApprovedAmountInWords,
                projection.RepaymentMonths,
                projection.GracePeriodMonths,
                projection.AnnualProfitRatePercent,
                projection.DailyPenaltyRatePercent,
                projection.CollateralDescription,
                projection.GuarantorsDescription,
                projection.OtherNotes,
                projection.ExpectedTotalProfit,
                projection.RepaymentCheckAmount);

        if (isInternalUser)
        {
            return new LoanCaseInternalDto(
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
                approvalDetail);
        }

        return new LoanCaseApplicantDto(
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
            approvalDetail);
    }

    public LoanApplicationDto MapApplication(LoanCaseApplication application) =>
        new(
            application.RequestedAmount,
            application.RequestedAmountInWords,
            application.FacilitySubject,
            application.OfferedGuarantees,
            application.ApplicantCategory,
            application.ApplicantCategoryOther,
            application.RepresentativePosition);

    public LoanApprovalDetailDto MapApprovalDetail(LoanApprovalDetail detail) =>
        new(
            detail.DebtToAssetRatio,
            detail.CurrentRatio,
            detail.ProfitabilityRatioPercent,
            detail.CreditLimitWithCheck,
            detail.IsCreditLineActive,
            detail.RemainingCreditAfterGrant,
            detail.FacilityType,
            detail.ContractSubject,
            detail.BrokerageAndRelatedContract,
            detail.ApprovedAmount,
            detail.ApprovedAmountInWords,
            detail.RepaymentMonths,
            detail.GracePeriodMonths,
            detail.AnnualProfitRatePercent,
            detail.DailyPenaltyRatePercent,
            detail.CollateralDescription,
            detail.GuarantorsDescription,
            detail.OtherNotes,
            detail.ExpectedTotalProfit,
            detail.RepaymentCheckAmount);

    public LoanCaseDocumentDto MapDocument(LoanCaseDocument document, string? uploadedByFullName = null) =>
        new(
            document.Id,
            document.DocumentType,
            document.FileName,
            document.MimeType,
            document.FileSize,
            document.Version,
            document.UploadedByUserId,
            uploadedByFullName,
            document.UploadedAt);

    public LoanInstallmentDto MapInstallment(LoanInstallmentListProjection installment) =>
        new(
            installment.Id,
            installment.RowNumber,
            installment.InstallmentDate,
            installment.PrincipalAmount,
            installment.ProfitAmount,
            installment.TotalAmount,
            installment.FundShareOfPrincipal,
            installment.FundShareOfProfit,
            installment.FundShareOfTotal,
            installment.IsGracePeriod,
            installment.IsPaid,
            installment.PaidAt);

    public LoanInstallmentDto MapInstallment(LoanInstallment installment) =>
        new(
            installment.Id,
            installment.RowNumber,
            installment.InstallmentDate,
            installment.PrincipalAmount,
            installment.ProfitAmount,
            installment.TotalAmount,
            installment.FundShareOfPrincipal,
            installment.FundShareOfProfit,
            installment.FundShareOfTotal,
            installment.IsGracePeriod,
            installment.IsPaid,
            installment.PaidAt);

    public LoanPaymentDto MapPayment(LoanPaymentListProjection payment, string? createdByFullName = null) =>
        new(
            payment.Id,
            payment.Amount,
            payment.PaymentDate,
            payment.TransactionNumber,
            payment.ReceiptS3Key,
            payment.Notes,
            payment.StageNumber,
            payment.CreatedByUserId,
            createdByFullName,
            payment.CreatedAt);

    public LoanPaymentDto MapPayment(LoanPayment payment, string? createdByFullName = null) =>
        new(
            payment.Id,
            payment.Amount,
            payment.PaymentDate,
            payment.TransactionNumber,
            payment.ReceiptS3Key,
            payment.Notes,
            payment.StageNumber,
            payment.CreatedByUserId,
            createdByFullName,
            payment.CreatedAt);

    public LoanWorkflowHistoryDto MapHistory(LoanCaseWorkflowHistory history, string? changedByFullName = null) =>
        new(
            history.Id,
            history.FromPhase,
            history.ToPhase,
            history.FromStatus,
            history.ToStatus,
            history.ChangedByUserId,
            changedByFullName,
            history.Action,
            history.ActorRole,
            history.CorrelationId,
            history.Comment,
            history.CreatedAt);

    public LoanWorkflowHistoryDto MapHistory(LoanWorkflowHistoryListProjection projection) =>
        new(
            projection.Id,
            projection.FromPhase,
            projection.ToPhase,
            projection.FromStatus,
            projection.ToStatus,
            projection.ChangedByUserId,
            projection.ChangedByFullName,
            projection.Action,
            projection.ActorRole,
            projection.CorrelationId,
            projection.Comment,
            projection.CreatedAt);

    public LoanCaseCommentDto MapComment(LoanCaseComment comment, string? senderFullName = null) =>
        new(
            comment.Id,
            comment.Phase,
            comment.SenderUserId,
            senderFullName,
            comment.SenderRole,
            comment.Message,
            comment.IsRevisionRequest,
            comment.IsInternal,
            comment.ParentId,
            comment.CreatedAt);

    public LoanCaseCommentDto MapComment(LoanCaseCommentListProjection projection) =>
        new(
            projection.Id,
            projection.Phase,
            projection.SenderUserId,
            projection.SenderFullName,
            projection.SenderRole,
            projection.Message,
            projection.IsRevisionRequest,
            projection.IsInternal,
            projection.ParentId,
            projection.CreatedAt);
}
