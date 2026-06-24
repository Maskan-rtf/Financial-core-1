/* global WorkflowModel */
(function () {
  const MODULE = { Investment: 1, Guarantee: 2 };

  const STATUS_BY_KEY = {
    Draft: 1,
    DataEntry: 2,
    CreditReview: 3,
    ApprovalFormEntry: 4,
    CeoApprovalInitial: 5,
    WaitingDraftContract: 6,
    WaitingSignedContractAndAttachments: 7,
    FinancialAttachmentReview: 8,
    WaitingFinalContract: 9,
    CeoApprovalFinal: 10,
    WaitingIssuanceDocuments: 11,
    Completed: 12,
    Rejected: 13,
    Cancelled: 14,
    Archived: 15,
    AmendmentDraft: 16,
    AmendmentDataEntry: 17,
    AmendmentCreditReview: 18,
    AmendmentCeoApproval: 19,
    AmendmentLegalReview: 20,
    AmendmentCompleted: 21,
    AmendmentApproved: 22,
    AmendmentRejected: 23,
  };

  function coerceStatus(status) {
    if (status == null || status === "") return 0;
    const n = Number(status);
    if (!Number.isNaN(n)) return n;
    return STATUS_BY_KEY[String(status)] ?? 0;
  }

  const STEPS = {
    1: { id: 1, title: "پیش‌نویس", unit: "applicant", phase: 1 },
    2: { id: 2, title: "ورود اطلاعات", unit: "applicant", phase: 1 },
    3: { id: 3, title: "بررسی اعتبارات", unit: "credit", phase: 2 },
    4: { id: 4, title: "فرم تصویب", unit: "credit", phase: 2 },
    5: { id: 5, title: "تأیید مدیرعامل (اول)", unit: "ceo", phase: 2 },
    6: { id: 6, title: "پیش‌قرارداد", unit: "legal", phase: 3 },
    7: { id: 7, title: "امضا و پیوست", unit: "applicant", phase: 3 },
    8: { id: 8, title: "بررسی مالی", unit: "financial", phase: 4 },
    9: { id: 9, title: "قرارداد نهایی", unit: "legal", phase: 3 },
    10: { id: 10, title: "تأیید مدیرعامل (نهایی)", unit: "ceo", phase: 4 },
    11: { id: 11, title: "صدور", unit: "financial", phase: 4 },
    12: { id: 12, title: "تکمیل", unit: "all", phase: 5 },
    13: { id: 13, title: "رد شده", unit: "all", phase: 5 },
    14: { id: 14, title: "لغو شده", unit: "all", phase: 5 },
    15: { id: 15, title: "بایگانی", unit: "all", phase: 5 },
    16: { id: 16, title: "اصلاحیه پیش‌نویس", unit: "applicant", phase: 5 },
    17: { id: 17, title: "اصلاحیه ثبت اطلاعات", unit: "applicant", phase: 5 },
    18: { id: 18, title: "اصلاحیه در انتظار بررسی", unit: "credit", phase: 5 },
    19: { id: 19, title: "تأیید مدیرعامل (اصلاحیه)", unit: "ceo", phase: 5 },
    20: { id: 20, title: "اصلاحیه حقوقی", unit: "legal", phase: 5 },
    21: { id: 21, title: "اصلاحیه تکمیل", unit: "all", phase: 5 },
    22: { id: 22, title: "اصلاحیه تایید شد", unit: "all", phase: 5 },
    23: { id: 23, title: "اصلاحیه رد شد", unit: "all", phase: 5 },
  };

  const CANCELLATION_STEP_TITLES = {
    14: "تکمیل ابطال",
    16: "پیش‌نویس ابطال",
    17: "ثبت اطلاعات ابطال",
    18: "بررسی اعتبارات (ابطال)",
    19: "تأیید مدیرعامل (ابطال)",
    20: "بررسی حقوقی ابطال",
    21: "تکمیل ابطال",
    22: "تکمیل ابطال",
    23: "ابطال رد شد",
  };

  const AMENDMENT_TYPE_BY_KEY = {
    Extension: 1,
    Reduction: 2,
    Cancellation: 3,
  };

  function coerceAmendmentType(value) {
    if (value == null || value === "") return 0;
    const n = Number(value);
    if (!Number.isNaN(n) && n > 0) return n;
    return AMENDMENT_TYPE_BY_KEY[String(value)] ?? 0;
  }

  function pickField(obj, camel, pascal) {
    if (!obj) return "";
    const v = obj[camel] ?? obj[pascal];
    return v == null ? "" : v;
  }

  function amendmentTypeFromCase(caseRow) {
    if (!caseRow) return 0;
    const amendment = caseRow.amendment || caseRow.Amendment;
    const fromAmendment = amendment
      ? pickField(amendment, "amendmentType", "AmendmentType")
      : "";
    const fromTop = pickField(caseRow, "amendmentType", "AmendmentType");
    return coerceAmendmentType(fromAmendment || fromTop);
  }

  function isCancellationWorkflow(amendmentType) {
    return Number(amendmentType || 0) === 3;
  }

  const MAIN_WORKFLOW_STEP_IDS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  const AMENDMENT_WORKFLOW_STEP_IDS = [16, 17, 18, 19, 20, 22];
  const CANCELLATION_WORKFLOW_STEP_IDS = [16, 17, 18, 19, 14];

  function stepForStatus(status, context) {
    const value = coerceStatus(status);
    const amendmentType = Number(context?.amendmentType || 0);
    if (isCancellationWorkflow(amendmentType) && CANCELLATION_STEP_TITLES[value]) {
      const base = STEPS[value] || { id: value, unit: "all", phase: 5 };
      return { ...base, title: CANCELLATION_STEP_TITLES[value] };
    }
    return STEPS[value] || { id: value, title: "نامشخص", unit: "all", phase: 0 };
  }

  /** مدارک ورود اطلاعات — مطابق mockup ضمانت‌نامه */
  const DATA_ENTRY_DOCUMENTS = [
    { type: 8, label: "فیش مبلغ تشکیل پرونده", hint: "ضروری — حداکثر ۱۰ مگابایت", required: true },
    { type: 9, label: "فرم پیوست جهت تشکیل پرونده", hint: "اختیاری", required: false },
    { type: 7, label: "فرم دریافت اطلاعات اعتباری (حقوقی و حقیقی)", hint: "ضروری", required: true },
    { type: 10, label: "اسناد معرفی شرکت (تابلو، چارت سازمانی و …)", hint: "اختیاری", required: false },
    { type: 2, label: "آگهی تأسیس و آخرین روزنامه رسمی هیئت‌مدیره", hint: "ضروری", required: true },
    { type: 12, label: "کارت ملی و شناسنامه مدیرعامل و اعضای هیئت‌مدیره", hint: "ضروری", required: true },
    { type: 11, label: "قرارداد اجاره یا سند مالکیت محل شرکت", hint: "اختیاری", required: false },
    { type: 3, label: "صورت‌های مالی ۳ سال گذشته", hint: "ضروری", required: true },
    { type: 5, label: "گردش حساب‌های بانکی فعال شرکت (یک سال گذشته)", hint: "ضروری", required: true },
    { type: 4, label: "تصویر مجوزهای اصلی فعالیت", hint: "ضروری", required: true },
    { type: 13, label: "گزارش امکان‌سنجی فنی و اقتصادی (FS یا BP)", hint: "اختیاری", required: false },
    { type: 15, label: "اسکن قراردادهای فروش و تأییدیه‌ها", hint: "اختیاری", required: false },
    { type: 14, label: "فرم درخواست صدور ضمانت‌نامه", hint: "ضروری", required: true },
    { type: 6, label: "استعلام اعتباری شرکت و مدیران (سامانه ایران)", hint: "اختیاری", required: false },
    { type: 16, label: "تصویر آگهی مناقصه/مزایده", hint: "ضروری — برای نوع «شرکت در مناقصه»", required: false, whenGuaranteeType: 1 },
    { type: 17, label: "تصویر قرارداد پایه", hint: "ضروری برای حسن انجام کار / پیش‌پرداخت", required: false, whenGuaranteeTypes: [2, 3] },
  ];

  const GUARANTEE_TYPES = [
    { value: 1, label: "شرکت در مناقصه" },
    { value: 2, label: "حسن انجام کار" },
    { value: 3, label: "پیش‌پرداخت" },
    { value: 4, label: "تعهد پرداخت" },
    { value: 99, label: "سایر" },
  ];

  const APPLICANT_CATEGORIES = [
    { value: 1, label: "دانش‌بنیان" },
    { value: 2, label: "خلاق" },
    { value: 4, label: "فناور" },
    { value: 8, label: "سایر" },
  ];

  const APPLICANT_LEGAL_FORMS = [
    { value: 1, label: "دولتی" },
    { value: 2, label: "خصوصی" },
    { value: 3, label: "دانش‌بنیان" },
    { value: 4, label: "تعاونی" },
    { value: 99, label: "سایر" },
  ];

  const BENEFICIARY_COMPANY_TYPES = [
    { value: 1, label: "دولتی" },
    { value: 2, label: "عمومی غیردولتی" },
    { value: 3, label: "خصوصی" },
    { value: 99, label: "سایر" },
  ];

  const PHASES = {
    1: "درخواست",
    2: "اعتبارات",
    3: "حقوقی",
    4: "مالی",
    5: "اختتام",
  };

  /** مدارک مراحل گردش کار (غیر از ورود اطلاعات) */
  const WORKFLOW_STAGE_DOCUMENTS = {
    6: {
      title: "بارگذاری پیش‌قرارداد",
      subtitle: "واحد حقوقی — قرارداد خام",
      autoAdvanceHint: "پس از بارگذاری موفق، پرونده معمولاً خودکار به مرحله «امضا و پیوست» می‌رود.",
      docs: [{ type: 18, label: "پیش‌قرارداد (قرارداد خام)", hint: "PDF یا Word", required: true }],
    },
    7: {
      title: "بارگذاری قرارداد امضاشده و پیوست‌ها",
      subtitle: "متقاضی",
      autoAdvanceHint: "قرارداد امضاشده الزامی است؛ پس از بارگذاری «ارسال قرارداد امضاشده» را بزنید.",
      docs: [
        { type: 19, label: "قرارداد امضاشده", hint: "ضروری", required: true },
        { type: 20, label: "پیوست امضاشده ۱", required: false },
        { type: 21, label: "پیوست امضاشده ۲", required: false },
        { type: 22, label: "پیوست امضاشده ۳", required: false },
        { type: 23, label: "پیوست امضاشده ۴", required: false },
        { type: 24, label: "پیوست امضاشده ۵", required: false },
        { type: 25, label: "پیوست امضاشده ۶", required: false },
      ],
    },
    9: {
      title: "بارگذاری قرارداد نهایی",
      subtitle: "واحد حقوقی",
      autoAdvanceHint: "پس از بارگذاری موفق، پرونده معمولاً خودکار به تأیید مدیرعامل (نهایی) می‌رود.",
      docs: [{ type: 26, label: "قرارداد نهایی", hint: "ضروری", required: true }],
    },
    11: {
      title: "بارگذاری مدارک صدور",
      subtitle: "واحد مالی",
      autoAdvanceHint: "هر دو مدرک الزامی است؛ سپس «تأیید صدور» را بزنید.",
      docs: [
        { type: 27, label: "ضمانت‌نامه صادره", required: true },
        { type: 28, label: "رسید صدور", required: true },
      ],
    },
    20: {
      title: "بارگذاری قرارداد اصلاحیه",
      subtitle: "واحد حقوقی",
      autoAdvanceHint: "پس از بارگذاری قرارداد اصلاحیه، «تأیید اصلاحیه» را بزنید.",
      docs: [{ type: 32, label: "قرارداد اصلاحیه", hint: "PDF یا Word", required: true }],
    },
  };

  const UNITS = [
    { id: "all", label: "همه" },
    { id: "applicant", label: "متقاضی", roles: ["Applicant", "User", "Admin"] },
    { id: "credit", label: "اعتبارات", roles: ["CreditExpert", "CreditManager", "Admin"] },
    { id: "legal", label: "حقوقی", roles: ["LegalExpert", "LegalManager", "LegalUnit", "Admin"] },
    { id: "financial", label: "مالی", roles: ["FinancialExpert", "FinancialManager", "FinancialUnit", "Admin"] },
    { id: "ceo", label: "مدیرعامل", roles: ["CEO", "Admin"] },
  ];

  function normalizeRole(roleText, roleNumber) {
    if (window.WorkflowModel && typeof window.WorkflowModel.normalizeRole === "function") {
      return window.WorkflowModel.normalizeRole(roleText, roleNumber);
    }
    return String(roleText || "").trim();
  }

  function getUnit(unitId) {
    return UNITS.find((u) => u.id === unitId) || null;
  }

  function roleMatchesUnit(role, unitId) {
    const unit = getUnit(unitId);
    if (!unit || unit.id === "all") return true;
    const normalized = normalizeRole(role);
    return (unit.roles || []).some((allowed) => allowed === normalized);
  }

  function canActOnCase(role, unitId) {
    const normalized = normalizeRole(role);
    if (normalized === "Admin") return true;
    return roleMatchesUnit(normalized, unitId);
  }

  function unitRoleLabels(unitId) {
    const unit = getUnit(unitId);
    if (!unit) return "";
    const labels = {
      applicant: "متقاضی (Applicant)",
      credit: "واحد اعتبارات (CreditExpert / CreditManager)",
      legal: "واحد حقوقی (LegalExpert / LegalManager)",
      financial: "واحد مالی (FinancialExpert / FinancialManager)",
      ceo: "مدیرعامل (CEO)",
    };
    return labels[unit.id] || unit.label;
  }

  const GUARANTEE_TYPE_BY_NAME = {
    tender: 1,
    performancebond: 2,
    advancepayment: 3,
    paymentcommitment: 4,
    other: 99,
  };

  const DOCUMENT_TYPE_BY_NAME = {
    requestletter: 1,
    establishmentgazette: 2,
    financialstatements3years: 3,
    activitylicenses: 4,
    bankaccountturnover: 5,
    creditinquiryresult: 6,
    creditinformationform: 7,
    caseformationfeereceipt: 8,
    caseformationattachmentform: 9,
    companyintroductiondocs: 10,
    leaseorownershipdeed: 11,
    ceoboardidcards: 12,
    feasibilityreport: 13,
    guaranteeissuancerequestform: 14,
    salescontractsscan: 15,
    tenderannouncement: 16,
    basecontractimage: 17,
  };

  function normalizeDocumentType(value) {
    if (value == null || value === "") return NaN;
    if (typeof value === "number" && Number.isFinite(value)) return value;
    const s = String(value).trim();
    if (/^\d+$/.test(s)) return Number(s);
    const key = s.replace(/[\s_-]/g, "").toLowerCase();
    return DOCUMENT_TYPE_BY_NAME[key] ?? NaN;
  }

  function normalizeGuaranteeType(value) {
    if (value == null || value === "") return 0;
    if (typeof value === "number" && Number.isFinite(value)) return value;
    const s = String(value).trim();
    if (/^\d+$/.test(s)) return Number(s);
    const key = s.replace(/[\s_-]/g, "").toLowerCase();
    return GUARANTEE_TYPE_BY_NAME[key] || 0;
  }

  function documentsForGuaranteeType(guaranteeType) {
    const gt = normalizeGuaranteeType(guaranteeType);
    return DATA_ENTRY_DOCUMENTS.filter((doc) => {
      if (doc.whenGuaranteeType != null) return doc.whenGuaranteeType === gt;
      if (doc.whenGuaranteeTypes) return doc.whenGuaranteeTypes.includes(gt);
      return true;
    });
  }

  function requiredDocumentsForSubmit(guaranteeType) {
    const gt = normalizeGuaranteeType(guaranteeType);
    return DATA_ENTRY_DOCUMENTS.filter((doc) => {
      if (doc.whenGuaranteeType != null) return doc.whenGuaranteeType === gt;
      if (doc.whenGuaranteeTypes) return doc.whenGuaranteeTypes.includes(gt);
      return doc.required;
    });
  }

  function isDocRequiredForType(doc, guaranteeType) {
    const gt = normalizeGuaranteeType(guaranteeType);
    if (!gt) return !!doc.required;
    if (doc.required) return true;
    if (doc.whenGuaranteeType != null) return doc.whenGuaranteeType === gt;
    if (doc.whenGuaranteeTypes) return doc.whenGuaranteeTypes.includes(gt);
    return false;
  }

  /** همه ردیف‌های بارگذاری: نوع ذخیره‌شده + انتخاب فرم + هر مدرک الزامی */
  function uploadDocumentDefs(savedGuaranteeType, formGuaranteeType) {
    const saved = normalizeGuaranteeType(savedGuaranteeType);
    const form = normalizeGuaranteeType(formGuaranteeType);
    const types = new Set([saved, form].filter((t) => t > 0));
    const map = new Map();
    types.forEach((gt) => {
      documentsForGuaranteeType(gt).forEach((d) => map.set(d.type, d));
    });
    const validateGt = saved || form;
    if (validateGt > 0) {
      requiredDocumentsForSubmit(validateGt).forEach((d) => map.set(d.type, d));
    }
    if (map.size === 0) {
      DATA_ENTRY_DOCUMENTS.forEach((d) => map.set(d.type, d));
    }
    const priority = [16, 17, 8, 7, 2, 12, 3, 5, 4, 14, 6, 9, 10, 11, 13, 15];
    return Array.from(map.values()).sort((a, b) => {
      const ia = priority.indexOf(a.type);
      const ib = priority.indexOf(b.type);
      if (ia === -1 && ib === -1) return a.type - b.type;
      if (ia === -1) return 1;
      if (ib === -1) return -1;
      return ia - ib;
    });
  }

  function documentTypeLabel(type) {
    const t = normalizeDocumentType(type);
    if (!Number.isFinite(t)) return "مدرک";
    const fromEntry = DATA_ENTRY_DOCUMENTS.find((d) => d.type === t);
    if (fromEntry) return fromEntry.label;
    for (const stage of Object.values(WORKFLOW_STAGE_DOCUMENTS)) {
      const found = stage.docs.find((d) => d.type === t);
      if (found) return found.label;
    }
    const extra = {
      1: "نامه درخواست",
      6: "استعلام اعتباری",
      18: "پیش‌قرارداد (قرارداد خام)",
      19: "قرارداد امضاشده",
      20: "پیوست امضاشده ۱",
      21: "پیوست امضاشده ۲",
      22: "پیوست امضاشده ۳",
      23: "پیوست امضاشده ۴",
      24: "پیوست امضاشده ۵",
      25: "پیوست امضاشده ۶",
      26: "قرارداد نهایی",
      27: "ضمانت‌نامه صادره",
      28: "رسید صدور",
      29: "نامه رفع تعهد ذی‌نفع",
      30: "مرجع ضمانت‌نامه اصلی",
      31: "تاییدیه تسویه",
      32: "قرارداد اصلاحیه",
      99: "سایر",
    };
    return extra[t] || "مدرک (نوع " + t + ")";
  }

  const CANCELLATION_DOCUMENTS = [
    { type: 29, label: "نامه رفع تعهد ذی‌نفع", hint: "ضروری", required: true },
  ];

  const APPLICANT_CATEGORY_OTHER = 8;
  const BASE_CONTRACT_GUARANTEE_TYPES = [2, 3];
  const TENDER_REFERENCE_GUARANTEE_TYPES = [1, 2, 3];

  /** فیلدهای فرم درخواست — نمایش شرطی بر اساس نوع ضمانت‌نامه و دسته متقاضی */
  const APPLICATION_FIELD_RULES = {
    applicantCategoryOther: {
      applies: (ctx) => Number(ctx.applicantCategory) === APPLICANT_CATEGORY_OTHER,
    },
    baseContractNumber: {
      applies: (ctx) => TENDER_REFERENCE_GUARANTEE_TYPES.includes(Number(ctx.guaranteeType)),
      labelFor: (ctx) =>
        Number(ctx.guaranteeType) === 1 ? "شماره مناقصه / مزایده" : "شماره قرارداد پایه",
      omitWhenEmpty: true,
    },
    baseContractAmount: {
      applies: (ctx) => BASE_CONTRACT_GUARANTEE_TYPES.includes(Number(ctx.guaranteeType)),
    },
    baseContractAmountInWords: {
      applies: (ctx) => BASE_CONTRACT_GUARANTEE_TYPES.includes(Number(ctx.guaranteeType)),
    },
    priceAdjustmentRatePercent: {
      applies: (ctx) => BASE_CONTRACT_GUARANTEE_TYPES.includes(Number(ctx.guaranteeType)),
    },
    executionProvince: {
      applies: (ctx) => BASE_CONTRACT_GUARANTEE_TYPES.includes(Number(ctx.guaranteeType)),
    },
    collateralDescription: {
      applies: () => true,
      omitWhenEmpty: true,
    },
  };

  function applicationFieldContext(application, guaranteeTypeOverride) {
    const app = application || {};
    const pick = (camel, pascal) => {
      if (app[camel] !== undefined) return app[camel];
      if (app[pascal] !== undefined) return app[pascal];
      return undefined;
    };
    return {
      guaranteeType: normalizeGuaranteeType(
        guaranteeTypeOverride != null && guaranteeTypeOverride !== ""
          ? guaranteeTypeOverride
          : pick("guaranteeType", "GuaranteeType")
      ),
      applicantCategory: Number(pick("applicantCategory", "ApplicantCategory") || 0),
    };
  }

  function isApplicationFieldApplicable(fieldKey, ctx) {
    const rule = APPLICATION_FIELD_RULES[fieldKey];
    if (!rule) return true;
    return rule.applies(ctx || { guaranteeType: 0, applicantCategory: 0 });
  }

  function applicationFieldLabel(fieldKey, defaultLabel, ctx) {
    const rule = APPLICATION_FIELD_RULES[fieldKey];
    if (rule && typeof rule.labelFor === "function") return rule.labelFor(ctx);
    return defaultLabel;
  }

  function shouldOmitEmptyApplicationField(fieldKey) {
    return !!APPLICATION_FIELD_RULES[fieldKey]?.omitWhenEmpty;
  }

  window.GuaranteeWorkflowModel = {
    MODULE,
    STEPS,
    DATA_ENTRY_DOCUMENTS,
    WORKFLOW_STAGE_DOCUMENTS,
    CANCELLATION_DOCUMENTS,
    GUARANTEE_TYPES,
    APPLICANT_CATEGORIES,
    APPLICANT_LEGAL_FORMS,
    BENEFICIARY_COMPANY_TYPES,
    UNITS,
    normalizeRole,
    roleMatchesUnit,
    canActOnCase,
    unitRoleLabels,
    normalizeGuaranteeType,
    normalizeDocumentType,
    documentTypeLabel,
    documentsForGuaranteeType,
    requiredDocumentsForSubmit,
    isDocRequiredForType,
    uploadDocumentDefs,
    APPLICATION_FIELD_RULES,
    APPLICANT_CATEGORY_OTHER,
    applicationFieldContext,
    isApplicationFieldApplicable,
    applicationFieldLabel,
    shouldOmitEmptyApplicationField,
    PHASES,
    getUnit(unitId) {
      return UNITS.find((u) => u.id === unitId) || null;
    },
    getStepperSteps(context) {
      const isCancellation = isCancellationWorkflow(context?.amendmentType);
      const hiddenStepIds = new Set([13, 15, 21, 23]);

      if (isCancellation) {
        hiddenStepIds.add(20);
        hiddenStepIds.add(22);
      } else {
        hiddenStepIds.add(14);
      }

      const orderedIds = isCancellation
        ? MAIN_WORKFLOW_STEP_IDS.concat(CANCELLATION_WORKFLOW_STEP_IDS)
        : MAIN_WORKFLOW_STEP_IDS.concat(AMENDMENT_WORKFLOW_STEP_IDS);

      return orderedIds
        .filter((id) => !hiddenStepIds.has(id))
        .map((id) => {
          const step = STEPS[id];
          if (!step) return null;
          if (isCancellation && CANCELLATION_STEP_TITLES[id]) {
            return { ...step, title: CANCELLATION_STEP_TITLES[id] };
          }
          return step;
        })
        .filter(Boolean);
    },
    getStepOrderIndex(status, context) {
      const value = coerceStatus(status);
      return this.getStepperSteps(context).findIndex((step) => step.id === value);
    },
    coerceStatus,
    coerceAmendmentType,
    amendmentTypeFromCase,
    isCancellationWorkflow,
    stepForStatus(status, context) {
      return stepForStatus(status, context);
    },
  };
})();
