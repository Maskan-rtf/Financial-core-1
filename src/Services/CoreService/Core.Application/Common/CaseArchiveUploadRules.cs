using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Results;
using Core.Domain.Enums;

namespace Core.Application.Common;

/// <summary>
/// Applicant filing-table uploads: zip/rar/pdf/png/jpg/jpeg, max 50 MB.
/// Stage uploads (contracts, receipts, …) stay on the broader MIME rules of each flow.
/// </summary>
public static class CaseArchiveUploadRules
{
    public const long MaxBytes = 50L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".rar",
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private static readonly HashSet<GuaranteeDocumentType> GuaranteeFilingTypes =
    [
        GuaranteeDocumentType.EstablishmentGazette,
        GuaranteeDocumentType.FinancialStatements3Years,
        GuaranteeDocumentType.ActivityLicenses,
        GuaranteeDocumentType.BankAccountTurnover,
        GuaranteeDocumentType.CreditInquiryResult,
        GuaranteeDocumentType.CreditInformationForm,
        GuaranteeDocumentType.CaseFormationAttachmentForm,
        GuaranteeDocumentType.CompanyIntroductionDocs,
        GuaranteeDocumentType.LeaseOrOwnershipDeed,
        GuaranteeDocumentType.CeoBoardIdCards,
        GuaranteeDocumentType.FeasibilityReport,
        GuaranteeDocumentType.ProjectProposal,
        GuaranteeDocumentType.SalesContractsScan,
        GuaranteeDocumentType.TenderAnnouncement,
        GuaranteeDocumentType.BaseContractImage,
        GuaranteeDocumentType.AssetOwnershipDocuments,
        GuaranteeDocumentType.CollateralDocuments,
        GuaranteeDocumentType.QuarterlySalesTaxReports,
        GuaranteeDocumentType.EmployeeInsuranceList,
        GuaranteeDocumentType.Other
    ];

    private static readonly HashSet<LoanDocumentType> LoanFilingTypes =
    [
        LoanDocumentType.FundRequiredDocsAndCompanySpecs,
        LoanDocumentType.EstablishmentGazette,
        LoanDocumentType.CeoBoardIdCards,
        LoanDocumentType.FinancialAndCreditContracts,
        LoanDocumentType.FinancialStatements,
        LoanDocumentType.ThreeYearActualFinancialStatements,
        LoanDocumentType.BankAccountTurnover,
        LoanDocumentType.EmployeeInsuranceReceipt,
        LoanDocumentType.ProposedFinancingContract,
        LoanDocumentType.ActivityLicenses,
        LoanDocumentType.CreditInquiryResult,
        LoanDocumentType.CreditRatingReport,
        LoanDocumentType.AssetOwnershipDocuments,
        LoanDocumentType.CollateralDocuments,
        LoanDocumentType.QuarterlySalesTaxReports,
        LoanDocumentType.CompanyIntroductionDocs,
        LoanDocumentType.ProjectProposal,
        LoanDocumentType.Other
    ];

    public static bool IsGuaranteeFilingDocument(GuaranteeDocumentType type)
        => GuaranteeFilingTypes.Contains(type);

    public static bool IsLoanFilingDocument(LoanDocumentType type)
        => LoanFilingTypes.Contains(type);

    public static Result ValidateArchiveUpload(string? fileName, long fileSize)
    {
        if (fileSize <= 0)
            return Result.Fail(Error.Validation("حجم فایل نامعتبر است."));

        if (fileSize > MaxBytes)
            return Result.Fail(Error.Validation("حجم فایل نباید بیشتر از ۵۰ مگابایت باشد."));

        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return Result.Fail(Error.Validation("فقط فایل ZIP، RAR، PDF، PNG، JPG یا JPEG مجاز است."));

        return Result.Ok();
    }
}
