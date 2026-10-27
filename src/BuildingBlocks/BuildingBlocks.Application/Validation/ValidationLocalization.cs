using System.Globalization;
using FluentValidation;

namespace BuildingBlocks.Application.Validation;

public static class ValidationLocalization
{
    private static readonly IReadOnlyDictionary<string, string> PropertyDisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplicantType"] = "نوع متقاضی",
            ["Company"] = "شرکت",
            ["Name"] = "نام",
            ["EconomicCode"] = "کد اقتصادی",
            ["RegistrationNumber"] = "شماره ثبت",
            ["NationalId"] = "شناسه ملی",
            ["PhoneNumber"] = "شماره موبایل",
            ["Address"] = "آدرس",
            ["City"] = "شهر",
            ["Province"] = "استان",
            ["PostalCode"] = "کد پستی",
            ["LegalType"] = "نوع شرکت به لحاظ حقوقی",
            ["TechnologyType"] = "نوع شرکت به لحاظ فناوری",
            ["TechnologyDomain"] = "حوزه فناوری",
            ["RepresentativeName"] = "نام نماینده",
            ["RepresentativePhone"] = "شماره تماس نماینده",
            ["CeoName"] = "نام مدیرعامل",
            ["CeoPhone"] = "شماره تماس مدیرعامل",
            ["BusinessStage"] = "مرحله کسب‌وکار",
            ["RequestedAmount"] = "سرمایه مورد نیاز",
            ["Website"] = "وب‌سایت",
            ["Country"] = "کشور",
            ["Industry"] = "صنعت",
            ["InvestmentAttractionBasis"] = "مبنای درخواست جذب سرمایه‌گذار",
            ["Risks"] = "ریسک‌ها",
            ["GoToMarketStrategy"] = "استراتژی ورود به بازار",
            ["Phase"] = "مرحله",
            ["Message"] = "پیام",
            ["Comment"] = "توضیح",
            ["DocumentType"] = "نوع سند",
            ["FileName"] = "نام فایل",
            ["MimeType"] = "نوع فایل",
            ["FileSize"] = "حجم فایل",
            ["S3Key"] = "کلید فایل",
            ["Version"] = "نسخه",
            ["Amount"] = "مبلغ",
            ["TransactionNumber"] = "شماره تراکنش",
            ["ReceiptS3Key"] = "کلید رسید",
            ["Notes"] = "یادداشت",
            ["BankName"] = "نام بانک",
            ["IBAN"] = "شماره شبا",
            ["Iban"] = "شماره شبا",
            ["ApprovedAmount"] = "مبلغ تأییدشده",
            ["PaymentSchedule"] = "زمان‌بندی پرداخت",
            ["Title"] = "عنوان",
            ["Items"] = "آیتم‌ها",
            ["OtpCode"] = "کد تایید",
            ["FirstName"] = "نام",
            ["LastName"] = "نام خانوادگی",
            ["NationalCode"] = "کد ملی",
            ["Email"] = "ایمیل"
        };

    public static void ConfigurePersian()
    {
        ValidatorOptions.Global.LanguageManager = new PersianLanguageManager();
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
        {
            if (member is null)
                return string.Empty;

            return PropertyDisplayNames.TryGetValue(member.Name, out var displayName)
                ? displayName
                : member.Name;
        };
    }

    public static string ToErrorMessage(this FluentValidation.Results.ValidationResult validation) =>
        string.Join("؛ ", validation.Errors.Select(error => error.ErrorMessage));
}
