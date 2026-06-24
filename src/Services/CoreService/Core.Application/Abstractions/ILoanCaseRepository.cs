using BuildingBlocks.Application.Results;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Abstractions;

public interface ILoanCaseRepository
{
    Task<LoanCase?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<LoanCase?> GetScopedAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<LoanCaseListProjection?> GetDetailProjectionAsync(
        Guid id,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanInstallmentListProjection>> GetInstallmentProjectionsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanPaymentListProjection>> GetPaymentProjectionsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<LoanCase?> GetScopedForTransitionAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<string?> GetWorkflowInstanceIdAsync(Guid id, CancellationToken cancellationToken);
    Task<LoanCase?> GetScopedWithDocumentsAsync(Guid id, string userId, bool isInternalUser, CancellationToken cancellationToken);
    Task<LoanCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);
    Task AddAsync(LoanCase loanCase, CancellationToken cancellationToken);
    Task<bool> ExistsCaseNumberAsync(string caseNumber, CancellationToken cancellationToken);
    Task<PagedResult<LoanCaseListProjection>> GetPagedAsync(
        GetLoanCasesRequest request,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanWorkflowHistoryListProjection>> GetWorkflowHistoryAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanCaseCommentListProjection>> GetCommentsAsync(
        Guid caseId,
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanKanbanCaseProjection>> ListActiveKanbanProjectionsAsync(
        string userId,
        bool isInternalUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanCaseCreditProjection>> GetCreditProjectionsAsync(CancellationToken cancellationToken);
}
