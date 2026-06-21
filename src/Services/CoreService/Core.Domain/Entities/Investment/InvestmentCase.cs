using BuildingBlocks.Domain.Abstractions;
using BuildingBlocks.Domain.Entities;
using Core.Domain.Common;
using Core.Domain.Enums;
using Core.Domain.Events;
using Core.Domain.Identity.Entities;

namespace Core.Domain.Entities.Investment;

public sealed class InvestmentCase : AggregateRoot<Guid>, IAuditableEntity, ISoftDelete
{
    private InvestmentCase()
    {
        CaseNumber = default!;
        ApplicantUserId = default!;
    }

    public InvestmentCase(string caseNumber, string applicantUserId, ApplicantType applicantType)
    {
        Id = Guid.NewGuid();
        CaseNumber = caseNumber;
        ApplicantUserId = applicantUserId;
        ApplicantType = applicantType;

        CurrentPhase = CasePhase.Application;
        CurrentStatus = CaseStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string CaseNumber { get; private set; }
    public string? Title { get; private set; }
    public string ApplicantUserId { get; private set; }
    public ApplicantType ApplicantType { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Company? ApplicantCompany { get; private set; }

    public CasePhase CurrentPhase { get; private set; }
    public CaseStatus CurrentStatus { get; private set; }
    public string? WorkflowInstanceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public InvestmentCaseApplicantProfile? ApplicantProfile { get; private set; }
    public InvestmentCaseAttractionBasis? AttractionBasis { get; private set; }
    public InvestmentCaseFinancialWorksheet? FinancialWorksheet { get; private set; }

    public List<InvestmentCaseDocument> Documents { get; private set; } = [];
    public List<InvestmentCaseComment> Comments { get; private set; } = [];
    public List<InvestmentCaseRevision> Revisions { get; private set; } = [];
    public List<InvestmentCaseEvaluation> Evaluations { get; private set; } = [];
    public List<InvestmentCaseValuation> Valuations { get; private set; } = [];
    public List<InvestmentCasePayment> Payments { get; private set; } = [];
    public List<InvestmentCaseWorkflowHistory> WorkflowHistory { get; private set; } = [];

    public void TransitionTo(
        CaseStatus nextStatus,
        string changedByUserId,
        string? actorRole = null,
        WorkflowAction? action = null,
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

        if (nextStatus == CaseStatus.Completed)
            CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new CasePhaseChangedDomainEvent(Id, fromPhase, CurrentPhase));

        WorkflowHistory.Add(new InvestmentCaseWorkflowHistory(
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

    private static CasePhase DerivePhaseFromStatus(CaseStatus status) => status switch
    {
        CaseStatus.Draft or CaseStatus.DataEntry1 or CaseStatus.ReviewDataEntry1 or CaseStatus.DataEntry2 or CaseStatus.ReviewDataEntry2 => CasePhase.Application,
        CaseStatus.InitialValuation or CaseStatus.SecondaryValuation => CasePhase.Valuation,
        CaseStatus.WaitingPreliminaryContract or CaseStatus.WaitingUserReviewPreliminaryContract or CaseStatus.ContractDrafting or CaseStatus.WaitingContractSignature or CaseStatus.WaitingSignedContractUpload => CasePhase.Legal,
        CaseStatus.WaitingFinancialWorksheet or CaseStatus.FinancialWorksheetReview or CaseStatus.WaitingCeoApproval or CaseStatus.WaitingPayment => CasePhase.Finance,
        CaseStatus.Completed or CaseStatus.Rejected or CaseStatus.Cancelled or CaseStatus.Archived => CasePhase.Closing,
        _ => CasePhase.Application
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

    public void Submit(string submittedByUserId, string? comment = null)
    {
        // Explicit submission logic for Data Entry 1
        if (CurrentStatus != CaseStatus.Draft)
            throw new InvalidOperationException(string.Format(DomainMessages.CannotSubmitFromStatus, CurrentStatus));

        TransitionTo(CaseStatus.DataEntry1, submittedByUserId, actorRole: null, action: WorkflowAction.Submit, correlationId: null, comment: comment);
    }

    public InvestmentCaseRevision CreateNewRevision(CasePhase phase, string submittedByUserId, DateTimeOffset submittedAt)
    {
        var currentRevisionNumber = Revisions
            .Where(r => r.Phase == phase)
            .Select(r => r.RevisionNumber)
            .DefaultIfEmpty(0)
            .Max();

        var revision = new InvestmentCaseRevision(
            caseId: Id,
            phase: phase,
            revisionNumber: currentRevisionNumber + 1,
            submittedByUserId: submittedByUserId,
            submittedAt: submittedAt);

        Revisions.Add(revision);
        UpdatedAt = DateTimeOffset.UtcNow;
        return revision;
    }

    public InvestmentCaseApplicantProfile UpsertApplicantProfile(
        string representativeFullName,
        BusinessStage businessStage,
        string contactEmail,
        decimal requestedAmount)
    {
        if (ApplicantProfile is null)
            ApplicantProfile = new InvestmentCaseApplicantProfile(Id, representativeFullName, businessStage, contactEmail, requestedAmount);

        ApplicantProfile.Update(representativeFullName, businessStage, contactEmail, requestedAmount);
        UpdatedAt = DateTimeOffset.UtcNow;
        return ApplicantProfile;
    }

    public InvestmentCaseAttractionBasis UpsertAttractionBasis(string investmentAttractionBasis)
    {
        if (AttractionBasis is null)
            AttractionBasis = new InvestmentCaseAttractionBasis(Id, investmentAttractionBasis);

        AttractionBasis.Update(investmentAttractionBasis);
        UpdatedAt = DateTimeOffset.UtcNow;
        return AttractionBasis;
    }

    public InvestmentCaseFinancialWorksheet UpsertFinancialWorksheet(
        string bankName,
        string iban,
        decimal approvedAmount,
        string paymentSchedule,
        string? notes)
    {
        if (FinancialWorksheet is null)
            FinancialWorksheet = new InvestmentCaseFinancialWorksheet(Id, bankName, iban, approvedAmount, paymentSchedule, notes);
        else
            FinancialWorksheet.Update(bankName, iban, approvedAmount, paymentSchedule, notes);

        UpdatedAt = DateTimeOffset.UtcNow;
        return FinancialWorksheet;
    }

    public void RequestRevision(
        CaseStatus targetStatus,
        string requestedByUserId,
        string? actorRole,
        WorkflowAction? action,
        Guid? correlationId,
        string message,
        bool isInternal)
    {
        var fromStatus = CurrentStatus;
        var fromPhase = CurrentPhase;

        CurrentStatus = targetStatus;
        CurrentPhase = DerivePhaseFromStatus(targetStatus);
        UpdatedAt = DateTimeOffset.UtcNow;

        Comments.Add(new InvestmentCaseComment(
            caseId: Id,
            phase: fromPhase,
            senderUserId: requestedByUserId,
            senderRole: actorRole,
            message: message,
            isRevisionRequest: true,
            isInternal: isInternal));

        WorkflowHistory.Add(new InvestmentCaseWorkflowHistory(
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

        AddDomainEvent(new RevisionRequestedDomainEvent(Id, fromPhase, requestedByUserId));
    }

    public void AddDiscussionComment(
        CasePhase phase,
        string senderUserId,
        string? senderRole,
        string message,
        bool isRevisionRequest,
        bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Comments.Add(new InvestmentCaseComment(
            caseId: Id,
            phase: phase,
            senderUserId: senderUserId,
            senderRole: senderRole,
            message: message.Trim(),
            isRevisionRequest: isRevisionRequest,
            isInternal: isInternal));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public InvestmentCaseDocument AddDocument(
        string s3Key,
        string fileName,
        string mimeType,
        long fileSize,
        int version,
        DocumentType documentType,
        string uploadedByUserId)
    {
        var doc = new InvestmentCaseDocument(
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

    public InvestmentCasePayment AddPayment(
        decimal amount,
        DateOnly paymentDate,
        string transactionNumber,
        string? receiptS3Key,
        string? notes,
        PaymentMethod method,
        PaymentStatus status,
        string createdByUserId)
    {
        var payment = new InvestmentCasePayment(
            caseId: Id,
            amount: amount,
            paymentDate: paymentDate,
            transactionNumber: transactionNumber,
            receiptS3Key: receiptS3Key,
            notes: notes,
            method: method,
            status: status,
            createdByUserId: createdByUserId);

        Payments.Add(payment);
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PaymentRecordedDomainEvent(Id, payment.Id, amount));

        CheckPaymentCompletion(createdByUserId);

        return payment;
    }

    public InvestmentCaseValuation AddValuation(ValuationType type, decimal amount, string? notes, string createdByUserId)
    {
        var valuation = new InvestmentCaseValuation(
            caseId: Id,
            type: type,
            amount: amount,
            notes: notes ?? string.Empty,
            createdByUserId: createdByUserId);

        Valuations.Add(valuation);
        UpdatedAt = DateTimeOffset.UtcNow;
        return valuation;
    }

    public void CheckPaymentCompletion(string userId)
    {
        if (CurrentStatus != CaseStatus.WaitingPayment)
            return;

        if (FinancialWorksheet == null || FinancialWorksheet.ApprovedAmount <= 0)
            return;

        var totalConfirmed = Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        if (totalConfirmed >= FinancialWorksheet.ApprovedAmount)
        {
            TransitionTo(CaseStatus.Completed, userId, actorRole: null, action: WorkflowAction.CompletePayment, correlationId: null, comment: "Automatic completion based on full payment confirmation.");
        }
    }
}
