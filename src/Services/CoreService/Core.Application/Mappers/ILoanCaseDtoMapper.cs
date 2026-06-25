using Core.Application.Abstractions;
using Core.Application.DTOs;
using Core.Domain.Entities;
using Core.Domain.Identity.Entities;

namespace Core.Application.Mappers;

public interface ILoanCaseDtoMapper
{
    LoanCaseDto MapCase(
        LoanCase entity,
        bool isInternalUser,
        Company? company,
        bool includeInstallments = false,
        bool includePayments = false,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null,
        IReadOnlyDictionary<string, UserDisplayDto>? userLookup = null);
    LoanCaseDto MapFromListProjection(LoanCaseListProjection projection, bool isInternalUser);
    LoanCaseDto MapFromDetailProjection(
        LoanCaseListProjection projection,
        bool isInternalUser,
        FundCreditCapacitySnapshotDto? fundCreditCapacity = null,
        IReadOnlyList<LoanInstallmentListProjection>? installments = null,
        IReadOnlyList<LoanPaymentListProjection>? payments = null,
        IReadOnlyDictionary<string, UserDisplayDto>? userLookup = null);
    LoanInstallmentDto MapInstallment(LoanInstallmentListProjection installment);
    LoanPaymentDto MapPayment(LoanPaymentListProjection payment, string? createdByFullName = null);
    LoanApplicationDto MapApplication(LoanCaseApplication application);
    LoanApprovalDetailDto MapApprovalDetail(LoanApprovalDetail detail);
    LoanCaseDocumentDto MapDocument(LoanCaseDocument document, string? uploadedByFullName = null);
    LoanInstallmentDto MapInstallment(LoanInstallment installment);
    LoanPaymentDto MapPayment(LoanPayment payment, string? createdByFullName = null);
    LoanWorkflowHistoryDto MapHistory(LoanCaseWorkflowHistory history, string? changedByFullName = null);
    LoanWorkflowHistoryDto MapHistory(LoanWorkflowHistoryListProjection projection);
    LoanCaseCommentDto MapComment(
        LoanCaseComment comment,
        string? senderFullName = null,
        IReadOnlyList<LoanCaseWorkflowHistory>? workflowHistory = null);
    LoanCaseCommentDto MapComment(
        LoanCaseCommentListProjection projection,
        IReadOnlyList<LoanCaseWorkflowHistory>? workflowHistory = null);
}
