using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Common;
using Core.Domain.Enums;
using Core.Domain.Identity.Entities;

namespace Core.Domain.Entities.Guarantee;

public sealed class GuaranteeCase : AggregateRoot<Guid>, IAuditableEntity, ISoftDelete
{
    private GuaranteeCase()
    {
        CaseNumber = default!;
        ApplicantUserId = default!;
    }

    public GuaranteeCase(string caseNumber, string applicantUserId, ApplicantType applicantType)
    {
        Id = Guid.NewGuid();
        CaseNumber = caseNumber;
        ApplicantUserId = applicantUserId;
        ApplicantType = applicantType;
        CurrentPhase = GuaranteeCasePhase.Application;
        CurrentStatus = GuaranteeCaseStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string CaseNumber { get; private set; }
    public string? Title { get; private set; }
    public string ApplicantUserId { get; private set; }
    public ApplicantType ApplicantType { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Company? ApplicantCompany { get; private set; }

    public GuaranteeCasePhase CurrentPhase { get; private set; }
    public GuaranteeCaseStatus CurrentStatus { get; private set; }
    public string? WorkflowInstanceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public GuaranteeCaseApplication? Application { get; private set; }
    public GuaranteeApprovalForm? ApprovalForm { get; private set; }

    public List<GuaranteeCaseDocument> Documents { get; private set; } = [];
    public List<GuaranteeCaseComment> Comments { get; private set; } = [];
    public List<GuaranteeCaseWorkflowHistory> WorkflowHistory { get; private set; } = [];

    public void TransitionTo(
        GuaranteeCaseStatus nextStatus,
        string changedByUserId,
        string? actorRole = null,
        GuaranteeWorkflowAction? action = null,
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

        if (nextStatus == GuaranteeCaseStatus.Completed)
            CompletedAt = DateTimeOffset.UtcNow;

        WorkflowHistory.Add(new GuaranteeCaseWorkflowHistory(
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

    public static GuaranteeCasePhase DerivePhaseFromStatus(GuaranteeCaseStatus status) => status switch
    {
        GuaranteeCaseStatus.Draft or GuaranteeCaseStatus.DataEntry or GuaranteeCaseStatus.CreditReview => GuaranteeCasePhase.Application,
        GuaranteeCaseStatus.ApprovalFormEntry or GuaranteeCaseStatus.CeoApprovalInitial => GuaranteeCasePhase.CreditAssessment,
        GuaranteeCaseStatus.WaitingDraftContract or GuaranteeCaseStatus.WaitingSignedContractAndAttachments or GuaranteeCaseStatus.WaitingFinalContract => GuaranteeCasePhase.Legal,
        GuaranteeCaseStatus.FinancialAttachmentReview or GuaranteeCaseStatus.CeoApprovalFinal or GuaranteeCaseStatus.WaitingIssuanceDocuments => GuaranteeCasePhase.Finance,
        GuaranteeCaseStatus.Completed or GuaranteeCaseStatus.Rejected or GuaranteeCaseStatus.Cancelled or GuaranteeCaseStatus.Archived => GuaranteeCasePhase.Closing,
        _ => GuaranteeCasePhase.Application
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

    public GuaranteeCaseApplication UpsertApplication(
        GuaranteeType? guaranteeType,
        string? contractSubject,
        bool? isKnowledgeBasedProduct,
        string? beneficiaryName,
        string? beneficiaryNationalId,
        BeneficiaryCompanyType? beneficiaryCompanyType,
        ApplicantCategory applicantCategory,
        string? applicantCategoryOther,
        ApplicantLegalForm? applicantLegalForm,
        string? baseContractNumber,
        decimal? baseContractAmount,
        string? baseContractAmountInWords,
        decimal? priceAdjustmentRatePercent,
        string? executionProvince,
        decimal? requestedGuaranteeAmount,
        int? initialValidityDays,
        DateOnly? validityFrom,
        DateOnly? validityTo,
        string? collateralDescription,
        string? facilitySubject)
    {
        if (Application is null)
            Application = new GuaranteeCaseApplication(Id);

        Application.Update(
            guaranteeType,
            contractSubject,
            isKnowledgeBasedProduct,
            beneficiaryName,
            beneficiaryNationalId,
            beneficiaryCompanyType,
            applicantCategory,
            applicantCategoryOther,
            applicantLegalForm,
            baseContractNumber,
            baseContractAmount,
            baseContractAmountInWords,
            priceAdjustmentRatePercent,
            executionProvince,
            requestedGuaranteeAmount,
            initialValidityDays,
            validityFrom,
            validityTo,
            collateralDescription,
            facilitySubject);

        UpdatedAt = DateTimeOffset.UtcNow;
        return Application;
    }

    public GuaranteeApprovalForm UpsertApprovalForm(
        decimal? creditLimitWithCheck,
        decimal? fundIssuedGuaranteesTotal,
        decimal? activeCommitments,
        decimal? remainingCredit,
        GuaranteeType? guaranteeType,
        decimal? guaranteeAmount,
        string? guaranteeAmountInWords,
        string? contractSubject,
        string? beneficiary,
        DateOnly? issuanceDate,
        DateOnly? expiryDate,
        int? activeDurationDays,
        decimal? depositRatePercent,
        decimal? depositAmount,
        decimal? annualCommissionRatePercent,
        decimal? commissionAmount,
        string? collateralDescription,
        string? guarantorsDescription,
        string? otherNotes)
    {
        if (ApprovalForm is null)
            ApprovalForm = new GuaranteeApprovalForm(Id);

        ApprovalForm.Update(
            creditLimitWithCheck,
            fundIssuedGuaranteesTotal,
            activeCommitments,
            remainingCredit,
            guaranteeType,
            guaranteeAmount,
            guaranteeAmountInWords,
            contractSubject,
            beneficiary,
            issuanceDate,
            expiryDate,
            activeDurationDays,
            depositRatePercent,
            depositAmount,
            annualCommissionRatePercent,
            commissionAmount,
            collateralDescription,
            guarantorsDescription,
            otherNotes);

        UpdatedAt = DateTimeOffset.UtcNow;
        return ApprovalForm;
    }

    public void RequestRevision(
        GuaranteeCaseStatus targetStatus,
        string requestedByUserId,
        string? actorRole,
        GuaranteeWorkflowAction? action,
        Guid? correlationId,
        string message,
        bool isInternal)
    {
        var fromStatus = CurrentStatus;
        var fromPhase = CurrentPhase;

        CurrentStatus = targetStatus;
        CurrentPhase = DerivePhaseFromStatus(targetStatus);
        UpdatedAt = DateTimeOffset.UtcNow;

        Comments.Add(new GuaranteeCaseComment(
            caseId: Id,
            phase: fromPhase,
            senderUserId: requestedByUserId,
            senderRole: actorRole,
            message: message,
            isRevisionRequest: true,
            isInternal: isInternal));

        WorkflowHistory.Add(new GuaranteeCaseWorkflowHistory(
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
        GuaranteeCasePhase phase,
        string senderUserId,
        string? senderRole,
        string message,
        bool isRevisionRequest,
        bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Comments.Add(new GuaranteeCaseComment(
            caseId: Id,
            phase: phase,
            senderUserId: senderUserId,
            senderRole: senderRole,
            message: message.Trim(),
            isRevisionRequest: isRevisionRequest,
            isInternal: isInternal));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public GuaranteeCaseDocument AddDocument(
        string s3Key,
        string fileName,
        string mimeType,
        long fileSize,
        int version,
        GuaranteeDocumentType documentType,
        string uploadedByUserId)
    {
        var doc = new GuaranteeCaseDocument(
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
}
