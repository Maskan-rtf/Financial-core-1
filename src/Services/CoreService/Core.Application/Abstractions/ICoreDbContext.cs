using Core.Domain.Entities;
using Core.Domain.Entities.Fund;
using Core.Domain.Entities.Workflow;
using Core.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;


namespace Core.Application.Abstractions;

public interface ICoreDbContext
{
    DbSet<InvestmentCase> InvestmentCases { get; }
    DbSet<InvestmentCaseApplicantProfile> InvestmentCaseApplicantProfiles { get; }
    DbSet<InvestmentCaseAttractionBasis> InvestmentCaseAttractionBases { get; }
    DbSet<InvestmentCaseFinancialWorksheet> FinancialWorksheets { get; }
    DbSet<InvestmentCaseDocument> CaseDocuments { get; }
    DbSet<InvestmentCaseComment> CaseComments { get; }
    DbSet<InvestmentCaseCommentAttachment> CaseCommentAttachments { get; }
    DbSet<InvestmentCaseRevision> CaseRevisions { get; }
    DbSet<InvestmentCaseEvaluation> CaseEvaluations { get; }
    DbSet<InvestmentCaseEvaluationItem> CaseEvaluationItems { get; }
    DbSet<InvestmentCaseValuation> CaseValuations { get; }
    DbSet<InvestmentCasePayment> PaymentRecords { get; }
    DbSet<InvestmentCaseWorkflowHistory> CaseWorkflowHistories { get; }
    DbSet<GuaranteeCase> GuaranteeCases { get; }
    DbSet<GuaranteeCaseApplication> GuaranteeCaseApplications { get; }
    DbSet<GuaranteeApprovalForm> GuaranteeApprovalForms { get; }
    DbSet<GuaranteeAmendmentHistoryRecord> GuaranteeAmendmentHistoryRecords { get; }
    DbSet<GuaranteeCaseDocument> GuaranteeCaseDocuments { get; }
    DbSet<GuaranteeCaseComment> GuaranteeCaseComments { get; }
    DbSet<GuaranteeCaseWorkflowHistory> GuaranteeCaseWorkflowHistories { get; }
    DbSet<GuaranteeRenewalCase> GuaranteeRenewalCases { get; }
    DbSet<GuaranteeApplicantCreditProfile> GuaranteeApplicantCreditProfiles { get; }
    DbSet<FundCreditLimit> FundCreditLimits { get; }
    DbSet<LoanCase> LoanCases { get; }
    DbSet<LoanCaseApplication> LoanCaseApplications { get; }
    DbSet<LoanApprovalDetail> LoanApprovalDetails { get; }
    DbSet<LoanCaseDocument> LoanCaseDocuments { get; }
    DbSet<LoanInstallment> LoanInstallments { get; }
    DbSet<LoanPayment> LoanPayments { get; }
    DbSet<LoanCaseComment> LoanCaseComments { get; }
    DbSet<LoanCaseWorkflowHistory> LoanCaseWorkflowHistories { get; }
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<DashboardStatsSnapshot> DashboardStatsSnapshots { get; }
    DbSet<ProcessInstance> ProcessInstances { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
