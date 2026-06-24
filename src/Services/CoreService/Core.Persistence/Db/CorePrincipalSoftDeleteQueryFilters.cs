using BuildingBlocks.Persistence.Db;
using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Persistence.Db;

internal static class CorePrincipalSoftDeleteQueryFilters
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseComment, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseEvaluation, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseEvaluationItem, InvestmentCase>(e => e.Evaluation.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseRevision, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseValuation, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseWorkflowHistory, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseFinancialWorksheet, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseApplicantProfile, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseAttractionBasis, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCasePayment, InvestmentCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<InvestmentCaseDocument, InvestmentCase>(e => e.Case);

        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeApprovalForm, GuaranteeCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeCaseApplication, GuaranteeCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeCaseComment, GuaranteeCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeCaseWorkflowHistory, GuaranteeCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeCaseDocument, GuaranteeCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeAmendmentHistoryRecord, GuaranteeCase>(e => e.GuaranteeCase);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<GuaranteeRenewalCase, GuaranteeCase>(e => e.ParentGuaranteeCase);

        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanApprovalDetail, LoanCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanCaseApplication, LoanCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanCaseComment, LoanCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanCaseWorkflowHistory, LoanCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanInstallment, LoanCase>(e => e.Case);
        modelBuilder.ApplyFilterForSoftDeletePrincipal<LoanPayment, LoanCase>(e => e.Case);
    }
}
