using BuildingBlocks.Persistence.Db;
using BuildingBlocks.Persistence.Db.DomainEvents;
using Core.Domain.Entities;
using Core.Domain.Entities.Analytics;
using Core.Domain.Entities.Fund;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Entities.Investment;
using Core.Domain.Entities.Loan;
using Core.Domain.Entities.Workflow;
using Core.Domain.Identity.Entities;
using Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.DemoSeeder;

/// <summary>
/// Seeder DbContext mirrored from CoreDbContext, with optional schema adaptations.
/// </summary>
internal sealed class DemoDbContext : DbContextBase
{
    private readonly SchemaFeatures _features;

    public DemoDbContext(
        DbContextOptions<DemoDbContext> options,
        IDomainEventDispatcher domainEventDispatcher,
        SchemaFeatures features)
        : base(options, domainEventDispatcher)
    {
        _features = features;
    }

    public DbSet<InvestmentCase> InvestmentCases => Set<InvestmentCase>();
    public DbSet<InvestmentCaseApplicantProfile> InvestmentCaseApplicantProfiles => Set<InvestmentCaseApplicantProfile>();
    public DbSet<InvestmentCaseAttractionBasis> InvestmentCaseAttractionBases => Set<InvestmentCaseAttractionBasis>();
    public DbSet<InvestmentCaseFinancialWorksheet> FinancialWorksheets => Set<InvestmentCaseFinancialWorksheet>();
    public DbSet<InvestmentCaseDocument> CaseDocuments => Set<InvestmentCaseDocument>();
    public DbSet<InvestmentCaseComment> CaseComments => Set<InvestmentCaseComment>();
    public DbSet<InvestmentCaseCommentAttachment> CaseCommentAttachments => Set<InvestmentCaseCommentAttachment>();
    public DbSet<InvestmentCaseRevision> CaseRevisions => Set<InvestmentCaseRevision>();
    public DbSet<InvestmentCaseEvaluation> CaseEvaluations => Set<InvestmentCaseEvaluation>();
    public DbSet<InvestmentCaseEvaluationItem> CaseEvaluationItems => Set<InvestmentCaseEvaluationItem>();
    public DbSet<InvestmentCaseValuation> CaseValuations => Set<InvestmentCaseValuation>();
    public DbSet<InvestmentCasePayment> PaymentRecords => Set<InvestmentCasePayment>();
    public DbSet<InvestmentCaseWorkflowHistory> CaseWorkflowHistories => Set<InvestmentCaseWorkflowHistory>();
    public DbSet<GuaranteeCase> GuaranteeCases => Set<GuaranteeCase>();
    public DbSet<GuaranteeCaseApplication> GuaranteeCaseApplications => Set<GuaranteeCaseApplication>();
    public DbSet<GuaranteeApprovalForm> GuaranteeApprovalForms => Set<GuaranteeApprovalForm>();
    public DbSet<GuaranteeAmendmentHistoryRecord> GuaranteeAmendmentHistoryRecords => Set<GuaranteeAmendmentHistoryRecord>();
    public DbSet<GuaranteeCaseDocument> GuaranteeCaseDocuments => Set<GuaranteeCaseDocument>();
    public DbSet<GuaranteeCaseComment> GuaranteeCaseComments => Set<GuaranteeCaseComment>();
    public DbSet<GuaranteeCaseWorkflowHistory> GuaranteeCaseWorkflowHistories => Set<GuaranteeCaseWorkflowHistory>();
    public DbSet<GuaranteeRenewalCase> GuaranteeRenewalCases => Set<GuaranteeRenewalCase>();
    public DbSet<GuaranteeApplicantCreditProfile> GuaranteeApplicantCreditProfiles => Set<GuaranteeApplicantCreditProfile>();
    public DbSet<FundCreditLimit> FundCreditLimits => Set<FundCreditLimit>();
    public DbSet<LoanCase> LoanCases => Set<LoanCase>();
    public DbSet<LoanCaseApplication> LoanCaseApplications => Set<LoanCaseApplication>();
    public DbSet<LoanApprovalDetail> LoanApprovalDetails => Set<LoanApprovalDetail>();
    public DbSet<LoanCaseDocument> LoanCaseDocuments => Set<LoanCaseDocument>();
    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();
    public DbSet<LoanCaseComment> LoanCaseComments => Set<LoanCaseComment>();
    public DbSet<LoanCaseWorkflowHistory> LoanCaseWorkflowHistories => Set<LoanCaseWorkflowHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<DashboardStatsSnapshot> DashboardStatsSnapshots => Set<DashboardStatsSnapshot>();
    public DbSet<ProcessInstance> ProcessInstances => Set<ProcessInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        var investmentCase = modelBuilder.Entity<InvestmentCase>();
        foreach (var propertyName in new[] { "RowVersion", "xmin" })
        {
            var property = investmentCase.Metadata.FindProperty(propertyName);
            if (property is not null)
                investmentCase.Metadata.RemoveProperty(property);
        }

        // Production DB may lack User.CompanyId; users are inserted via SQL in that case.
        // Keep the EF mapping as-is so the rest of the model stays valid.

        if (!_features.HasProcessInstances)
            modelBuilder.Ignore<ProcessInstance>();

        if (!_features.HasFundCreditLimits)
            modelBuilder.Ignore<FundCreditLimit>();

        if (!_features.HasGuaranteeApplicantCreditProfiles)
            modelBuilder.Ignore<GuaranteeApplicantCreditProfile>();
    }
}
