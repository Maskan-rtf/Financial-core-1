using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

public static class LoanDocumentRequirements
{
    /// <summary>Same set the applicant form marks required in loan data entry.</summary>
    public static readonly LoanDocumentType[] RequiredForDataEntrySubmit =
    [
        LoanDocumentType.SupplementaryAttachmentForm,
        LoanDocumentType.EstablishmentGazette,
        LoanDocumentType.CeoBoardIdCards,
        LoanDocumentType.ThreeYearActualFinancialStatements,
        LoanDocumentType.BankAccountTurnover,
        LoanDocumentType.EmployeeInsuranceReceipt,
        LoanDocumentType.ProposedFinancingContract,
        LoanDocumentType.ActivityLicenses,
        LoanDocumentType.CreditInquiryResult,
        LoanDocumentType.CollateralDocuments,
        LoanDocumentType.QuarterlySalesTaxReports
    ];

    public static IReadOnlyList<LoanDocumentType> GetMissingForDataEntrySubmit(IEnumerable<LoanCaseDocument> documents)
    {
        var present = documents
            .Where(d => !d.IsDeleted)
            .Select(d => d.DocumentType)
            .ToHashSet();

        return RequiredForDataEntrySubmit
            .Where(t => !present.Contains(t))
            .ToList();
    }

    public static string ToPersianLabel(LoanDocumentType type) => type switch
    {
        LoanDocumentType.SupplementaryAttachmentForm => "فرم رضایت‌نامه دریافت اطلاعات اعتباری",
        LoanDocumentType.FundRequiredDocsAndCompanySpecs => "اسناد مورد نیاز صندوق و مشخصات شرکت",
        LoanDocumentType.EstablishmentGazette => "آگهی تأسیس و آخرین روزنامه رسمی",
        LoanDocumentType.CeoBoardIdCards => "کارت ملی و شناسنامه مدیرعامل و اعضای هیئت مدیره",
        LoanDocumentType.FinancialAndCreditContracts => "قراردادهای مالی و اعتباری",
        LoanDocumentType.FinancialStatements => "صورت‌های مالی سال گذشته",
        LoanDocumentType.ThreeYearActualFinancialStatements => "صورت‌های مالی واقعی سه ساله",
        LoanDocumentType.BankAccountTurnover => "گردش/معدل حساب‌های بانکی",
        LoanDocumentType.MainLoanRequestForm => "فرم درخواست اصلی / نامه درخواست",
        LoanDocumentType.ProposedFinancingContract => "قرارداد پیشنهادی مرتبط با درخواست تأمین مالی",
        LoanDocumentType.EmployeeInsuranceReceipt => "آخرین رسید بیمه کارکنان",
        LoanDocumentType.ActivityLicenses => "مجوزهای فعالیت (پروانه بهره‌برداری، گواهی دانش‌بنیان و سایر مصوبات مرتبط)",
        LoanDocumentType.CreditInquiryResult => "آخرین نتیجه استعلام اعتبارسنجی شرکت",
        LoanDocumentType.CreditRatingReport => "گزارش رتبه‌بندی اعتباری از سامانه اعتبارسنجی",
        LoanDocumentType.AssetOwnershipDocuments => "تصویر اسناد دارایی شرکت، مدیران و ضامنین",
        LoanDocumentType.CollateralDocuments => "مستندات وثایق قابل ارائه",
        LoanDocumentType.QuarterlySalesTaxReports => "گزارش‌های فروش فصلی سال جاری از سامانه مالیاتی",
        LoanDocumentType.CompanyIntroductionDocs => "اسناد معرفی شرکت (کاتالوگ، چارت سازمانی و ...)",
        LoanDocumentType.ProjectProposal => "پروپوزال طرح درخواستی",
        LoanDocumentType.RevisionRequestAttachment => "پیوست درخواست اصلاح",
        LoanDocumentType.RawContract => "قرارداد خام",
        LoanDocumentType.InstallmentScheduleExport => "خروجی جدول اقساط",
        LoanDocumentType.SignedContract => "قرارداد امضاشده",
        LoanDocumentType.SignedAttachment1 => "پیوست امضاشده ۱",
        LoanDocumentType.SignedAttachment2 => "پیوست امضاشده ۲",
        LoanDocumentType.SignedAttachment3 => "پیوست امضاشده ۳",
        LoanDocumentType.SignedAttachment4 => "پیوست امضاشده ۴",
        LoanDocumentType.SignedAttachment5 => "پیوست امضاشده ۵",
        LoanDocumentType.SignedAttachment6 => "پیوست امضاشده ۶",
        LoanDocumentType.FinalContract => "قرارداد نهایی",
        LoanDocumentType.PaymentReceipt => "رسید پرداخت",
        LoanDocumentType.SignedApprovalFormFirstPage => "فرم تصویب امضاشده — صفحه اول",
        LoanDocumentType.CreditCommitteeMinutes => "صورتجلسه کمیته اعتباری",
        LoanDocumentType.SiteVisitReport => "گزارش بازدید از محل",
        LoanDocumentType.Other => "سایر",
        _ => type.ToString()
    };

    public static string FormatDataEntryDocumentsIncompleteMessage(IEnumerable<LoanDocumentType> missing)
        => "مدارک الزامی هنوز کامل بارگذاری نشده است: "
           + string.Join("، ", missing.Select(ToPersianLabel));
}
