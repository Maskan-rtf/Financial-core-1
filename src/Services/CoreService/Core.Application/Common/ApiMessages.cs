namespace Core.Application.Common;

public static class ApiMessages
{
    public const string ValidationFailed = "اطلاعات ارسالی نامعتبر است.";
    public const string UnexpectedError = "خطای غیرمنتظره رخ داد.";
    public const string ConcurrencyConflict = "این پرونده توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید و دوباره تلاش کنید.";
    public const string NotAllowed = "شما به این عملیات دسترسی ندارید.";
    public const string AuthenticationRequired = "برای ادامه باید وارد شوید.";
    public const string CaseNotFound = "پرونده سرمایه‌گذاری یافت نشد.";
    public const string PaymentNotFound = "رکورد پرداخت یافت نشد.";
    public const string CommentNotFound = "نظر یافت نشد.";
    public const string DocumentNotFound = "سند یافت نشد.";
    public const string UploadedFileNotFound = "فایل بارگذاری‌شده در سیستم یافت نشد.";
    public const string UploadedFileEmpty = "فایل بارگذاری‌شده خالی است و قابل ثبت نیست.";
    public const string DocumentAlreadyExists = "این سند قبلاً ثبت شده است.";
    public const string DocumentAlreadyRegistered = "این سند قبلاً ثبت شده است.";
    public const string InvalidFileName = "نام فایل نامعتبر است.";
    public const string InvalidDocumentType = "نوع سند نامعتبر است.";
    public const string FileTypeNotAllowed = "نوع فایل مجاز نیست.";
    public const string InvalidDocumentKey = "کلید سند نامعتبر است.";
    public const string InvalidAttachmentKey = "کلید پیوست نامعتبر است.";
    public const string CompanyRequiredForCompanyApplicant = "برای متقاضی حقوقی، انتخاب شرکت الزامی است.";
    public const string CompanyNotFound = "شرکت انتخاب‌شده یافت نشد.";
    public const string CompanyAccessDenied = "به این شرکت دسترسی ندارید.";
    public const string CompanyHasLinkedCases = "این شرکت دارای پرونده فعال است و امکان حذف آن وجود ندارد.";
    public const string DataEntry1NotEditable = "در وضعیت فعلی امکان ویرایش ورود اطلاعات ۱ وجود ندارد.";
    public const string DataEntry2NotEditable = "در وضعیت فعلی امکان ویرایش ورود اطلاعات ۲ وجود ندارد.";
    public const string FinancialWorksheetNotEditable = "در وضعیت فعلی امکان ویرایش کاربرگ مالی وجود ندارد.";
    public const string DataEntry1NotCurrentPhase = "مرحله فعلی پرونده، ورود اطلاعات ۱ نیست.";
    public const string DataEntry2NotCurrentPhase = "مرحله فعلی پرونده، ورود اطلاعات ۲ نیست.";
    public const string FinancialWorksheetNotCurrentPhase = "مرحله فعلی پرونده، کاربرگ مالی نیست.";
    public const string PaymentPhaseMismatch = "پرونده در مرحله پردازش پرداخت نیست.";
    public const string PaymentsOnlyInWaitingPayment = "ثبت پرداخت فقط پس از تأیید مدیرعامل و در وضعیت «انتظار پرداخت» امکان‌پذیر است.";
    public const string CeoApprovalRequiredForPayment = "تا زمانی که مدیرعامل پرونده را تأیید نکرده، ورود به مرحله پرداخت ممکن نیست.";
    public const string ApprovalMustMatchPhase = "تأیید باید با مرحله فعلی پرونده هم‌خوان باشد.";
    public const string RevisionMustMatchPhase = "درخواست اصلاح باید با مرحله فعلی پرونده هم‌خوان باشد.";
    public const string ApplicantsCannotCreateInternalComments = "متقاضی نمی‌تواند نظر داخلی ثبت کند.";
    public const string ApplicantsCannotRequestRevisions = "متقاضی نمی‌تواند درخواست اصلاح ثبت کند.";
    public const string RevisionMessageRequired = "پیام درخواست اصلاح الزامی است.";
    public const string FreeformCommentNotAllowed = "در این مرحله امکان ثبت نظر آزاد وجود ندارد.";
    public const string DocumentUploadNotAllowed = "در وضعیت فعلی امکان بارگذاری سند وجود ندارد.";
    public const string DocumentConfirmationNotAllowed = "در وضعیت فعلی امکان تأیید سند وجود ندارد.";
    public const string CannotTransitionFromTerminalState = "از وضعیت پایانی نمی‌توان به مرحله بعد رفت.";
    public const string OnlyAdminCanArchive = "فقط مدیر سیستم می‌تواند پرونده را بایگانی کند.";
    public const string InvalidTransition = "انتقال وضعیت درخواستی مجاز نیست.";
    public const string CaseEntityIsNull = "پرونده برای پردازش در دسترس نیست.";
    public const string CannotSubmitDataEntry1BeforeSave = "ابتدا ورود اطلاعات ۱ را ذخیره کنید.";
    public const string DataEntry1Incomplete = "ورود اطلاعات ۱ ناقص است.";
    public const string ApplicantProfileIncomplete =
        "نام، نام خانوادگی و ایمیل در پروفایل کاربر باید تکمیل شود؛ سپس فرم را ذخیره کنید.";
    public const string DataEntry1PitchDeckRequired = "بارگذاری پیچ‌دک برای ارسال فرم اولیه الزامی است.";
    public const string CannotSubmitDataEntry2BeforeSave = "ابتدا ورود اطلاعات ۲ را ذخیره کنید.";
    public const string DataEntry2Incomplete = "ورود اطلاعات ۲ ناقص است.";
    public const string DataEntry2DocumentsIncomplete = "مدارک الزامی فرم تکمیلی هنوز کامل بارگذاری نشده است.";
    public const string PreliminaryContractMissing = "برای ادامه، سند پیش‌قرارداد بارگذاری نشده است.";
    public const string SignedContractMissing = "برای ادامه، سند قرارداد امضاشده بارگذاری نشده است.";
    public const string FinancialWorksheetMissingOrInvalid = "کاربرگ مالی ثبت نشده یا مبلغ تأییدشده نامعتبر است.";
    public const string ApprovedAmountNotSet = "برای تکمیل پرونده، مبلغ تأییدشده مشخص نشده است.";
    public const string PaymentsIncomplete = "برای تکمیل پرونده، پرداخت‌ها کامل نشده‌اند.";
    public const string ValuationStatusMismatch = "ثبت ارزش‌گذاری فقط در وضعیت {0} امکان‌پذیر است.";
    public const string CaseNumberAllocationFailed = "امکان تخصیص شماره یکتای پرونده وجود ندارد.";

    public const string GuaranteeCaseNotFound = "پرونده ضمانت‌نامه یافت نشد.";
    public const string GuaranteeRenewalNotFound = "درخواست تمدید یافت نشد.";
    public const string GuaranteeApplicationIncomplete = "اطلاعات درخواست ضمانت‌نامه ناقص است.";
    public const string GuaranteeDocumentsIncomplete = "مدارک الزامی هنوز کامل بارگذاری نشده است.";
    public const string GuaranteeApprovalFormIncomplete = "فرم تصویب تکمیل نشده است.";
    public const string GuaranteeDraftContractMissing = "پیش‌قرارداد بارگذاری نشده است.";
    public const string GuaranteeSignedContractMissing = "قرارداد امضاشده بارگذاری نشده است.";
    public const string GuaranteeFinalContractMissing = "قرارداد نهایی بارگذاری نشده است.";
    public const string GuaranteeAmendmentContractMissing = "قرارداد اصلاحیه بارگذاری نشده است.";
    public const string GuaranteeIssuanceDocumentsIncomplete = "ضمانت‌نامه یا رسید صدور بارگذاری نشده است.";
    public const string GuaranteeApplicationNotEditable = "در وضعیت فعلی امکان ویرایش درخواست وجود ندارد.";
    public const string GuaranteeCancellationNotEligible = "ابطال ضمانت‌نامه فقط برای پرونده ضمانت‌نامه تکمیل‌شده مجاز است.";
    public const string GuaranteeCancellationAlreadyActive = "برای این پرونده یک فرایند اصلاحیه/ابطال فعال یا تکمیل‌شده ثبت شده است.";
    public const string GuaranteeCancellationDataIncomplete = "اطلاعات ابطال ضمانت‌نامه کامل نیست.";
    public const string GuaranteeCancellationDocumentsIncomplete = "مدارک الزامی ابطال ضمانت‌نامه کامل نیست.";
    public const string GuaranteeCancellationOriginalReferenceRequired = "مرجع ضمانت‌نامه اصلی برای ابطال ضمانت‌نامه الزامی است.";
    public const string GuaranteeCancellationRequiresLegalOverride = "ابطال ضمانت‌نامه دارای تعهد فعال است و بدون تایید حقوقی با گزینه رفع تعهد قابل انجام نیست.";
    public const string ParentGuaranteeNotEligibleForRenewal = "پرونده والد برای تمدید واجد شرایط نیست.";
    public const string GuaranteeRenewalDeprecatedUseAmendment = "فرآیند تمدید مستقل غیرفعال شده است. برای تمدید یا تقلیل از اصلاحیه پرونده ضمانت‌نامه استفاده کنید.";
    public const string ApplicantCreditLimitNotSet = "سقف اعتبار برای این متقاضی هنوز توسط مدیرعامل تعیین نشده است.";
    public const string FundCreditLimitNotSet = "سقف اعتبار کل صندوق هنوز توسط مدیرعامل تعیین نشده است.";
    public const string FundCreditLimitExpired = "سقف اعتبار صندوق منقضی شده است. مدیرعامل باید سقف جدید با بازه جدید تعیین کند.";
    public const string FundCreditLimitNotYetActive = "سقف اعتبار صندوق هنوز در بازه زمانی فعال نیست.";
    public const string InvalidFundCreditLimitPeriod = "تاریخ پایان سقف باید بعد از تاریخ شروع باشد.";
    public const string FundCreditLimitPeriodOverlap =
        "بازه زمانی انتخاب‌شده با یک سقف دوره‌ای دیگر برای همین ماژول همپوشانی دارد.";
    public const string FundCreditLimitNotFound = "سقف اعتبار دوره‌ای یافت نشد.";
    public const string FundCreditLimitHasUtilization =
        "این سقف دوره‌ای دارای مصرف است و امکان حذف آن وجود ندارد.";
    public const string FundCreditLimitBelowUtilization =
        "مبلغ سقف نمی‌تواند کمتر از مصرف فعلی ({0} ریال) باشد.";
    public const string GuaranteeApprovalFormAmountRequired = "مبلغ ضمانت‌نامه در فرم تصویب مشخص نشده است.";
    public const string FundCreditLimitExceededFormat =
        "مجموع ضمانت‌نامه‌های صادره در بازه سقف ({0} ریال) و تعهدات فعال ({1} ریال) از سقف تعیین‌شده توسط مدیرعامل ({2} ریال) بیشتر است.";
    public const string InvalidCreditLimitAmount = "مبلغ سقف اعتبار باید بزرگ‌تر از صفر باشد.";
    public const string CreditLimitAmountTooLarge =
        "مبلغ سقف از حد مجاز سیستم بیشتر است (حداکثر ۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٬۹۹۹٫۹۹ ریال).";
    public const string CreditLimitDatabasePrecisionTooSmall =
        "ستون سقف در دیتابیس ظرفیت مبلغ را ندارد. اسکریپت scripts/FixGuaranteeFundCreditLimitPrecision.sql را اجرا کنید.";
    public const string OnlyCeoCanSetCreditLimit = "تعیین سقف اعتبار فقط توسط مدیرعامل امکان‌پذیر است.";

    public const string LoanCaseNotFound = "پرونده تسهیلات یافت نشد.";
    public const string LoanApplicationIncomplete = "اطلاعات درخواست تسهیلات ناقص است.";
    public const string LoanApprovalDetailIncomplete = "فرم تصویب تکمیل نشده است.";
    public const string LoanApplicationNotEditable = "در وضعیت فعلی امکان ویرایش درخواست وجود ندارد.";
    public const string LoanRawContractMissing = "قرارداد خام بارگذاری نشده است.";
    public const string LoanInstallmentsMissing = "جدول اقساط ثبت نشده است.";
    public const string LoanSignedContractMissing = "قرارداد امضاشده بارگذاری نشده است.";
    public const string LoanFinalContractMissing = "قرارداد نهایی بارگذاری نشده است.";
    public const string LoanApprovalDetailNotEditable = "در وضعیت فعلی امکان ویرایش فرم تصویب وجود ندارد.";
    public const string LoanInstallmentsNotEditable = "در وضعیت فعلی امکان ویرایش اقساط وجود ندارد.";
    public const string LoanRepaymentIncomplete = "همه اقساط هنوز پرداخت نشده‌اند.";

    public const string InvalidCaseModule = "ماژول پرونده نامعتبر است.";
    public const string InvalidTargetStage = "مرحله هدف نامعتبر است.";
    public const string CannotRollbackToSameStage = "مرحله فعلی و مرحله هدف یکسان هستند.";
    public const string CannotAdvanceCaseStage = "فقط امکان بازگشت به مراحل قبلی وجود دارد؛ پیشروی به مرحله جلوتر مجاز نیست.";
    public const string TargetStageNotVisited = "پرونده قبلاً در مرحله هدف نبوده است.";
    public const string CaseStageRollbackCommentRequired = "ثبت دلیل بازگشت مرحله الزامی است.";
    public const string OnlyAdminOrTechnicalCanRollbackStage = "فقط مدیر سیستم یا واحد فنی می‌توانند مرحله پرونده را بازگردانند.";
}
