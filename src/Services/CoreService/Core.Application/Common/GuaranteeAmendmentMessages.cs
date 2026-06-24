namespace Core.Application.Common;

public static class GuaranteeAmendmentMessages
{
    public const string Incomplete = "اطلاعات الحاقیه ضمانت‌نامه ناقص است.";
    public const string NotFound = "اطلاعات الحاقیه برای این پرونده یافت نشد.";
    public const string NotEditable = "در وضعیت فعلی امکان ویرایش الحاقیه وجود ندارد.";
    public const string OnlyForCompletedCases = "الحاقیه فقط برای پرونده‌های تکمیل‌شده مجاز است.";
    public const string ExtensionDateRequired = "در تمدید ضمانت‌نامه، تاریخ نهایی جدید الزامی است.";
    public const string ExtensionDateMustExtend = "تاریخ جدید باید بعد از تاریخ فعلی انقضا باشد.";
    public const string ReductionAmountRequired = "در تقلیل ضمانت‌نامه، مبلغ جدید الزامی است.";
    public const string ReductionAmountInvalid = "مبلغ تقلیل باید بزرگتر از صفر و کمتر یا مساوی مبلغ اصلی ضمانت‌نامه باشد.";
    public const string SuccessCreated = "الحاقیه ضمانت‌نامه ثبت شد.";
    public const string SuccessRetrieved = "اطلاعات الحاقیه دریافت شد.";
    public const string SuccessSubmitted = "الحاقیه ضمانت‌نامه برای بررسی ارسال شد.";
    public const string SuccessApproved = "الحاقیه ضمانت‌نامه تأیید شد.";
    public const string SuccessRejected = "الحاقیه ضمانت‌نامه رد شد.";
    public const string SuccessRevisionRequested = "درخواست اصلاح الحاقیه برای متقاضی ثبت شد.";
}
