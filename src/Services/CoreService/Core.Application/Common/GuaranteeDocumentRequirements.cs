using Core.Domain.Entities;
using Core.Domain.Enums;

namespace Core.Application.Common;

public static class GuaranteeDocumentRequirements
{
    public static readonly GuaranteeDocumentType[] RequiredForDataEntrySubmit =
    [
        GuaranteeDocumentType.EstablishmentGazette,
        GuaranteeDocumentType.FinancialStatements3Years,
        GuaranteeDocumentType.ActivityLicenses,
        GuaranteeDocumentType.BankAccountTurnover,
        GuaranteeDocumentType.CreditInformationForm,
        GuaranteeDocumentType.CaseFormationFeeReceipt,
        GuaranteeDocumentType.GuaranteeIssuanceRequestForm,
        GuaranteeDocumentType.CeoBoardIdCards
    ];

    public static IEnumerable<GuaranteeDocumentType> GetRequiredForDataEntrySubmit(GuaranteeType? guaranteeType)
    {
        foreach (var doc in RequiredForDataEntrySubmit)
            yield return doc;

        if (guaranteeType == GuaranteeType.Tender)
            yield return GuaranteeDocumentType.TenderAnnouncement;

        if (guaranteeType is GuaranteeType.PerformanceBond or GuaranteeType.AdvancePayment)
            yield return GuaranteeDocumentType.BaseContractImage;
    }

    public static IReadOnlyList<GuaranteeDocumentType> GetMissingForDataEntrySubmit(
        GuaranteeType? guaranteeType,
        IEnumerable<GuaranteeCaseDocument> documents)
    {
        var present = documents
            .Where(d => !d.IsDeleted)
            .Select(d => d.DocumentType)
            .ToHashSet();

        return GetRequiredForDataEntrySubmit(guaranteeType)
            .Where(t => !present.Contains(t))
            .ToList();
    }

    public static string ToPersianLabel(GuaranteeDocumentType type) => type switch
    {
        GuaranteeDocumentType.EstablishmentGazette => "آگهی تاسیس و آخرین روزنامه رسمی هیئت‌مدیره",
        GuaranteeDocumentType.FinancialStatements3Years => "صورت‌های مالی 3 سال گذشته",
        GuaranteeDocumentType.ActivityLicenses => "تصویر مجوزهای اصلی فعالیت",
        GuaranteeDocumentType.BankAccountTurnover => "گردش حساب‌های بانکی فعال شرکت",
        GuaranteeDocumentType.CreditInformationForm => "فرم دریافت اطلاعات اعتباری",
        GuaranteeDocumentType.CaseFormationFeeReceipt => "فیش مبلغ تشکیل پرونده",
        GuaranteeDocumentType.GuaranteeIssuanceRequestForm => "فرم درخواست صدور ضمانت‌نامه",
        GuaranteeDocumentType.CeoBoardIdCards => "کارت ملی و شناسنامه مدیرعامل و اعضای هیئت‌مدیره",
        GuaranteeDocumentType.TenderAnnouncement => "تصویر آگهی مناقصه/مزایده",
        GuaranteeDocumentType.BaseContractImage => "تصویر قرارداد پایه",
        GuaranteeDocumentType.CaseFormationAttachmentForm => "فرم پیوست جهت تشکیل پرونده",
        GuaranteeDocumentType.CompanyIntroductionDocs => "اسناد معرفی شرکت",
        GuaranteeDocumentType.LeaseOrOwnershipDeed => "قرارداد اجاره یا سند مالکیت محل شرکت",
        GuaranteeDocumentType.FeasibilityReport => "گزارش امکان‌سنجی فنی و اقتصادی",
        GuaranteeDocumentType.SalesContractsScan => "اسکن قراردادهای فروش و تاییدیه‌ها",
        GuaranteeDocumentType.CreditInquiryResult => "استعلام اعتباری شرکت و مدیران",
        GuaranteeDocumentType.DraftContract => "پیش‌قرارداد (قرارداد خام)",
        GuaranteeDocumentType.SignedContract => "قرارداد امضاشده",
        GuaranteeDocumentType.SignedAttachment1 => "پیوست امضاشده 1",
        GuaranteeDocumentType.SignedAttachment2 => "پیوست امضاشده 2",
        GuaranteeDocumentType.SignedAttachment3 => "پیوست امضاشده 3",
        GuaranteeDocumentType.SignedAttachment4 => "پیوست امضاشده 4",
        GuaranteeDocumentType.SignedAttachment5 => "پیوست امضاشده 5",
        GuaranteeDocumentType.SignedAttachment6 => "پیوست امضاشده 6",
        GuaranteeDocumentType.FinalContract => "قرارداد نهایی",
        GuaranteeDocumentType.GuaranteeInstrument => "ضمانت‌نامه صادره",
        GuaranteeDocumentType.IssuanceReceipt => "رسید صدور",
        GuaranteeDocumentType.BeneficiaryReleaseLetter => "نامه رفع تعهد ذی‌نفع",
        GuaranteeDocumentType.OriginalGuaranteeReference => "مرجع ضمانت‌نامه اصلی",
        GuaranteeDocumentType.SettlementConfirmation => "تاییدیه تسویه",
        GuaranteeDocumentType.AmendmentContract => "قرارداد اصلاحیه",
        GuaranteeDocumentType.Other => "سایر",
        _ => type.ToString()
    };

    public static string FormatDataEntryDocumentsIncompleteMessage(IEnumerable<GuaranteeDocumentType> missing)
    {
        var labels = missing.Select(ToPersianLabel).ToList();
        if (labels.Count == 0)
            return ApiMessages.GuaranteeDocumentsIncomplete;

        return $"{ApiMessages.GuaranteeDocumentsIncomplete} موارد ناقص: {string.Join("، ", labels)}.";
    }

    public static readonly GuaranteeDocumentType[] RequiredForSignedPackageSubmit =
    [
        GuaranteeDocumentType.SignedContract
    ];

    public static readonly GuaranteeDocumentType[] RequiredForIssuance =
    [
        GuaranteeDocumentType.GuaranteeInstrument,
        GuaranteeDocumentType.IssuanceReceipt
    ];

    public static IReadOnlyList<GuaranteeDocumentType> GetMissingForCancellation(GuaranteeCase caseEntity)
    {
        var required = new List<GuaranteeDocumentType>
        {
            GuaranteeDocumentType.BeneficiaryReleaseLetter
        };

        var present = caseEntity.Documents
            .Where(d => !d.IsDeleted)
            .Select(d => d.DocumentType)
            .ToHashSet();

        return required.Where(t => !present.Contains(t)).ToList();
    }

    public static bool HasAmendmentContract(IEnumerable<GuaranteeCaseDocument> documents)
        => documents.Any(x => !x.IsDeleted && x.DocumentType == GuaranteeDocumentType.AmendmentContract);
}
