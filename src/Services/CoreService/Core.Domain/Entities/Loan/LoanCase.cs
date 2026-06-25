using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Common;
using Core.Domain.Enums;
using Core.Domain.Identity.Entities;

namespace Core.Domain.Entities.Loan;

public sealed class LoanCase : AggregateRoot<Guid>, IAuditableEntity, ISoftDelete
{
    private LoanCase()
    {
        CaseNumber = default!;
        ApplicantUserId = default!;
    }

    public LoanCase(string caseNumber, string applicantUserId, ApplicantType applicantType)
    {
        Id = Guid.NewGuid();
        CaseNumber = caseNumber;
        ApplicantUserId = applicantUserId;
        ApplicantType = applicantType;
        CurrentPhase = LoanCasePhase.Application;
        CurrentStatus = LoanCaseStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string CaseNumber { get; private set; }
    public string? Title { get; private set; }
    public string ApplicantUserId { get; private set; }
    public ApplicantType ApplicantType { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Company? ApplicantCompany { get; private set; }

    public LoanCasePhase CurrentPhase { get; private set; }
    public LoanCaseStatus CurrentStatus { get; private set; }
    public string? WorkflowInstanceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public LoanCaseApplication? Application { get; private set; }
    public LoanApprovalDetail? ApprovalDetail { get; private set; }

    public List<LoanCaseDocument> Documents { get; private set; } = [];
    public List<LoanInstallment> Installments { get; private set; } = [];
    public List<LoanPayment> Payments { get; private set; } = [];
    public List<LoanCaseComment> Comments { get; private set; } = [];
    public List<LoanCaseWorkflowHistory> WorkflowHistory { get; private set; } = [];

    public void TransitionTo(
        LoanCaseStatus nextStatus,
        string changedByUserId,
        string? actorRole = null,
        LoanWorkflowAction? action = null,
        Guid? correlationId = null,
        string? comment = null)
    {
        if (CurrentStatus == nextStatus)
            return;

        var fromStatus = CurrentStatus;
        var fromPhase = CurrentPhase;

        CurrentStatus = nextStatus;
        CurrentPhase = DerivePhaseFromStatus(nextStatus);
        UpdatedAt = DateTimeOffset.UtcNow;

        if (nextStatus is LoanCaseStatus.Completed or LoanCaseStatus.Archived)
            CompletedAt = DateTimeOffset.UtcNow;

        WorkflowHistory.Add(new LoanCaseWorkflowHistory(
            Id,
            fromPhase,
            CurrentPhase,
            fromStatus,
            nextStatus,
            changedByUserId,
            action?.ToString() ?? string.Empty,
            actorRole ?? string.Empty,
            correlationId ?? Guid.NewGuid(),
            comment));
    }

    public void RollbackTo(
        LoanCaseStatus nextStatus,
        string changedByUserId,
        string actorRole,
        Guid correlationId,
        string? comment)
    {
        if (CurrentStatus is LoanCaseStatus.Completed or LoanCaseStatus.Archived or LoanCaseStatus.CanceledByCeo)
            CompletedAt = null;

        TransitionTo(nextStatus, changedByUserId, actorRole, LoanWorkflowAction.StageRollback, correlationId, comment);
    }

    public static LoanCasePhase DerivePhaseFromStatus(LoanCaseStatus status) => status switch
    {
        LoanCaseStatus.Draft or LoanCaseStatus.DataEntry or LoanCaseStatus.PendingCreditReview
            or LoanCaseStatus.RevisionRequestedByCredit => LoanCasePhase.Application,
        LoanCaseStatus.PendingCeoInitialApproval or LoanCaseStatus.CanceledByCeo => LoanCasePhase.CreditAssessment,
        LoanCaseStatus.PendingLegalRawContract or LoanCaseStatus.PendingApplicantSignature
            or LoanCaseStatus.PendingLegalFinalReview or LoanCaseStatus.RevisionRequestedByLegal
            or LoanCaseStatus.PendingLegalFinalContract => LoanCasePhase.Legal,
        LoanCaseStatus.PendingFinancialReview or LoanCaseStatus.RevisionRequestedByFinancial
            or LoanCaseStatus.PendingCeoFinalApproval or LoanCaseStatus.ReadyForPayment => LoanCasePhase.Finance,
        LoanCaseStatus.RepaymentPhase => LoanCasePhase.Repayment,
        LoanCaseStatus.Completed or LoanCaseStatus.Archived => LoanCasePhase.Closing,
        _ => LoanCasePhase.Application
    };

    public void AttachWorkflowInstance(string workflowInstanceId)
    {
        WorkflowInstanceId = workflowInstanceId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTitle(string? title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssignCompany(Guid companyId)
    {
        if (ApplicantType != ApplicantType.Company)
            throw new InvalidOperationException(DomainMessages.CompanyIdOnlyForCompanyApplicant);

        if (companyId == Guid.Empty)
            throw new ArgumentException("Company id is required.", nameof(companyId));

        CompanyId = companyId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public LoanCaseApplication UpsertApplication(
        decimal? requestedAmount,
        string? requestedAmountInWords,
        string? facilitySubject,
        string? offeredGuarantees,
        ApplicantCategory applicantCategory,
        string? applicantCategoryOther,
        string? representativePosition)
    {
        Application ??= new LoanCaseApplication(Id);

        Application.Update(
            requestedAmount,
            requestedAmountInWords,
            facilitySubject,
            offeredGuarantees,
            applicantCategory,
            applicantCategoryOther,
            representativePosition);

        UpdatedAt = DateTimeOffset.UtcNow;
        return Application;
    }

    public LoanApprovalDetail UpsertApprovalDetail(
        decimal? debtToAssetRatio,
        decimal? currentRatio,
        decimal? profitabilityRatioPercent,
        decimal? creditLimitWithCheck,
        bool? isCreditLineActive,
        decimal? remainingCreditAfterGrant,
        LoanFacilityType? facilityType,
        string? contractSubject,
        string? brokerageAndRelatedContract,
        decimal? approvedAmount,
        string? approvedAmountInWords,
        int? repaymentMonths,
        int? gracePeriodMonths,
        decimal? annualProfitRatePercent,
        decimal? dailyPenaltyRatePercent,
        string? collateralDescription,
        string? guarantorsDescription,
        string? otherNotes,
        decimal? expectedTotalProfit,
        decimal? repaymentCheckAmount)
    {
        ApprovalDetail ??= new LoanApprovalDetail(Id);

        ApprovalDetail.Update(
            debtToAssetRatio,
            currentRatio,
            profitabilityRatioPercent,
            creditLimitWithCheck,
            isCreditLineActive,
            remainingCreditAfterGrant,
            facilityType,
            contractSubject,
            brokerageAndRelatedContract,
            approvedAmount,
            approvedAmountInWords,
            repaymentMonths,
            gracePeriodMonths,
            annualProfitRatePercent,
            dailyPenaltyRatePercent,
            collateralDescription,
            guarantorsDescription,
            otherNotes,
            expectedTotalProfit,
            repaymentCheckAmount);

        UpdatedAt = DateTimeOffset.UtcNow;
        return ApprovalDetail;
    }

    public void RequestRevision(
        LoanCaseStatus targetStatus,
        string requestedByUserId,
        string? actorRole,
        LoanWorkflowAction? action,
        Guid? correlationId,
        string message,
        bool isInternal)
    {
        var fromStatus = CurrentStatus;
        var fromPhase = CurrentPhase;

        CurrentStatus = targetStatus;
        CurrentPhase = DerivePhaseFromStatus(targetStatus);
        UpdatedAt = DateTimeOffset.UtcNow;

        Comments.Add(new LoanCaseComment(
            caseId: Id,
            phase: fromPhase,
            senderUserId: requestedByUserId,
            senderRole: actorRole,
            message: message,
            isRevisionRequest: true,
            isInternal: isInternal,
            workflowStatusAtCreation: fromStatus));

        WorkflowHistory.Add(new LoanCaseWorkflowHistory(
            Id,
            fromPhase,
            CurrentPhase,
            fromStatus,
            targetStatus,
            requestedByUserId,
            action?.ToString() ?? string.Empty,
            actorRole ?? string.Empty,
            correlationId ?? Guid.NewGuid(),
            $"Revision requested: {message}"));
    }

    public void AddDiscussionComment(
        LoanCasePhase phase,
        string senderUserId,
        string? senderRole,
        string message,
        bool isRevisionRequest,
        bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Comments.Add(new LoanCaseComment(
            caseId: Id,
            phase: phase,
            senderUserId: senderUserId,
            senderRole: senderRole,
            message: message.Trim(),
            isRevisionRequest: isRevisionRequest,
            isInternal: isInternal,
            workflowStatusAtCreation: CurrentStatus));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public LoanCaseDocument AddDocument(
        string s3Key,
        string fileName,
        string mimeType,
        long fileSize,
        int version,
        LoanDocumentType documentType,
        string uploadedByUserId)
    {
        var doc = new LoanCaseDocument(
            caseId: Id,
            s3Key: s3Key,
            fileName: fileName,
            mimeType: mimeType,
            fileSize: fileSize,
            version: version,
            documentType: documentType,
            uploadedByUserId: uploadedByUserId,
            uploadedAt: DateTimeOffset.UtcNow);

        Documents.Add(doc);
        return doc;
    }

    public void ReplaceInstallments(IReadOnlyList<LoanInstallment> installments)
    {
        Installments.Clear();
        foreach (var installment in installments)
            Installments.Add(installment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public LoanPayment AddPayment(
        decimal amount,
        DateOnly paymentDate,
        string transactionNumber,
        string? receiptS3Key,
        string? notes,
        int stageNumber,
        string createdByUserId)
    {
        var payment = new LoanPayment(
            Id,
            amount,
            paymentDate,
            transactionNumber,
            receiptS3Key,
            notes,
            stageNumber,
            createdByUserId);

        Payments.Add(payment);
        UpdatedAt = DateTimeOffset.UtcNow;
        return payment;
    }
}
