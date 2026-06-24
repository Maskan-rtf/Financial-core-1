using BuildingBlocks.Application.Results;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface IGuaranteeCaseRepository
{
    Task<GuaranteeCase?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<GuaranteeCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<GuaranteeCaseDetailProjection?> GetDetailProjectionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<GuaranteeCase?> GetScopedForTransitionAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GuaranteeCase?> GetScopedWithDocumentsAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<GuaranteeCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);
    Task AddAsync(GuaranteeCase guaranteeCase, CancellationToken cancellationToken);
    Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);
    Task<PagedResult<GuaranteeCaseListProjection>> GetPagedAsync(
        GetGuaranteeCasesRequest request,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuaranteeWorkflowHistoryListProjection>> GetWorkflowHistoryAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuaranteeCaseCommentListProjection>> GetCommentsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GuaranteeKanbanCaseProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<bool> ExistsScopedAsync(Guid caseId, string userId, bool isInternalUser, CancellationToken cancellationToken);

    Task<GuaranteeCaseStatus?> GetCurrentStatusScopedAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<GuaranteeCase?> GetAsNoTrackingAsync(Guid caseId, CancellationToken cancellationToken);

    Task<int> TouchUpdatedAtAsync(Guid caseId, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    Task<int> SetTitleAsync(Guid caseId, string? title, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    Task<int> ApplyStateAndAmendmentAsync(
        Guid caseId,
        GuaranteeCaseStatus status,
        GuaranteeCasePhase phase,
        DateTimeOffset updatedAt,
        DateTimeOffset? completedAt,
        AmendmentType? amendmentType,
        string? amendmentReason,
        bool amendmentRequiresCreditReview,
        string? amendmentOriginalGuaranteeReference,
        bool legalOverrideApproved,
        bool settlementConfirmationRequired,
        DateOnly? amendmentRequestedValidityTo,
        decimal? amendmentRequestedAmount,
        DateOnly? amendmentApprovedValidityTo,
        decimal? amendmentApprovedAmount,
        DateTimeOffset? amendmentCreatedAt,
        DateTimeOffset? amendmentCompletedAt,
        CancellationToken cancellationToken);

    Task AddDocumentAsync(GuaranteeCaseDocument document, CancellationToken cancellationToken);

    void AddAmendmentHistoryRecord(GuaranteeAmendmentHistoryRecord record);

    Task AddAmendmentHistoryRecordAsync(GuaranteeAmendmentHistoryRecord record, CancellationToken cancellationToken);

    Task AddWorkflowHistoryAsync(GuaranteeCaseWorkflowHistory history, CancellationToken cancellationToken);

    Task InsertWorkflowHistoryAsync(GuaranteeCaseWorkflowHistory history, CancellationToken cancellationToken);

    Task AddCommentAsync(GuaranteeCaseComment comment, CancellationToken cancellationToken);

    Task InsertCommentAsync(GuaranteeCaseComment comment, CancellationToken cancellationToken);

    Task InsertAmendmentHistoryRecordAsync(GuaranteeAmendmentHistoryRecord record, CancellationToken cancellationToken);

    Task<int> ApplyLatestPendingAmendmentAuditDecisionAsync(
        Guid caseId,
        GuaranteeAmendmentHistoryStatus status,
        string approvalUser,
        DateTimeOffset decidedAt,
        string? decisionReason,
        CancellationToken cancellationToken);

    Task<GuaranteeAmendmentHistoryStatus?> GetLatestAmendmentHistoryStatusAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuaranteeAmendmentHistoryRecord>> GetAmendmentHistoryRecordsAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    void ClearChangeTracker();

    void DetachTrackedExceptAdded();

    IReadOnlyList<string> DescribeTrackedEntries();

    IReadOnlyList<GuaranteeAmendmentHistoryRecord> CapturePendingNewAmendmentHistory();

    Task<GuaranteeApprovalForm?> GetApprovalFormAsync(Guid caseId, CancellationToken cancellationToken);

    Task<bool> ApprovalFormExistsAsync(Guid caseId, CancellationToken cancellationToken);

    Task AddApprovalFormAsync(GuaranteeApprovalForm approvalForm, CancellationToken cancellationToken);

    Task PersistApprovedAmendmentExtensionAsync(
        Guid caseId,
        DateOnly approvedValidityTo,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task PersistApprovedAmendmentReductionAsync(
        Guid caseId,
        decimal approvedAmount,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task PersistApprovedAmendmentCancellationAsync(Guid caseId, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    Task AddApplicantCreditProfileAsync(GuaranteeApplicantCreditProfile profile, CancellationToken cancellationToken);

    Task<GuaranteeApplicantCreditProfile?> FindApplicantCreditProfileByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<GuaranteeApplicantCreditProfile?> FindApplicantCreditProfileByUserAsync(
        string applicantUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GuaranteeCaseCreditProjection>> GetCreditProjectionsAsync(CancellationToken cancellationToken);

    Task<GuaranteeCaseApplication?> GetApplicationByCaseIdAsync(Guid caseId, CancellationToken cancellationToken);

    Task<GuaranteeCaseApplication> UpsertApplicationAsync(
        Guid caseId,
        UpdateGuaranteeApplicationRequest request,
        CancellationToken cancellationToken);
}
