/* global GuaranteeWorkflowModel */
(function () {
  const model = window.GuaranteeWorkflowModel;
  const state = { panel: null, caseId: "", caseData: null, documents: [], comments: [], history: [], amendmentDetails: null, cancellationDetails: null, busy: false };
  let uploadFieldCounter = 0;

  function ensureCancellationModel() {
    if (!model) return;
    model.CANCELLATION_DOCUMENTS = [
      { type: 29, label: "نامه رفع تعهد ذی‌نفع", hint: "ضروری", required: true },
    ];
  }

  ensureCancellationModel();

  const qs = (sel, root) => (root || document).querySelector(sel);

  function gPath(suffix) {
    return state.panel.guaranteeCasesBasePath() + suffix;
  }

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function pick(obj, camel, pascal) {
    if (!obj) return undefined;
    if (obj[camel] !== undefined) return obj[camel];
    if (obj[pascal] !== undefined) return obj[pascal];
    return undefined;
  }

  function pickStatus(obj) {
    const raw = pick(obj, "currentStatus", "CurrentStatus") ?? 0;
    return model && typeof model.coerceStatus === "function" ? model.coerceStatus(raw) : Number(raw) || 0;
  }

  function pickCompany(obj) {
    if (!obj) return null;
    return obj.company || obj.Company || null;
  }

  function getSessionRole() {
    const session = state.panel.getActiveSession();
    if (!session) return "";
    return model.normalizeRole(session.userRoleText, session.userRoleNumber);
  }

  async function refreshStageRollback() {
    if (!window.CaseStageRollback) return;
    await window.CaseStageRollback.refresh({
      panel: state.panel,
      module: "guarantee",
      host: "gStageRollback",
      getCaseId: function () {
        return state.caseId;
      },
      getRole: getSessionRole,
      onSuccess: async function (msg) {
        setInfo(msg);
        await refreshCase();
      },
      onError: setError,
    });
  }

  function isInternalUser() {
    const role = getSessionRole();
    if (window.WorkflowModel && typeof window.WorkflowModel.isInternalRole === "function") {
      return window.WorkflowModel.isInternalRole(role);
    }
    return ["CreditExpert", "CreditManager", "LegalExpert", "LegalManager", "FinancialExpert", "FinancialManager", "CEO", "Admin"].includes(role);
  }

  function canViewFundCreditCapacity() {
    const role = getSessionRole();
    return window.FundCreditCapacityUi && window.FundCreditCapacityUi.canViewFundCreditCapacity(role);
  }

  function renderFundCreditCapacityBlock(card) {
    if (!window.FundCreditCapacityUi) return;
    window.FundCreditCapacityUi.renderFundCreditCapacityWidget(card, state.caseData, getSessionRole());
  }

  function readValue(id, root) {
    const node = (root || document).querySelector("#" + id);
    if (!node) return "";
    return String(node.value || "").trim();
  }

  function phaseForStatus(status) {
    return (stepForCase(status) || {}).phase || 0;
  }

  function commentsForPhase(phase) {
    return state.comments.filter((c) => Number(pick(c, "phase", "Phase")) === Number(phase));
  }

  function pickApplicantContact() {
    const session = state.panel.getActiveSession();
    const user = session && session.raw && (session.raw.user || session.raw.User);
    if (!user) return { fullName: "", phone: "", nationalCode: "" };
    const first = user.firstName || user.FirstName || "";
    const last = user.lastName || user.LastName || "";
    return {
      fullName: (first + " " + last).trim(),
      phone: user.phoneNumber || user.PhoneNumber || "",
      nationalCode: user.nationalCode || user.NationalCode || "",
    };
  }

  function setError(msg) {
    const box = qs("#gPortalError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#gPortalInfo")?.classList.add("hidden");
  }

  function setInfo(msg) {
    const box = qs("#gPortalInfo");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#gPortalError")?.classList.add("hidden");
  }

  function scrollToPortalMessage() {
    const target = qs("#gPortalError:not(.hidden)") || qs("#gPortalInfo:not(.hidden)");
    target?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }

  function el(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }

  function syncCompanyRow() {
    const row = qs("#gCompanyRow");
    const isCompany = Number(qs("#gApplicantType")?.value) === 2;
    if (row) row.style.display = isCompany ? "" : "none";
  }

  function populateCompanySelect(companies) {
    const select = qs("#gCompanyId");
    if (!select) return;
    select.innerHTML = "";
    if (!companies || !companies.length) {
      const option = document.createElement("option");
      option.value = "";
      option.textContent = "شرکتی ثبت نشده — از تب پرونده‌ها شرکت ثبت کنید";
      select.appendChild(option);
      return;
    }
    companies.forEach((company) => {
      const option = document.createElement("option");
      const id = company.id || company.Id;
      const name = company.name || company.Name || "شرکت";
      const economicCode = company.economicCode || company.EconomicCode || "";
      option.value = id;
      option.textContent = economicCode ? name + " (" + economicCode + ")" : name;
      select.appendChild(option);
    });
  }

  async function loadMyCompanies() {
    const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/identity/companies/mine" });
    const companies = unwrap(res.body) || [];
    populateCompanySelect(companies);
    return companies;
  }

  async function createCase() {
    const applicantType = Number(qs("#gApplicantType")?.value || 1);
    const payload = { applicantType };
    const title = qs("#gCaseTitleHub")?.value?.trim() || qs("#gCaseTitleInput")?.value?.trim();
    if (title) payload.title = title;
    if (applicantType === 2) {
      const companyId = (qs("#gCompanyId")?.value || "").trim();
      if (!companyId) throw new Error("برای متقاضی حقوقی، انتخاب شرکت الزامی است.");
      payload.companyId = companyId;
    }
    const res = await state.panel.apiRequest({ method: "POST", path: gPath(""), body: payload });
    const created = unwrap(res.body);
    state.caseId = created.id || created.Id;
    state.panel.setGuaranteeCaseId(state.caseId);
    await refreshCase();
  }

  async function fetchOptionalGet(path) {
    try {
      const res = await state.panel.apiRequest({ method: "GET", path });
      if (!isApiSuccess(res)) return null;
      return unwrap(res.body);
    } catch (_) {
      return null;
    }
  }

  async function reloadCaseData(options) {
    options = options || {};
    setError("");
    const base = gPath("/" + state.caseId);
    const caseRes = await apiCall({ method: "GET", path: base });
    state.caseData = unwrap(caseRes.body);
    if (options.renderEarly) {
      render();
    }

    const [docsPayload, commentsPayload, historyPayload, amendmentPayload, cancellationPayload] =
      await Promise.all([
        fetchOptionalGet(base + "/documents"),
        fetchOptionalGet(base + "/comments?includeInternal=" + (isInternalUser() ? "true" : "false")),
        fetchOptionalGet(base + "/history"),
        fetchOptionalGet(base + "/amendment/details"),
        fetchOptionalGet(base + "/amendment/cancellation/details"),
      ]);

    state.documents = docsPayload || [];
    state.comments = commentsPayload || [];
    state.history = historyPayload || [];
    state.amendmentDetails = amendmentPayload;
    state.cancellationDetails = cancellationPayload;
    render();
  }

  async function refreshCase(previousStatus, options) {
    if (!state.caseId) return;
    const pollOptions = Object.assign(
      { maxAttempts: previousStatus != null ? 4 : 1, baseDelayMs: 80 },
      options || {}
    );
    const refreshOnce = function () {
      return reloadCaseData({ renderEarly: previousStatus != null });
    };
    if (previousStatus != null && window.PortalCaseRefresh) {
      await window.PortalCaseRefresh.refreshUntilChanged(
        refreshOnce,
        function () {
          return pickStatus(state.caseData);
        },
        previousStatus,
        pollOptions
      );
    } else {
      await refreshOnce();
    }
    try {
      await refreshStageRollback();
    } catch (rollbackErr) {
      console.warn("[guarantee-portal] stage rollback refresh failed", rollbackErr);
    }
  }

  async function apiCall(opts) {
    const res = await state.panel.apiRequest(opts);
    if (!isApiSuccess(res)) {
      const msg =
        (res.body && (res.body.message || res.body.Message)) ||
        "درخواست با کد " + res.status + " ناموفق بود.";
      throw new Error(msg);
    }
    return res;
  }

  async function safeRefreshAfterAction(previousStatus) {
    try {
      await refreshCase(previousStatus, { maxAttempts: 4, baseDelayMs: 80 });
    } catch (refreshErr) {
      console.warn("[guarantee-portal] post-action refresh failed", refreshErr);
    }
  }

  function resolveActionSuccessMessage(actionId) {
    const messages = {
      "submit-app": "پرونده با موفقیت به واحد اعتبارات ارسال شد — وضعیت: بررسی اعتبارات.",
      "save-app": "درخواست ذخیره شد.",
      "credit-approve": "بررسی اعتبارات تأیید شد.",
      "credit-revision": "درخواست اصلاح برای متقاضی ثبت شد.",
      "fin-approve": "مدارک مالی تأیید شد.",
      "fin-revision": "درخواست اصلاح برای متقاضی ثبت شد.",
      "approval-save": "فرم تصویب ذخیره شد.",
      "approval-submit": "فرم تصویب ارسال شد — پرونده به تأیید مدیرعامل رفت.",
      "cancel-case": "پرونده لغو شد.",
      "ceo-cancel": "پرونده لغو شد.",
      "ceo-final-cancel": "پرونده لغو شد.",
      "save-amendment": "اصلاحیه ذخیره شد.",
      "submit-amendment": "اصلاحیه برای بررسی ارسال شد.",
      "approve-amendment": "اصلاحیه تایید شد.",
      "approve-cancellation": "ابطال تایید شد.",
      "ceo-amendment-approve": "اصلاحیه تایید شد.",
      "reject-amendment": "اصلاحیه رد شد.",
      "ceo-amendment-reject": "اصلاحیه رد شد.",
      "amendment-revision": "درخواست اصلاح برای متقاضی ثبت شد.",
    };
    return messages[actionId] || "";
  }

  function finishPortalMutation(successMessage, previousStatus) {
    if (successMessage) {
      setInfo(successMessage);
      scrollToPortalMessage();
    }
    void safeRefreshAfterAction(previousStatus);
  }

  function cancelPortalAction() {
    setInfo("");
  }

  function documentForType(documentType) {
    const t = model.normalizeDocumentType(documentType);
    if (!Number.isFinite(t)) return null;
    const matches = state.documents.filter(
      (d) => model.normalizeDocumentType(pick(d, "documentType", "DocumentType")) === t
    );
    if (!matches.length) return null;
    return matches.reduce((best, cur) => {
      const bv = Number(pick(best, "version", "Version") ?? 0);
      const cv = Number(pick(cur, "version", "Version") ?? 0);
      return cv > bv ? cur : best;
    });
  }

  function renderSummary() {
    const empty = qs("#gPortalEmpty");
    const header = qs("#gPortalHeader");
    if (!state.caseData) {
      empty?.classList.remove("hidden");
      header?.classList.add("hidden");
      return;
    }
    empty?.classList.add("hidden");
    header?.classList.remove("hidden");
    qs("#gCaseNumber").textContent = pick(state.caseData, "caseNumber", "CaseNumber") || "—";
    const title = pick(state.caseData, "title", "Title") || "—";
    if (qs("#gCaseTitle")) qs("#gCaseTitle").textContent = title;
    const titleInput = qs("#gCaseTitleInput");
    if (titleInput && document.activeElement !== titleInput) {
      titleInput.value = pick(state.caseData, "title", "Title") || "";
    }
    const gCaseIdEl = qs("#gCaseId");
    if (gCaseIdEl) gCaseIdEl.textContent = state.caseId;
    const st = pickStatus(state.caseData);
    const step = stepForCase(st);
    qs("#gCaseStatus").textContent = step.title + " (" + st + ")";
    const roleEl = qs("#gCaseRole");
    if (roleEl) roleEl.textContent = getSessionRole() || "—";
    const company = pickCompany(state.caseData);
    const companyEl = qs("#gCaseCompany");
    if (companyEl) {
      companyEl.textContent = company
        ? (pick(company, "name", "Name") || "—") + " · " + (pick(company, "nationalId", "NationalId") || "—")
        : "متقاضی حقیقی";
    }
  }

  function readApplicationFromCase() {
    const c = state.caseData;
    if (!c) return null;
    if (c.application || c.Application) return c.application || c.Application;
    if (pick(c, "guaranteeType", "GuaranteeType") != null) return c;
    return null;
  }

  function readAmendmentFromCase() {
    return state.amendmentDetails || state.caseData?.amendment || state.caseData?.Amendment || null;
  }

  function readCancellationDetails() {
    return state.cancellationDetails || null;
  }

  function amendmentTypeLabel(value) {
    const n = Number(value || 0);
    if (n === 1) return "تمدید";
    if (n === 2) return "تقلیل";
    if (n === 3) return "ابطال";
    return "—";
  }

  function amendmentReviewStateLabel(status) {
    const value = Number(status || 0);
    if (value === 1) return "در انتظار بررسی";
    if (value === 2) return "تایید شده";
    if (value === 3) return "رد شده";
    if (value === 18) return "در انتظار بررسی اعتبارات";
    if (value === 19) return "در انتظار تأیید مدیرعامل";
    if (value === 20) return "در انتظار بررسی حقوقی";
    if (value === 22 || value === 21 || value === 14) return "تایید شده";
    if (value === 23) return "رد شده";
    if (value === 16 || value === 17) return "پیش‌نویس";
    return "—";
  }

  function currentAmendmentType() {
    const amendment = readAmendmentFromCase() || {};
    const cancellation = readCancellationDetails() || {};
    const raw =
      pick(amendment, "amendmentType", "AmendmentType") ||
      pick(cancellation, "amendmentType", "AmendmentType") ||
      pick(state.caseData, "amendmentType", "AmendmentType") ||
      0;
    return typeof model.coerceAmendmentType === "function"
      ? model.coerceAmendmentType(raw)
      : Number(raw || 0);
  }

  function workflowContext() {
    const fromDetails = currentAmendmentType();
    const fromCase = Number(pick(state.caseData, "amendmentType", "AmendmentType") || 0);
    const amendmentType = fromDetails || fromCase;
    return amendmentType > 0 ? { amendmentType } : {};
  }

  function stepForCase(status) {
    return model.stepForStatus(status, workflowContext());
  }

  function workflowProcessLabel() {
    return currentAmendmentType() === 3 ? "ابطال" : "اصلاحیه";
  }

  function canStartNewAmendment(status) {
    return status === 12 || status === 16 || status === 17 || status === 22 || status === 23;
  }

  function isApiSuccess(res) {
    return !!(res && res.ok && !(res.body && res.body.success === false));
  }

  function pickAmendmentType(amendment) {
    return Number(pick(amendment, "amendmentType", "AmendmentType") || 0);
  }

  function pickAmendmentValidityTo(amendment) {
    const next = pick(amendment, "newValues", "NewValues") || {};
    return (
      pick(amendment, "approvedValidityTo", "ApprovedValidityTo") ||
      pick(amendment, "requestedValidityTo", "RequestedValidityTo") ||
      pick(next, "validityTo", "ValidityTo") ||
      null
    );
  }

  function pickAmendmentAmount(amendment, fallbackAmount) {
    const next = pick(amendment, "newValues", "NewValues") || {};
    const approved = pick(amendment, "approvedAmount", "ApprovedAmount");
    const requested = pick(amendment, "requestedAmount", "RequestedAmount");
    const fromNext = pick(next, "guaranteeAmount", "GuaranteeAmount");
    if (approved != null && approved !== "") return approved;
    if (requested != null && requested !== "") return requested;
    if (fromNext != null && fromNext !== "") return fromNext;
    return fallbackAmount ?? null;
  }

  function buildAmendmentInfoRows(amendment, typeValue, status, currentValidityTo, currentAmount) {
    const processLabel = typeValue === 3 ? "ابطال" : "اصلاحیه";
    const rows = [
      ["نوع " + processLabel, amendmentTypeLabel(typeValue)],
      ["وضعیت " + processLabel, amendmentReviewStateLabel(status)],
      ["علت", pick(amendment, "reason", "Reason") || "—"],
    ];
    const prev = pick(amendment, "previousValues", "PreviousValues") || {};
    const beforeValidity = pick(prev, "validityTo", "ValidityTo") || currentValidityTo;
    const beforeAmount = pick(prev, "guaranteeAmount", "GuaranteeAmount") || currentAmount;
    rows.push(["تاریخ اعتبار قبل از اصلاح", beforeValidity || "—"]);
    rows.push(["مبلغ قبل از اصلاح", formatRialAmount(beforeAmount) || "—"]);

    if (typeValue === 1) {
      rows.push(["تاریخ اعتبار جدید", pickAmendmentValidityTo(amendment) || "—"]);
    } else if (typeValue === 2) {
      rows.push(["مبلغ جدید", formatRialAmount(pickAmendmentAmount(amendment, null)) || "—"]);
    } else if (typeValue === 3) {
      rows.push(["نوع عملیات", "ابطال ضمانت‌نامه"]);
    }

    if (typeValue === 1 || typeValue === 2) {
      const approvedValidity = pick(amendment, "approvedValidityTo", "ApprovedValidityTo");
      const approvedAmount = pick(amendment, "approvedAmount", "ApprovedAmount");
      if (approvedValidity) rows.push(["تاریخ اعتبار تأییدشده", approvedValidity]);
      if (approvedAmount != null && approvedAmount !== "") rows.push(["مبلغ تأییدشده", formatRialAmount(approvedAmount)]);
    }

    return rows;
  }

  function readAmendmentForm() {
    const type = Number(qs("#gAmendmentType")?.value || 0);
    const reason = qs("#gAmendmentReason")?.value?.trim() || null;
    const newValidityTo = qs("#gAmendmentValidityTo")?.value?.trim() || null;
    const amountRaw = qs("#gAmendmentAmount")?.value?.trim() || "";
    const newGuaranteeAmount = amountRaw ? Number(amountRaw) : null;
    return {
      amendmentType: type || null,
      newValidityTo: type === 1 ? newValidityTo : null,
      newGuaranteeAmount: type === 2 && Number.isFinite(newGuaranteeAmount) ? newGuaranteeAmount : null,
      reason,
    };
  }

  function readCancellationForm() {
    return {
      reason: qs("#gAmendmentReason")?.value?.trim() || null,
    };
  }

  function readCancellationSubmitForm() {
    return {
      comment: qs("#gAmendmentApproveComment")?.value?.trim() || null,
    };
  }

  function guaranteeSourceSnapshot() {
    const cancellation = readCancellationDetails() || {};
    const src = cancellation.source || cancellation.Source;
    if (src && (pick(src, "caseNumber", "CaseNumber") || pick(src, "caseId", "CaseId"))) return src;
    const app = readApplicationFromCase() || {};
    const form = (state.caseData && (state.caseData.approvalForm || state.caseData.ApprovalForm)) || {};
    const instrument = (state.documents || []).find((d) => Number(pick(d, "documentType", "DocumentType")) === 27);
    const active = Number(pick(form, "activeCommitments", "ActiveCommitments") || 0);
    return {
      caseId: pick(state.caseData, "id", "Id"),
      caseNumber: pick(state.caseData, "caseNumber", "CaseNumber"),
      guaranteeAmount: pick(form, "guaranteeAmount", "GuaranteeAmount") || pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount"),
      beneficiaryName: pick(form, "beneficiary", "Beneficiary") || pick(app, "beneficiaryName", "BeneficiaryName"),
      issuanceDate: pick(form, "issuanceDate", "IssuanceDate"),
      expiryDate: pick(form, "expiryDate", "ExpiryDate") || pick(app, "validityTo", "ValidityTo"),
      commissionAmount: pick(form, "commissionAmount", "CommissionAmount"),
      depositAmount: pick(form, "depositAmount", "DepositAmount"),
      activeCommitments: pick(form, "activeCommitments", "ActiveCommitments"),
      settlementConfirmationRequired: active > 0,
      hasIssuanceDocument: !!instrument,
      issuanceDocumentFileName: instrument ? pick(instrument, "fileName", "FileName") : null,
    };
  }

  function renderIssuedGuaranteeSummary(card) {
    const src = guaranteeSourceSnapshot();
    const settlementNote = pick(src, "settlementConfirmationRequired", "SettlementConfirmationRequired")
      ? "تعهد فعال باقی مانده — بررسی اعتباری الزامی است."
      : "بدون تعهد فعال — تسویه از نظر سیستم تکمیل است.";
    renderReadOnlyBlock(card, "ضمانت‌نامه صادره (مرجع سیستم)", [
      ["شماره پرونده / مرجع", pick(src, "caseNumber", "CaseNumber") || "—"],
      ["شناسه پرونده", pick(src, "caseId", "CaseId") || "—"],
      ["مبلغ ضمانت‌نامه", formatRialAmount(pick(src, "guaranteeAmount", "GuaranteeAmount")) || "—"],
      ["ذی‌نفع", pick(src, "beneficiaryName", "BeneficiaryName") || "—"],
      ["تاریخ صدور", pick(src, "issuanceDate", "IssuanceDate") || "—"],
      ["تاریخ انقضا", pick(src, "expiryDate", "ExpiryDate") || "—"],
      ["کارمزد", formatRialAmount(pick(src, "commissionAmount", "CommissionAmount")) || "—"],
      ["ودیعه", formatRialAmount(pick(src, "depositAmount", "DepositAmount")) || "—"],
      ["تعهدات فعال", formatRialAmount(pick(src, "activeCommitments", "ActiveCommitments")) || "—"],
      ["وضعیت تسویه", settlementNote],
      [
        "مدرک صدور (ضمانت‌نامه)",
        pick(src, "hasIssuanceDocument", "HasIssuanceDocument")
          ? "✓ " + (pick(src, "issuanceDocumentFileName", "IssuanceDocumentFileName") || "بارگذاری شده")
          : "— (در پرونده یافت نشد)",
      ],
    ]);
  }

  function checkboxField(label, id, checked) {
    const row = el("label", "formrow");
    const input = document.createElement("input");
    input.type = "checkbox";
    input.id = id;
    input.checked = !!checked;
    row.appendChild(input);
    row.appendChild(document.createTextNode(" " + label));
    return row;
  }

  function savedGuaranteeType() {
    const app = readApplicationFromCase();
    return model.normalizeGuaranteeType(pick(app, "guaranteeType", "GuaranteeType"));
  }

  /** root = کارت مرحله؛ قبل از append به DOM باید از همان subtree بخوانیم */
  function formGuaranteeType(root) {
    const scope = root || document;
    const select = scope.querySelector("#gGuaranteeType");
    if (!select) return 0;
    const v = select.value;
    if (v) return model.normalizeGuaranteeType(v);
    const first = select.options && select.options[0];
    return first ? model.normalizeGuaranteeType(first.value) : 0;
  }

  function resolveGuaranteeTypes(root) {
    const saved = savedGuaranteeType();
    const form = formGuaranteeType(root);
    return { saved, form, effective: saved || form };
  }

  function guaranteeTypeForValidation(root) {
    return resolveGuaranteeTypes(root).effective;
  }

  function formatMissingDocumentsError(missing, guaranteeType) {
    const labels = missing.map((d) => d.label).join("، ");
    const gt = Number(guaranteeType);
    if (gt === 1 && missing.some((d) => d.type === 16) && documentForType(1)) {
      return (
        "مدارک الزامی ناقص است: " +
        labels +
        ". «نامه درخواست» دیگر استفاده نمی‌شود — برای «شرکت در مناقصه» فیلد «تصویر آگهی مناقصه/مزایده» را بارگذاری کنید."
      );
    }
    return "مدارک الزامی ناقص است: " + labels;
  }

  function missingRequiredDocuments(explicitGt, root) {
    const gt =
      explicitGt != null
        ? model.normalizeGuaranteeType(explicitGt)
        : guaranteeTypeForValidation(root);
    if (!gt) {
      return model.DATA_ENTRY_DOCUMENTS.filter((d) => d.required && !documentForType(d.type));
    }
    return model.requiredDocumentsForSubmit(gt).filter((doc) => !documentForType(doc.type));
  }

  function requiredDocumentsComplete(explicitGt, root) {
    return missingRequiredDocuments(explicitGt, root).length === 0;
  }

  function renderStepper() {
    const root = qs("#gPortalStepper");
    if (!root) return;
    root.innerHTML = "";
    if (!state.caseData) return;

    const current = pickStatus(state.caseData);
    const track = el("div", "portal-stepper__track");
    const currentIndex = model.getStepOrderIndex(current, workflowContext());
    const ctx = workflowContext();

    model.getStepperSteps(ctx).forEach((step, index) => {
      const item = el("div", "portal-stepper__item");
      if (currentIndex >= 0) {
        if (index < currentIndex) item.classList.add("is-done");
        else if (index === currentIndex) item.classList.add("is-current");
        else item.classList.add("is-upcoming");
      } else if (step.id === current) {
        item.classList.add("is-current");
      } else {
        item.classList.add("is-upcoming");
      }

      item.appendChild(el("div", "portal-stepper__index", String(step.id)));
      item.appendChild(el("div", "portal-stepper__title", step.title));
      const unit = model.getUnit(step.unit);
      item.appendChild(el("div", "portal-stepper__unit", (unit && unit.label) || step.unit));
      track.appendChild(item);

      if (step.id === current) {
        requestAnimationFrame(function () {
          item.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
        });
      }
    });

    root.appendChild(track);
  }

  function renderActionBar() {
    const root = qs("#gPortalActionBar");
    if (!root) return;
    root.innerHTML = "";
    root.classList.add("hidden");

    if (!state.caseData) return;

    const status = pickStatus(state.caseData);
    const role = getSessionRole();

    // Only show action bar if case is Completed (status 12)
    if (status !== 12) return;

    const actions = [];
    const isApplicant = role === "Applicant" || role === "Admin";
    if (!isApplicant) return;

    actions.push(
      { id: "extension", label: "تمدید ضمانت‌نامه", amendmentType: 1, class: "btn--success" },
      { id: "reduction", label: "تقلیل ضمانت‌نامه", amendmentType: 2, class: "btn--warning" },
      { id: "cancellation", label: "ابطال ضمانت‌نامه", amendmentType: 3, class: "btn--danger" }
    );

    root.classList.remove("hidden");

    const title = el("div", "portal-action-bar__title", "عملیات‌های پرونده تکمیل‌شده");
    root.appendChild(title);

    const buttonGroup = el("div", "portal-action-bar__buttons");

    actions.forEach(action => {
      const btn = el("button", "btn " + action.class, action.label);
      btn.type = "button";
      btn.dataset.amendmentType = action.amendmentType;
      btn.addEventListener("click", () => focusAmendmentForm(action.amendmentType));
      buttonGroup.appendChild(btn);
    });

    root.appendChild(buttonGroup);
  }

  function focusAmendmentForm(amendmentType) {
    const sel = qs("#gAmendmentType");
    if (!sel) {
      setError("فرم اصلاحیه یافت نشد. صفحه را یک‌بار رفرش کنید.");
      scrollToPortalMessage();
      return;
    }
    sel.value = String(amendmentType);
    sel.dispatchEvent(new Event("change"));
    const form = sel.closest(".portal-form");
    if (form) {
      const title = form.querySelector(".card__title");
      if (title) {
        title.textContent = Number(amendmentType) === 3
          ? "ثبت ابطال ضمانت‌نامه"
          : "ثبت اصلاحیه ضمانت‌نامه";
      }
      form.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  function renderActionHint() {
    const box = qs("#gPortalActionHint");
    if (!box || !state.caseData) return;

    const status = pickStatus(state.caseData);
    const role = getSessionRole();
    const step = stepForCase(status);
    let text = "";

    if (!model.canActOnCase(role, step.unit)) {
      text = "با نقش «" + (role || "—") + "» در این مرحله اقدامی ندارید.";
    } else if (status === 1) {
      text = "گام بعدی: «شروع ورود اطلاعات» — سپس فرم و مدارک را تکمیل کنید.";
    } else if (status === 2) {
      const missing = missingRequiredDocuments(undefined, document);
      if (missing.length) {
        text =
          "گام بعدی: «ارسال به واحد اعتبارات» — مدارک باقی‌مانده: " +
          missing.map((d) => d.label).join("، ");
      } else {
        text = "همه مدارک ضروری بارگذاری شده — «ارسال به واحد اعتبارات» را بزنید.";
      }
    } else if (status === 3) {
      text = "پرونده در انتظار بررسی واحد اعتبارات است.";
    } else if (status >= 4 && status <= 11) {
      text = "مرحله «" + step.title + "» — از دکمه‌های اقدام در پایین استفاده کنید.";
    } else if (status === 12) {
      text = "پرونده تکمیل شده است — برای تمدید، تقلیل یا ابطال از دکمه‌های بالا یا فرم پایین استفاده کنید.";
    }

    if (!text && status >= 16 && status <= 23) {
      const amendment = readAmendmentFromCase();
      const typeValue = Number(pick(amendment, "amendmentType", "AmendmentType") || 0);
      const typeLabel = amendmentTypeLabel(typeValue);
      text = "فرایند " + typeLabel + " ضمانت‌نامه در حال پیگیری است. وضعیت جاری: " + step.title + ".";
    }
    if (!text && status === 14) {
      text = "این پرونده ابطال شده است.";
    }
    if (!text) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.classList.remove("hidden");
    box.textContent = text;
  }

  function renderCommentsHistory(card, phase, title, options) {
    options = options || {};
    const block = el("div", "portal-thread card portal-card portal-card--nested");
    block.dataset.commentPhase = String(phase);
    block.appendChild(el("div", "card__title", title || "تاریخچه نظرات"));

    let items = commentsForPhase(phase);
    if (options.revisionOnly) {
      items = items.filter((c) => pick(c, "isRevisionRequest", "IsRevisionRequest"));
    }
    if (!items.length) {
      block.appendChild(el("div", "muted", options.emptyText || "هنوز نظری ثبت نشده است."));
    } else if (window.UIComponents && UIComponents.renderCommentThreadList) {
      block.appendChild(
        UIComponents.renderCommentThreadList(items, {
          module: "guarantee",
          history: state.history,
          allComments: state.comments,
        })
      );
    } else {
      const list = el("div", "portal-thread__list");
      items.forEach((comment) => {
        const row = el("div", "portal-thread__item");
        const meta = el("div", "portal-thread__meta muted");
        const role = pick(comment, "senderRole", "SenderRole") || "";
        const revision = pick(comment, "isRevisionRequest", "IsRevisionRequest");
        const internal = pick(comment, "isInternal", "IsInternal");
        const parts = [role];
        if (revision) parts.push("درخواست اصلاح");
        else if (internal) parts.push("نظر داخلی");
        else parts.push("نظر");
        meta.textContent = parts.filter(Boolean).join(" · ");
        row.appendChild(meta);
        row.appendChild(el("div", "portal-thread__message", pick(comment, "message", "Message") || "—"));
        list.appendChild(row);
      });
      block.appendChild(list);
    }
    card.appendChild(block);
  }

  function renderApplicantRevisionInbox(card) {
    const revisions = commentsForPhase(phaseForStatus(2)).filter((c) =>
      pick(c, "isRevisionRequest", "IsRevisionRequest")
    );
    if (!revisions.length) return;

    const box = el("div", "portal-revision-inbox card portal-card portal-card--nested");
    box.appendChild(el("div", "card__title", "درخواست اصلاح — لطفاً اصلاح کنید و دوباره ارسال کنید"));
    revisions.forEach((comment) => {
      const row = el("div", "portal-thread__item");
      row.appendChild(
        el("div", "portal-thread__meta muted", pick(comment, "senderRole", "SenderRole") || "واحد اعتبارات")
      );
      row.appendChild(el("div", "portal-thread__message", pick(comment, "message", "Message") || "—"));
      box.appendChild(row);
    });
    card.appendChild(box);
  }

  function labelFromOptions(value, options) {
    if (value == null || value === "") return "—";
    const n = Number(value);
    const found = (options || []).find((o) => o.value === n);
    return found ? found.label : String(value);
  }

  function renderReadOnlyBlock(card, title, rows) {
    const wrap = el("div", "portal-readonly-block");
    if (title) wrap.appendChild(el("div", "card__title", title));
    rows.forEach(([label, value]) => {
      const row = el("div", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value != null && value !== "" ? String(value) : "—"));
      wrap.appendChild(row);
    });
    card.appendChild(wrap);
  }

  function shouldShowCompletedCaseDossier(status) {
    const value = Number(status) || 0;
    return value === 12 || value === 22;
  }

  function shouldShowCaseDossier(status) {
    const value = Number(status) || 0;
    if (value >= 12) return true;
    return isInternalUser() && value >= 3 && value <= 11;
  }

  function formatCaseDate(iso) {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleString("fa-IR");
    } catch {
      return String(iso);
    }
  }

  function renderFullWorkflowHistory(parent) {
    if (!state.history.length) {
      parent.appendChild(el("div", "muted", "تاریخچه‌ای ثبت نشده است."));
      return;
    }
    const list = el("div", "portal-history");
    state.history.forEach((item) => {
      const row = el("div", "portal-history__item");
      const fromStatus = pick(item, "fromStatus", "FromStatus") ?? "";
      const toStatus = pick(item, "toStatus", "ToStatus") ?? "";
      const action = pick(item, "action", "Action") || "";
      const actor = pick(item, "actorRole", "ActorRole") || "";
      const when = formatCaseDate(pick(item, "createdAt", "CreatedAt"));
      row.textContent = [when, action, fromStatus && toStatus ? fromStatus + " → " + toStatus : toStatus, actor]
        .filter(Boolean)
        .join(" · ");
      list.appendChild(row);
    });
    parent.appendChild(list);
  }

  function renderAmendmentHistoryInDossier(parent) {
    const amendment = readAmendmentFromCase() || {};
    const auditHistory = pick(amendment, "history", "History") || [];
    if (!auditHistory.length) {
      parent.appendChild(el("div", "muted", "سابقه اصلاحیه‌ای ثبت نشده است."));
      return;
    }
    auditHistory.forEach((item) => {
      const row = el("div", "portal-thread__item");
      const createdBy = pick(item, "createdByFullName", "CreatedByFullName") || pick(item, "createdBy", "CreatedBy") || "—";
      const approvalBy = pick(item, "approvalUserFullName", "ApprovalUserFullName") || pick(item, "approvalUser", "ApprovalUser") || "—";
      const decisionReason = pick(item, "decisionReason", "DecisionReason") || "—";
      row.appendChild(el("div", "portal-thread__meta muted", amendmentReviewStateLabel(pick(item, "status", "Status"))));
      row.appendChild(el("div", "portal-thread__message", "نوع: " + amendmentTypeLabel(pick(item, "amendmentType", "AmendmentType"))));
      row.appendChild(el("div", "portal-thread__message", "ثبت‌کننده: " + createdBy));
      row.appendChild(el("div", "portal-thread__message", "علت: " + (pick(item, "reason", "Reason") || "—")));
      row.appendChild(el("div", "portal-thread__message", "تایید/رد توسط: " + approvalBy));
      row.appendChild(el("div", "portal-thread__message", "توضیح تصمیم: " + decisionReason));
      parent.appendChild(row);
    });
  }

  function formatUploadedAt(doc) {
    const raw = pick(doc, "uploadedAt", "UploadedAt");
    if (!raw) return "";
    try {
      return new Date(raw).toLocaleString("fa-IR");
    } catch {
      return String(raw);
    }
  }

  async function downloadGuaranteeDocument(documentId) {
    if (!state.caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const session = state.panel.getActiveSession();
    const headers = {};
    if (session?.accessToken) headers.Authorization = "Bearer " + session.accessToken;
    const url = state.panel.makeUrl(
      gPath("/" + state.caseId + "/documents/" + encodeURIComponent(documentId) + "/download")
    );
    const res = await fetch(url, { method: "GET", headers });
    if (!res.ok) throw new Error("دانلود فایل با کد " + res.status + " ناموفق بود.");
    const blob = await res.blob();
    const disposition = res.headers.get("Content-Disposition") || "";
    const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
    const fileName = match ? decodeURIComponent(match[1].replace(/"/g, "")) : "document";
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.rel = "noopener";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  }

  function appendDownloadButton(parent, documentId) {
    if (!documentId) return;
    const btn = el("button", "btn btn--small", "دانلود");
    btn.type = "button";
    btn.addEventListener("click", () => {
      void downloadGuaranteeDocument(documentId).catch((e) => setError(e.message || String(e)));
    });
    parent.appendChild(btn);
  }

  function renderApprovalFormReadOnly(parent) {
    const form = pickApprovalForm();
    if (!form) {
      parent.appendChild(el("div", "muted", "فرم تصویب هنوز ثبت نشده است."));
      return;
    }
    const f = (camel, pascal) => pick(form, camel, pascal);
    const rows = [
      ["نوع ضمانت‌نامه (تصویب)", labelFromOptions(f("guaranteeType", "GuaranteeType"), model.GUARANTEE_TYPES)],
      ["مبلغ ضمانت‌نامه (ریال)", formatRialAmount(f("guaranteeAmount", "GuaranteeAmount"))],
      ["مبلغ (حروف)", f("guaranteeAmountInWords", "GuaranteeAmountInWords")],
      ["موضوع قرارداد", f("contractSubject", "ContractSubject")],
      ["ذی‌نفع", f("beneficiary", "Beneficiary")],
      ["تاریخ صدور", formatDateInput(f("issuanceDate", "IssuanceDate"))],
      ["تاریخ انقضا", formatDateInput(f("expiryDate", "ExpiryDate"))],
      ["مدت (روز)", f("activeDurationDays", "ActiveDurationDays")],
      ["نرخ ودیعه (٪)", f("depositRatePercent", "DepositRatePercent")],
      ["مبلغ ودیعه (ریال)", formatRialAmount(f("depositAmount", "DepositAmount"))],
      ["نرخ کارمزد سالانه (٪)", f("annualCommissionRatePercent", "AnnualCommissionRatePercent")],
      ["مبلغ کارمزد (ریال)", formatRialAmount(f("commissionAmount", "CommissionAmount"))],
      ["وثایق / تضمین", f("collateralDescription", "CollateralDescription")],
      ["ضامنین", f("guarantorsDescription", "GuarantorsDescription")],
      ["سایر توضیحات", f("otherNotes", "OtherNotes")],
    ];
    const snap = pickApplicantCreditSnapshot();
    if (snap) {
      rows.unshift(
        ["اعتبار باقی‌مانده (جدول ۱)", formatRialAmount(pick(snap, "remainingCredit", "RemainingCredit"))],
        ["تعهدات فعال (جدول ۱)", formatRialAmount(pick(snap, "activeCommitments", "ActiveCommitments"))],
        ["صادره صندوق (جدول ۱)", formatRialAmount(pick(snap, "fundIssuedGuaranteesTotal", "FundIssuedGuaranteesTotal"))],
        ["سقف اعتبار (جدول ۱)", formatRialAmount(pick(snap, "creditLimitWithCheck", "CreditLimitWithCheck"))]
      );
    }
    const inner = el("div", "portal-readonly-block");
    rows.forEach(([label, value]) => {
      const row = el("div", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value != null && value !== "" ? String(value) : "—"));
      inner.appendChild(row);
    });
    parent.appendChild(inner);
  }

  function renderAllDocumentsArchive(parent) {
    const wrap = el("div", "portal-doc-archive");
    if (!state.documents.length) {
      wrap.appendChild(el("div", "muted", "هنوز مدرکی بارگذاری نشده است."));
      parent.appendChild(wrap);
      return;
    }

    const byType = new Map();
    state.documents.forEach((doc) => {
      const type = model.normalizeDocumentType(pick(doc, "documentType", "DocumentType"));
      if (!byType.has(type)) byType.set(type, []);
      byType.get(type).push(doc);
    });

    const types = Array.from(byType.keys()).sort((a, b) => a - b);
    types.forEach((type) => {
      const versions = byType
        .get(type)
        .slice()
        .sort((a, b) => Number(pick(b, "version", "Version") ?? 0) - Number(pick(a, "version", "Version") ?? 0));
      const block = el("div", "portal-doc-archive__type");
      block.appendChild(el("div", "portal-doc-archive__type-title", model.documentTypeLabel(type)));
      versions.forEach((doc) => {
        const id = pick(doc, "id", "Id");
        const ver = pick(doc, "version", "Version") ?? 1;
        const name = pick(doc, "fileName", "FileName") || "فایل";
        const size = pick(doc, "fileSize", "FileSize");
        const row = el("div", "portal-doc-archive__row");
        const meta = el("div", "portal-doc-archive__meta");
        meta.textContent =
          "نسخه " +
          ver +
          " — " +
          name +
          (size ? " · " + Math.round(Number(size) / 1024) + " KB" : "") +
          (formatUploadedAt(doc) ? " · " + formatUploadedAt(doc) : "");
        row.appendChild(meta);
        appendDownloadButton(row, id);
        block.appendChild(row);
      });
      wrap.appendChild(block);
    });
    parent.appendChild(wrap);
  }

  function renderDossierComments(parent) {
    const items = state.comments
      .slice()
      .sort(
        (a, b) =>
          new Date(pick(a, "createdAt", "CreatedAt") || 0).getTime() -
          new Date(pick(b, "createdAt", "CreatedAt") || 0).getTime()
      );
    if (!items.length) {
      parent.appendChild(el("div", "muted", "نظری ثبت نشده است."));
      return;
    }
    if (window.UIComponents && UIComponents.renderCommentThreadList) {
      parent.appendChild(
        UIComponents.renderCommentThreadList(items, {
          module: "guarantee",
          history: state.history,
          allComments: state.comments,
        })
      );
      return;
    }
    const block = el("div", "portal-thread");
    const list = el("div", "portal-thread__list");
    items.forEach((comment) => {
      const row = el("div", "portal-thread__item");
      const phase = Number(pick(comment, "phase", "Phase"));
      const phaseTitle = model.PHASES[phase] || "فاز " + phase;
      const role = pick(comment, "senderRole", "SenderRole") || "";
      const revision = pick(comment, "isRevisionRequest", "IsRevisionRequest");
      const internal = pick(comment, "isInternal", "IsInternal");
      const parts = [phaseTitle, role];
      if (revision) parts.push("درخواست اصلاح");
      else if (internal) parts.push("داخلی");
      const when = formatUploadedAt(comment);
      if (when) parts.push(when);
      row.appendChild(el("div", "portal-thread__meta muted", parts.join(" · ")));
      row.appendChild(el("div", "portal-thread__message", pick(comment, "message", "Message") || "—"));
      list.appendChild(row);
    });
    block.appendChild(list);
    parent.appendChild(block);
  }

  function renderCaseDossier(card) {
    const details = document.createElement("details");
    details.className = "portal-dossier card portal-card portal-card--nested";
    details.open = true;

    const summary = document.createElement("summary");
    summary.className = "portal-dossier__summary card__title";
    summary.textContent = "پرونده کامل — اطلاعات ثبت‌شده و مدارک (با دکمه دانلود)";
    details.appendChild(summary);

    const body = el("div", "portal-dossier__body");
    const status = pickStatus(state.caseData);
    body.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        status === 22
          ? "اصلاحیه تأیید شد. خلاصه کامل پرونده و سابقه اصلاحیه در زیر قابل مشاهده است."
          : shouldShowCompletedCaseDossier(status)
            ? "پرونده تکمیل شده است. تمام اطلاعات ثبت‌شده در زیر قابل مشاهده است."
            : "خلاصه درخواست متقاضی، فرم تصویب (در صورت وجود) و همه فایل‌های بارگذاری‌شده."
      )
    );

    const metaRows = [
      ["تاریخ ایجاد", formatCaseDate(pick(state.caseData, "createdAt", "CreatedAt"))],
      ["تاریخ تکمیل", formatCaseDate(pick(state.caseData, "completedAt", "CompletedAt"))],
    ];
    if (isInternalUser()) {
      metaRows.push(
        ["متقاضی", pick(state.caseData, "applicantFullName", "ApplicantFullName") || "—"],
        ["موبایل متقاضی", pick(state.caseData, "applicantPhoneNumber", "ApplicantPhoneNumber") || "—"]
      );
    }
    const metaWrap = el("div", "card portal-card portal-card--nested");
    metaWrap.appendChild(el("div", "card__title", "اطلاعات پرونده"));
    metaRows.forEach(([label, value]) => {
      const row = el("div", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value || "—"));
      metaWrap.appendChild(row);
    });
    body.appendChild(metaWrap);

    renderProfileSummary(body);
    renderApplicantApplicationReadOnly(body);

    const afWrap = el("div", "card portal-card portal-card--nested");
    afWrap.appendChild(el("div", "card__title", "فرم تصویب (ثبت‌شده)"));
    renderApprovalFormReadOnly(afWrap);
    body.appendChild(afWrap);

    if (shouldShowCompletedCaseDossier(status) || status >= 12) {
      renderIssuedGuaranteeSummary(body);
    }

    const amendment = readAmendmentFromCase();
    const auditHistory = pick(amendment, "history", "History") || [];
    if (amendment && (pick(amendment, "amendmentType", "AmendmentType") || auditHistory.length)) {
      const prev = pick(amendment, "previousValues", "PreviousValues") || {};
      const next = pick(amendment, "newValues", "NewValues") || {};
      if (pick(amendment, "amendmentType", "AmendmentType")) {
        const typeValue = pickAmendmentType(amendment);
        renderReadOnlyBlock(body, "آخرین اصلاحیه", buildAmendmentInfoRows(
          amendment,
          typeValue,
          status,
          pick(readApplicationFromCase(), "validityTo", "ValidityTo") || pick(prev, "validityTo", "ValidityTo"),
          pick(readApplicationFromCase(), "requestedGuaranteeAmount", "RequestedGuaranteeAmount") || pick(prev, "guaranteeAmount", "GuaranteeAmount")
        ));
        if (pick(prev, "validityTo", "ValidityTo") || pick(next, "validityTo", "ValidityTo") || pick(prev, "guaranteeAmount", "GuaranteeAmount") || pick(next, "guaranteeAmount", "GuaranteeAmount")) {
          renderReadOnlyBlock(body, "مقایسه قبل و بعد (اصلاحیه)", [
            ["تاریخ اعتبار قبل", pick(prev, "validityTo", "ValidityTo") || "—"],
            ["تاریخ اعتبار بعد", pick(next, "validityTo", "ValidityTo") || pickAmendmentValidityTo(amendment) || "—"],
            ["مبلغ قبل", formatRialAmount(pick(prev, "guaranteeAmount", "GuaranteeAmount")) || "—"],
            ["مبلغ بعد", formatRialAmount(pick(next, "guaranteeAmount", "GuaranteeAmount") || pickAmendmentAmount(amendment, null)) || "—"],
          ]);
        }
      }
      if (auditHistory.length) {
        const amendmentHistoryWrap = el("div", "card portal-card portal-card--nested");
        amendmentHistoryWrap.appendChild(el("div", "card__title", "سابقه اصلاحیه‌ها"));
        renderAmendmentHistoryInDossier(amendmentHistoryWrap);
        body.appendChild(amendmentHistoryWrap);
      }
    }

    const docsWrap = el("div", "card portal-card portal-card--nested");
    docsWrap.appendChild(el("div", "card__title", "همه مدارک و پیوست‌ها"));
    renderAllDocumentsArchive(docsWrap);
    body.appendChild(docsWrap);

    const commentsWrap = el("div", "card portal-card portal-card--nested");
    commentsWrap.appendChild(el("div", "card__title", "تاریخچه نظرات و درخواست‌های اصلاح"));
    renderDossierComments(commentsWrap);
    body.appendChild(commentsWrap);

    const historyWrap = el("div", "card portal-card portal-card--nested");
    historyWrap.appendChild(el("div", "card__title", "تاریخچه گردش کار"));
    renderFullWorkflowHistory(historyWrap);
    body.appendChild(historyWrap);

    details.appendChild(body);
    card.appendChild(details);
  }

  /** همه فیلدهایی که متقاضی در ورود اطلاعات ثبت کرده — بدون input در مراحل بعدی */
  function renderApplicantApplicationReadOnly(card) {
    const app = readApplicationFromCase();
    if (!app) {
      renderReadOnlyBlock(card, "درخواست متقاضی", [["وضعیت", "هنوز فرم درخواست ذخیره نشده است."]]);
      return;
    }

    renderReadOnlyBlock(card, "درخواست متقاضی (ثبت‌شده در ورود اطلاعات — فقط نمایش)", buildApplicantApplicationRows(app));
  }

  function renderApplicationSummaryReadOnly(card) {
    renderApplicantApplicationReadOnly(card);
  }

  /** برای ذخیره فرم تصویب: فیلدهای متقاضی از application، نه از input مرحله ۴ */
  function applicantFieldsForApprovalPayload() {
    const app = readApplicationFromCase();
    if (!app) {
      return {
        guaranteeType: null,
        guaranteeAmount: null,
        guaranteeAmountInWords: null,
        contractSubject: null,
        beneficiary: null,
        issuanceDate: null,
        expiryDate: null,
        activeDurationDays: null,
        collateralDescription: null,
      };
    }
    const saved = pickApprovalForm();
    const gt = model.normalizeGuaranteeType(pick(app, "guaranteeType", "GuaranteeType"));
    const requestedAmount = pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount");
    return {
      guaranteeType: gt || null,
      guaranteeAmount: requestedAmount ?? null,
      guaranteeAmountInWords:
        pick(saved, "guaranteeAmountInWords", "GuaranteeAmountInWords") ||
        resolveAmountInWords(null, requestedAmount) ||
        resolveAmountInWords(pick(app, "baseContractAmountInWords", "BaseContractAmountInWords"), pick(app, "baseContractAmount", "BaseContractAmount")) ||
        null,
      contractSubject: pick(app, "contractSubject", "ContractSubject") || null,
      beneficiary: pick(app, "beneficiaryName", "BeneficiaryName") || null,
      issuanceDate: pick(app, "validityFrom", "ValidityFrom") || null,
      expiryDate: pick(app, "validityTo", "ValidityTo") || null,
      activeDurationDays: pick(app, "initialValidityDays", "InitialValidityDays") ?? null,
      collateralDescription: pick(app, "collateralDescription", "CollateralDescription") || null,
    };
  }

  function renderDocumentsChecklist(card) {
    const gt = savedGuaranteeType() || formGuaranteeType(card);
    const required = model.requiredDocumentsForSubmit(gt);
    const wrap = el("div", "card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "وضعیت مدارک الزامی"));
    const list = el("ul", "portal-doc-checklist");
    required.forEach((doc) => {
      const ok = !!documentForType(doc.type);
      const li = el("li", ok ? "is-ok" : "is-missing", (ok ? "✓ " : "✗ ") + doc.label);
      list.appendChild(li);
    });
    wrap.appendChild(list);
    card.appendChild(wrap);
  }

  function renderCreditReviewStage(card, canAct) {
    card.appendChild(
      el("div", "portal-stage__subtitle", "بررسی واحد اعتبارات — تأیید یا درخواست اصلاح با ثبت توضیح")
    );
    renderCommentsHistory(card, phaseForStatus(3), "درخواست‌های اصلاح قبلی (این مرحله)", {
      revisionOnly: true,
      emptyText: "هنوز درخواست اصلاحی ثبت نشده است.",
    });
    if (canAct) {
      renderPrimaryActions(card);
      card.appendChild(
        field(
          "پیام اصلاح برای متقاضی (الزامی برای «درخواست اصلاح»)",
          "gCreditRevision",
          "textarea",
          ""
        )
      );
      card.appendChild(
        field(
          "نظر داخلی هنگام تأیید (متقاضی نمی‌بیند — اختیاری)",
          "gCreditInternalComment",
          "textarea",
          ""
        )
      );
    }
  }

  function pickApprovalForm() {
    if (!state.caseData) return null;
    return state.caseData.approvalForm || state.caseData.ApprovalForm || null;
  }

  function formatDateInput(val) {
    if (val == null || val === "") return "";
    const s = String(val);
    return /^\d{4}-\d{2}-\d{2}/.test(s) ? s.slice(0, 10) : s;
  }

  function pickApplicantCreditSnapshot() {
    if (!state.caseData) return null;
    return state.caseData.applicantCreditSnapshot || state.caseData.ApplicantCreditSnapshot || null;
  }

  function formatRialAmount(value) {
    if (value == null || value === "") return "—";
    const n = Number(value);
    if (!Number.isFinite(n)) return String(value);
    return n.toLocaleString("fa-IR") + " ریال";
  }

  function resolveAmountInWords(storedWords, amount) {
    const stored = storedWords != null ? String(storedWords).trim() : "";
    if (stored) return stored;
    if (window.MoneyInWords && amount != null && Number(amount) > 0) {
      return window.MoneyInWords.formatRial(amount);
    }
    return null;
  }

  function isBlankDisplayValue(value) {
    return value == null || value === "" || value === "—";
  }

  function buildApplicantApplicationRows(app) {
    const ctx = model.applicationFieldContext(app);
    const gt = ctx.guaranteeType;
    const kb = pick(app, "isKnowledgeBasedProduct", "IsKnowledgeBasedProduct");
    const baseContractAmount = pick(app, "baseContractAmount", "BaseContractAmount");
    const requestedAmount = pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount");

    const rows = [
      ["guaranteeType", "نوع ضمانت‌نامه", labelFromOptions(gt, model.GUARANTEE_TYPES)],
      ["contractSubject", "موضوع ضمانت‌نامه (قرارداد پایه)", pick(app, "contractSubject", "ContractSubject")],
      [
        "isKnowledgeBasedProduct",
        "محصول دانش‌بنیان",
        kb === true || kb === "true" ? "بله" : kb === false || kb === "false" ? "خیر" : "—",
      ],
      ["beneficiaryName", "نام ذی‌نفع", pick(app, "beneficiaryName", "BeneficiaryName")],
      ["beneficiaryNationalId", "شناسه ملی ذی‌نفع", pick(app, "beneficiaryNationalId", "BeneficiaryNationalId")],
      [
        "beneficiaryCompanyType",
        "نوع شرکت ذی‌نفع",
        labelFromOptions(pick(app, "beneficiaryCompanyType", "BeneficiaryCompanyType"), model.BENEFICIARY_COMPANY_TYPES),
      ],
      [
        "applicantCategory",
        "دسته‌بندی متقاضی",
        labelFromOptions(pick(app, "applicantCategory", "ApplicantCategory"), model.APPLICANT_CATEGORIES),
      ],
      ["applicantCategoryOther", "دسته‌بندی سایر", pick(app, "applicantCategoryOther", "ApplicantCategoryOther")],
      [
        "applicantLegalForm",
        "نوع شرکت متقاضی (حقوقی)",
        labelFromOptions(pick(app, "applicantLegalForm", "ApplicantLegalForm"), model.APPLICANT_LEGAL_FORMS),
      ],
      [
        "baseContractNumber",
        model.applicationFieldLabel("baseContractNumber", "شماره قرارداد پایه / مناقصه", ctx),
        pick(app, "baseContractNumber", "BaseContractNumber"),
      ],
      ["baseContractAmount", "مبلغ قرارداد پایه (ریال)", formatRialAmount(baseContractAmount)],
      [
        "baseContractAmountInWords",
        "مبلغ قرارداد پایه (حروف)",
        resolveAmountInWords(
          pick(app, "baseContractAmountInWords", "BaseContractAmountInWords"),
          baseContractAmount
        ),
      ],
      ["priceAdjustmentRatePercent", "نرخ تعدیل قرارداد (٪)", pick(app, "priceAdjustmentRatePercent", "PriceAdjustmentRatePercent")],
      ["executionProvince", "استان محل اجرا", pick(app, "executionProvince", "ExecutionProvince")],
      ["requestedGuaranteeAmount", "مبلغ ضمانت‌نامه درخواستی (ریال)", formatRialAmount(requestedAmount)],
      ["initialValidityDays", "مدت اعتبار اولیه (روز)", pick(app, "initialValidityDays", "InitialValidityDays")],
      ["validityFrom", "اعتبار از", formatDateInput(pick(app, "validityFrom", "ValidityFrom"))],
      ["validityTo", "اعتبار تا", formatDateInput(pick(app, "validityTo", "ValidityTo"))],
      ["collateralDescription", "تضمین و وثایق قابل ارائه", pick(app, "collateralDescription", "CollateralDescription")],
    ];

    return rows
      .filter(([fieldKey]) => model.isApplicationFieldApplicable(fieldKey, ctx))
      .filter(([fieldKey, , value]) => !model.shouldOmitEmptyApplicationField(fieldKey) || !isBlankDisplayValue(value))
      .map(([, label, value]) => [label, value]);
  }

  /** جدول ۱ — از API (محاسبه از پرونده‌های قبلی متقاضی) */
  function table1CreditFromDatabase() {
    const snap = pickApplicantCreditSnapshot();
    const pickF = (camel, pascal) => pick(snap, camel, pascal);
    return {
      creditLimitWithCheck: pickF("creditLimitWithCheck", "CreditLimitWithCheck"),
      fundIssuedGuaranteesTotal: pickF("fundIssuedGuaranteesTotal", "FundIssuedGuaranteesTotal"),
      activeCommitments: pickF("activeCommitments", "ActiveCommitments"),
      remainingCredit: pickF("remainingCredit", "RemainingCredit"),
      periodStart: pickF("periodStart", "PeriodStart"),
      expiresAt: pickF("expiresAt", "ExpiresAt"),
    };
  }

  /** فقط فیلدهایی که واحد اعتبارات در فرم تصویب وارد می‌کند (نه تکرار ورود متقاضی) */
  function approvalFormCreditValues() {
    const saved = pickApprovalForm();
    const pickF = (camel, pascal) => pick(saved, camel, pascal);
    const table1 = table1CreditFromDatabase();
    return {
      ...table1,
      depositRatePercent: pickF("depositRatePercent", "DepositRatePercent"),
      depositAmount: pickF("depositAmount", "DepositAmount"),
      annualCommissionRatePercent: pickF("annualCommissionRatePercent", "AnnualCommissionRatePercent"),
      commissionAmount: pickF("commissionAmount", "CommissionAmount"),
      guarantorsDescription: pickF("guarantorsDescription", "GuarantorsDescription"),
      otherNotes: pickF("otherNotes", "OtherNotes"),
    };
  }

  function readApprovalForm() {
    const num = (id) => {
      const v = readValue(id);
      if (!v) return null;
      const n = Number(v);
      return Number.isFinite(n) ? n : null;
    };
    const credit = approvalFormCreditValues();
    const table1 = table1CreditFromDatabase();
    return {
      ...applicantFieldsForApprovalPayload(),
      creditLimitWithCheck: table1.creditLimitWithCheck ?? credit.creditLimitWithCheck ?? null,
      fundIssuedGuaranteesTotal: table1.fundIssuedGuaranteesTotal ?? credit.fundIssuedGuaranteesTotal ?? null,
      activeCommitments: table1.activeCommitments ?? credit.activeCommitments ?? null,
      remainingCredit: table1.remainingCredit ?? credit.remainingCredit ?? null,
      depositRatePercent: num("gAfDepositRate") ?? credit.depositRatePercent ?? null,
      depositAmount: num("gAfDepositAmount") ?? credit.depositAmount ?? null,
      annualCommissionRatePercent: num("gAfCommissionRate") ?? credit.annualCommissionRatePercent ?? null,
      commissionAmount: num("gAfCommissionAmount") ?? credit.commissionAmount ?? null,
      guarantorsDescription: readValue("gAfGuarantors") || credit.guarantorsDescription || null,
      otherNotes: readValue("gAfOtherNotes") || credit.otherNotes || null,
    };
  }

  function renderApprovalFormStage(card, canAct) {
    const v = approvalFormCreditValues();
    const t1vals = table1CreditFromDatabase();

    card.appendChild(
      el(
        "div",
        "portal-stage__subtitle",
        "فرم تصویب — اطلاعات متقاضی و جدول ۱ از دیتابیس خوانده می‌شوند. واحد اعتبارات فقط ودیعه، کارمزد و ضامنین را وارد می‌کند."
      )
    );

    const t1 = el("div", "card portal-card portal-card--nested");
    t1.appendChild(el("div", "card__title", "جدول ۱ — وضعیت اعتباری صندوق (ریال)"));
    const periodHint =
      t1vals.periodStart && t1vals.expiresAt
        ? "محاسبه فقط در بازه سقف صندوق: از " + t1vals.periodStart + " تا " + t1vals.expiresAt + "."
        : "سقف صندوق هنوز توسط مدیرعامل تعیین نشده یا بازه فعال نیست.";
    t1.appendChild(el("div", "muted portal-stage__hint", periodHint));
    t1.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        "صادره = ضمانت‌نامه‌های تکمیل‌شده در همین بازه؛ تعهدات فعال = پرونده‌های در جریان ثبت‌شده در همین بازه. ارسال فرم در صورت تجاوز از سقف رد می‌شود."
      )
    );
    renderReadOnlyBlock(t1, null, [
      ["اعتبار ضمانت‌نامه با چک", formatRialAmount(t1vals.creditLimitWithCheck)],
      ["ضمانت‌نامه‌های صادره صندوق", formatRialAmount(t1vals.fundIssuedGuaranteesTotal)],
      ["تعهدات فعال", formatRialAmount(t1vals.activeCommitments)],
      ["اعتبار باقی‌مانده", formatRialAmount(t1vals.remainingCredit)],
    ]);
    card.appendChild(t1);

    const t2 = el("div", "card portal-card portal-card--nested");
    t2.appendChild(el("div", "card__title", "جدول ۲ — تکمیل اعتبارات (ودیعه، کارمزد، ضامنین)"));
    t2.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        "نوع ضمانت‌نامه، مبلغ، موضوع، ذی‌نفع، تاریخ‌ها و وثایق از درخواست متقاضی بالا خوانده می‌شود و در ذخیره خودکار لحاظ می‌گردد."
      )
    );
    t2.appendChild(
      field("نرخ ودیعه (٪)", "gAfDepositRate", "number", v.depositRatePercent, { min: 0, max: 999.99, step: 0.01 })
    );
    t2.appendChild(field("مبلغ ودیعه (ریال)", "gAfDepositAmount", "number", v.depositAmount));
    t2.appendChild(
      field("نرخ سالانه کارمزد (٪)", "gAfCommissionRate", "number", v.annualCommissionRatePercent, {
        min: 0,
        max: 999.99,
        step: 0.01,
      })
    );
    t2.appendChild(field("مبلغ کارمزد (ریال)", "gAfCommissionAmount", "number", v.commissionAmount));
    t2.appendChild(field("تعداد ضامنین و شرایط", "gAfGuarantors", "textarea", v.guarantorsDescription));
    t2.appendChild(field("سایر توضیحات لازم", "gAfOtherNotes", "textarea", v.otherNotes));
    card.appendChild(t2);

    if (canAct) {
      renderPrimaryActions(card);
    }
  }

  function renderFinancialReviewStage(card, canAct) {
    card.appendChild(
      el("div", "portal-stage__subtitle", "بررسی پیوست‌های مالی — تأیید یا درخواست اصلاح (اطلاعات و فایل‌ها در «پرونده کامل» بالا)")
    );
    renderCommentsHistory(card, phaseForStatus(8), "درخواست‌های اصلاح قبلی (این مرحله)", {
      revisionOnly: true,
      emptyText: "هنوز درخواست اصلاحی ثبت نشده است.",
    });
    if (canAct) {
      renderPrimaryActions(card);
      card.appendChild(
        field("پیام اصلاح برای متقاضی (الزامی برای «درخواست اصلاح»)", "gFinRevision", "textarea", "")
      );
      card.appendChild(
        field(
          "نظر داخلی هنگام تأیید (متقاضی نمی‌بیند — اختیاری)",
          "gFinInternalComment",
          "textarea",
          ""
        )
      );
    }
  }

  function renderAmendmentCreationForm(card, status, canAct) {
    if (!canAct) return;

    const amendment = readAmendmentFromCase() || {};
    const cancellation = readCancellationDetails() || {};
    const typeValue = Number(
      pick(amendment, "amendmentType", "AmendmentType") ||
      pick(cancellation, "amendmentType", "AmendmentType") ||
      0
    );
    const isFreshEntry = status === 12 || status === 22 || status === 23;
    const isCancellation = typeValue === 3 || (isFreshEntry && Number(qs("#gAmendmentType")?.value || 0) === 3);

    const box = el("div", "portal-form card portal-card portal-card--nested");
    const initialType = isFreshEntry ? 1 : (typeValue || 1);
    box.appendChild(el("div", "card__title", isCancellation ? "ثبت ابطال ضمانت‌نامه" : "ثبت اصلاحیه ضمانت‌نامه"));
    box.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        status === 12
          ? "پرونده تکمیل شده است. در صورت نیاز می‌توانید درخواست تمدید، تقلیل یا ابطال ثبت کنید."
          : status === 22 || status === 23
            ? "اصلاحیه قبلی به پایان رسیده است. در صورت نیاز می‌توانید درخواست جدید (تمدید، تقلیل یا ابطال) ثبت کنید."
            : "اطلاعات اصلاحیه را تکمیل و ذخیره کنید."
      )
    );
    box.appendChild(
      selectField("نوع اصلاحیه", "gAmendmentType", [
        { value: "1", label: "تمدید" },
        { value: "2", label: "تقلیل" },
        { value: "3", label: "ابطال" },
      ], initialType)
    );
    const validityRow = field(
      "تاریخ اعتبار جدید",
      "gAmendmentValidityTo",
      "date",
      isFreshEntry ? "" : pick(amendment, "requestedValidityTo", "RequestedValidityTo")
    );
    validityRow.hidden = initialType !== 1;
    box.appendChild(validityRow);
    const amountRow = field(
      "مبلغ جدید",
      "gAmendmentAmount",
      "number",
      isFreshEntry ? "" : pick(amendment, "requestedAmount", "RequestedAmount")
    );
    amountRow.hidden = initialType !== 2;
    box.appendChild(amountRow);
    box.appendChild(
      field("علت", "gAmendmentReason", "textarea", isFreshEntry ? "" : pick(amendment, "reason", "Reason"))
    );
    card.appendChild(box);

    let cancellationUploads = null;

    const actionsWrap = el("div", "card portal-card portal-card--nested");
    actionsWrap.appendChild(el("div", "card__title", typeValue === 3 ? "اقدامات ابطال" : "اقدامات اصلاحیه"));
    const row = el("div", "row");
    const saveBtn = el("button", "btn btn--primary", typeValue === 3 ? "ذخیره ابطال" : "ذخیره اصلاحیه");
    saveBtn.type = "button";
    saveBtn.addEventListener("click", () => void handleAction({ id: "save-amendment", method: "POST", path: "/amendment/create" }));
    row.appendChild(saveBtn);
    let submitBtn = null;
    if (status === 16 || status === 17) {
      submitBtn = el("button", "btn btn--primary", typeValue === 3 ? "ارسال ابطال" : "ارسال اصلاحیه");
      submitBtn.type = "button";
      submitBtn.addEventListener("click", () => void handleAction({ id: "submit-amendment", method: "POST", path: "/amendment/submit" }));
      row.appendChild(submitBtn);
    }
    actionsWrap.appendChild(row);
    card.appendChild(actionsWrap);

    const syncAmendmentTypeFields = function () {
      const type = Number(qs("#gAmendmentType", card)?.value || 0);
      const validity = qs("#gAmendmentValidityTo", card)?.closest(".formrow");
      const amount = qs("#gAmendmentAmount", card)?.closest(".formrow");
      if (validity) validity.hidden = type !== 1;
      if (amount) amount.hidden = type !== 2;

      const formTitle = box.querySelector(".card__title");
      if (formTitle) {
        formTitle.textContent = type === 3 ? "ثبت ابطال ضمانت‌نامه" : "ثبت اصلاحیه ضمانت‌نامه";
      }
      const actionsTitle = actionsWrap.querySelector(".card__title");
      if (actionsTitle) {
        actionsTitle.textContent = type === 3 ? "اقدامات ابطال" : "اقدامات اصلاحیه";
      }
      saveBtn.textContent = type === 3 ? "ذخیره ابطال" : "ذخیره اصلاحیه";
      if (submitBtn) submitBtn.textContent = type === 3 ? "ارسال ابطال" : "ارسال اصلاحیه";

      if (type === 3) {
        if (!cancellationUploads) {
          cancellationUploads = el("div", "portal-cancellation-documents");
          renderCancellationUploads(cancellationUploads);
          box.insertAdjacentElement("afterend", cancellationUploads);
        }
      } else if (cancellationUploads) {
        cancellationUploads.remove();
        cancellationUploads = null;
      }
    };
    syncAmendmentTypeFields();
    qs("#gAmendmentType", card)?.addEventListener("change", syncAmendmentTypeFields);
  }

  function renderAmendmentStage(card, status, canAct) {
    const amendment = readAmendmentFromCase() || {};
    const cancellation = readCancellationDetails() || {};
    const typeValue = Number(
      pick(amendment, "amendmentType", "AmendmentType") ||
      pick(cancellation, "amendmentType", "AmendmentType") ||
      0
    );
    const prev = pick(amendment, "previousValues", "PreviousValues") || {};
    const next = pick(amendment, "newValues", "NewValues") || {};
    const auditHistory = pick(amendment, "history", "History") || [];
    const currentValidityTo = pick(readApplicationFromCase(), "validityTo", "ValidityTo")
      || pick(state.caseData && (state.caseData.approvalForm || state.caseData.ApprovalForm), "expiryDate", "ExpiryDate")
      || "";
    const currentAmount = pick(readApplicationFromCase(), "requestedGuaranteeAmount", "RequestedGuaranteeAmount")
      || pick(state.caseData && (state.caseData.approvalForm || state.caseData.ApprovalForm), "guaranteeAmount", "GuaranteeAmount")
      || "";

    const processLabel = typeValue === 3 ? "ابطال" : "اصلاحیه";

    renderReadOnlyBlock(card, "اطلاعات " + processLabel, buildAmendmentInfoRows(
      amendment,
      typeValue,
      status,
      currentValidityTo,
      currentAmount
    ));

    renderIssuedGuaranteeSummary(card);

    if (typeValue) {
      renderReadOnlyBlock(card, "مقایسه قبل و بعد", [
        ["تاریخ اعتبار قبل", pick(prev, "validityTo", "ValidityTo") || currentValidityTo || "—"],
        ["تاریخ اعتبار بعد", pick(next, "validityTo", "ValidityTo") || pickAmendmentValidityTo(amendment) || "—"],
        ["مبلغ قبل", formatRialAmount(pick(prev, "guaranteeAmount", "GuaranteeAmount") || currentAmount) || "—"],
        ["مبلغ بعد", formatRialAmount(pick(next, "guaranteeAmount", "GuaranteeAmount") || pickAmendmentAmount(amendment, currentAmount)) || "—"],
      ]);
    }

    if (auditHistory.length) {
      const timeline = el("div", "card portal-card portal-card--nested");
      timeline.appendChild(el("div", "card__title", "سابقه " + processLabel));
      auditHistory.forEach((item) => {
        const row = el("div", "portal-thread__item");
        const createdBy = pick(item, "createdByFullName", "CreatedByFullName") || pick(item, "createdBy", "CreatedBy") || "—";
        const approvalBy = pick(item, "approvalUserFullName", "ApprovalUserFullName") || pick(item, "approvalUser", "ApprovalUser") || "—";
        const decisionReason = pick(item, "decisionReason", "DecisionReason") || "—";
        row.appendChild(el("div", "portal-thread__meta muted", amendmentReviewStateLabel(pick(item, "status", "Status"))));
        row.appendChild(el("div", "portal-thread__message", "ثبت‌کننده: " + createdBy));
        row.appendChild(el("div", "portal-thread__message", "علت: " + (pick(item, "reason", "Reason") || "—")));
        row.appendChild(el("div", "portal-thread__message", "تایید/رد توسط: " + approvalBy));
        row.appendChild(el("div", "portal-thread__message", "توضیح تصمیم: " + decisionReason));
        timeline.appendChild(row);
      });
      card.appendChild(timeline);
    }

    if (canAct && canStartNewAmendment(status)) {
      renderAmendmentCreationForm(card, status, canAct);
    }

    if (canAct && (status === 18 || status === 19 || (status === 20 && typeValue !== 3))) {
      if (status === 18 || status === 20) {
        card.appendChild(field("توضیح تایید", "gAmendmentApproveComment", "textarea", ""));
        card.appendChild(field("توضیح داخلی", "gAmendmentInternalComment", "textarea", ""));
        card.appendChild(field("علت رد / برگشت", "gAmendmentRejectReason", "textarea", ""));
      } else if (status === 19) {
        card.appendChild(field("توضیح تأیید / رد", "gAmendmentApproveComment", "textarea", ""));
      }
      const actionsWrap = el("div", "card portal-card portal-card--nested");
      actionsWrap.appendChild(el("div", "card__title", status === 19
        ? "تأیید مدیرعامل"
        : (typeValue === 3 ? "بررسی ابطال" : "بررسی اصلاحیه")));
      const row = el("div", "row");
      if (status === 19) {
        if (typeValue === 3) {
          const approveBtn = el("button", "btn btn--primary", "تایید ابطال");
          approveBtn.type = "button";
          approveBtn.addEventListener("click", () => {
            void handleAction({
              id: "approve-cancellation",
              method: "POST",
              path: "/amendment/cancellation/approve",
            });
          });
          row.appendChild(approveBtn);
        } else {
          const approveBtn = el("button", "btn btn--primary", "تایید اصلاحیه");
          approveBtn.type = "button";
          approveBtn.addEventListener("click", () => void handleAction({
            id: "ceo-amendment-approve",
            method: "POST",
            path: "/ceo/amendment/approve",
          }));
          row.appendChild(approveBtn);
        }
        const rejectBtn = el("button", "btn btn--warn", typeValue === 3 ? "رد ابطال" : "رد اصلاحیه");
        rejectBtn.type = "button";
        rejectBtn.addEventListener("click", () => void handleAction({
          id: "ceo-amendment-reject",
          method: "POST",
          path: "/ceo/amendment/reject",
        }));
        row.appendChild(rejectBtn);
      } else {
        const approveBtn = el("button", "btn btn--primary", typeValue === 3 ? "تایید ابطال" : "تایید اصلاحیه");
        approveBtn.type = "button";
        approveBtn.addEventListener("click", () => {
          void handleAction({
          id: status === 20 && typeValue === 3 ? "approve-cancellation" : "approve-amendment",
          method: "POST",
          path: status === 20 && typeValue === 3 ? "/amendment/cancellation/approve" : "/amendment/approve",
        });
        });
        row.appendChild(approveBtn);
        const rejectBtn = el("button", "btn btn--warn", typeValue === 3 ? "رد ابطال" : "رد اصلاحیه");
        rejectBtn.type = "button";
        rejectBtn.addEventListener("click", () => void handleAction({ id: "reject-amendment", method: "POST", path: "/amendment/reject" }));
        row.appendChild(rejectBtn);
        const revisionBtn = el("button", "btn", "درخواست اصلاح");
        revisionBtn.type = "button";
        revisionBtn.addEventListener("click", () => {
          void handleAction({ id: "amendment-revision", method: "POST", path: "/amendment/revision-request" });
        });
        row.appendChild(revisionBtn);
      }
      actionsWrap.appendChild(row);
      card.appendChild(actionsWrap);
    }
  }

  function renderPrimaryActions(parent) {
    const status = pickStatus(state.caseData);
    const step = stepForCase(status);
    const role = getSessionRole();
    if (!model.canActOnCase(role, step.unit)) return;

    const actions = actionsForStatus(status);
    if (!actions.length) return;

    const wrap = el("div", "card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "اقدامات این مرحله"));
    const row = el("div", "row");
    actions.forEach((a) => {
      const btnClass = a.variant === "warn" ? "btn btn--warn" : "btn btn--primary";
      const btn = el("button", btnClass, a.label);
      btn.type = "button";
      if (status === 2 && a.id === "submit-app" && !requiredDocumentsComplete(savedGuaranteeType())) {
        btn.classList.add("btn--warn");
        btn.title = "ابتدا همه مدارک ضروری را بارگذاری کنید";
      }
      btn.addEventListener("click", () => void handleAction(a));
      row.appendChild(btn);
    });
    wrap.appendChild(row);
    parent.appendChild(wrap);
  }

  function renderProfileSummary(card) {
    const company = pickCompany(state.caseData);
    const contact = pickApplicantContact();
    const wrap = el("div", "portal-profile-summary card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "اطلاعات پروفایل (فقط نمایش — از User/Company)"));
    const rows = [
      ["نام شرکت متقاضی", company ? pick(company, "name", "Name") : "— (حقیقی)"],
      ["شناسه ملی شرکت", company ? pick(company, "nationalId", "NationalId") : "—"],
      ["تلفن شرکت", company ? pick(company, "phoneNumber", "PhoneNumber") : "—"],
      ["نام و نام خانوادگی نماینده", contact.fullName],
      ["شماره تماس نماینده", contact.phone],
      ["کد ملی نماینده", contact.nationalCode],
    ];
    rows.forEach(([label, value]) => {
      const row = el("div", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value || "—"));
      wrap.appendChild(row);
    });
    card.appendChild(wrap);
  }

  function field(label, id, type, value, opts) {
    const row = el("div", "formrow");
    row.appendChild(el("label", "", label));
    let input;
    if (type === "textarea") {
      input = document.createElement("textarea");
      input.rows = 3;
    } else {
      input = document.createElement("input");
      input.type = type === "number" ? "number" : type === "date" ? "date" : "text";
    }
    input.id = id;
    if (opts && opts.placeholder) input.placeholder = opts.placeholder;
    if (type === "number" && opts) {
      if (opts.min != null) input.min = String(opts.min);
      if (opts.max != null) input.max = String(opts.max);
      if (opts.step != null) input.step = String(opts.step);
    }
    if (value != null && value !== "") input.value = String(value);
    row.appendChild(input);
    return row;
  }

  function selectField(label, id, options, value) {
    const row = el("div", "formrow");
    row.appendChild(el("label", "", label));
    const sel = document.createElement("select");
    sel.id = id;
    options.forEach((opt) => {
      const o = document.createElement("option");
      o.value = String(opt.value);
      o.textContent = opt.label;
      sel.appendChild(o);
    });
    const normalized =
      (id === "gGuaranteeType" || id === "gAfGuaranteeType") && model.normalizeGuaranteeType
        ? model.normalizeGuaranteeType(value)
        : value;
    if (normalized != null && normalized !== "" && normalized !== 0) sel.value = String(normalized);
    row.appendChild(sel);
    return row;
  }

  function readApplicationForm() {
    const text = (id) => {
      const v = qs("#" + id)?.value?.trim();
      return v || null;
    };
    const num = (id) => {
      const v = qs("#" + id)?.value?.trim();
      if (!v) return null;
      const n = Number(v);
      return Number.isFinite(n) ? n : null;
    };
    const gt = qs("#gGuaranteeType")?.value;
    const baseContractAmount = num("gBaseContractAmount");
    const payload = {
      guaranteeType: gt ? Number(gt) : null,
      contractSubject: text("gContractSubject"),
      isKnowledgeBasedProduct: qs("#gKnowledgeBased")?.value === "true",
      beneficiaryName: text("gBeneficiaryName"),
      beneficiaryNationalId: text("gBeneficiaryNationalId"),
      beneficiaryCompanyType: num("gBeneficiaryCompanyType"),
      applicantCategory: Number(qs("#gApplicantCategory")?.value || 0),
      applicantCategoryOther: text("gApplicantCategoryOther"),
      applicantLegalForm: num("gApplicantLegalForm"),
      baseContractNumber: text("gBaseContractNumber"),
      baseContractAmount,
      baseContractAmountInWords:
        text("gBaseContractAmountWords") || resolveAmountInWords(null, baseContractAmount),
      priceAdjustmentRatePercent: num("gPriceAdjustmentRate"),
      executionProvince: text("gExecutionProvince"),
      requestedGuaranteeAmount: num("gRequestedAmount"),
      initialValidityDays: num("gInitialValidityDays"),
      validityFrom: text("gValidityFrom") || null,
      validityTo: text("gValidityTo") || null,
      collateralDescription: text("gCollateral"),
    };
    return payload;
  }

  function fillApplicationForm(app) {
    if (!app) return;
    const set = (id, val) => {
      const node = qs("#" + id);
      if (node && val != null && val !== "") node.value = String(val);
    };
    set("gGuaranteeType", pick(app, "guaranteeType", "GuaranteeType"));
    set("gContractSubject", pick(app, "contractSubject", "ContractSubject"));
    const kb = pick(app, "isKnowledgeBasedProduct", "IsKnowledgeBasedProduct");
    if (qs("#gKnowledgeBased")) qs("#gKnowledgeBased").value = kb ? "true" : "false";
    set("gBeneficiaryName", pick(app, "beneficiaryName", "BeneficiaryName"));
    set("gBeneficiaryNationalId", pick(app, "beneficiaryNationalId", "BeneficiaryNationalId"));
    set("gBeneficiaryCompanyType", pick(app, "beneficiaryCompanyType", "BeneficiaryCompanyType"));
    set("gApplicantCategory", pick(app, "applicantCategory", "ApplicantCategory"));
    set("gApplicantCategoryOther", pick(app, "applicantCategoryOther", "ApplicantCategoryOther"));
    set("gApplicantLegalForm", pick(app, "applicantLegalForm", "ApplicantLegalForm"));
    set("gBaseContractNumber", pick(app, "baseContractNumber", "BaseContractNumber"));
    set("gBaseContractAmount", pick(app, "baseContractAmount", "BaseContractAmount"));
    set(
      "gBaseContractAmountWords",
      resolveAmountInWords(
        pick(app, "baseContractAmountInWords", "BaseContractAmountInWords"),
        pick(app, "baseContractAmount", "BaseContractAmount")
      )
    );
    set("gPriceAdjustmentRate", pick(app, "priceAdjustmentRatePercent", "PriceAdjustmentRatePercent"));
    set("gExecutionProvince", pick(app, "executionProvince", "ExecutionProvince"));
    set("gRequestedAmount", pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount"));
    set("gInitialValidityDays", pick(app, "initialValidityDays", "InitialValidityDays"));
    set("gValidityFrom", pick(app, "validityFrom", "ValidityFrom"));
    set("gValidityTo", pick(app, "validityTo", "ValidityTo"));
    set("gCollateral", pick(app, "collateralDescription", "CollateralDescription"));
  }

  function renderApplicationForm(card) {
    const app = state.caseData.application || state.caseData.Application;
    const formGuaranteeType = qs("#gGuaranteeType")?.value ?? pick(app, "guaranteeType", "GuaranteeType");
    const formApplicantCategory = qs("#gApplicantCategory")?.value ?? pick(app, "applicantCategory", "ApplicantCategory");
    const ctx = model.applicationFieldContext(app, formGuaranteeType);
    ctx.applicantCategory = Number(formApplicantCategory || ctx.applicantCategory || 0);
    const show = (fieldKey) => model.isApplicationFieldApplicable(fieldKey, ctx);
    const baseContractAmount = pick(app, "baseContractAmount", "BaseContractAmount");
    const baseContractAmountWords = resolveAmountInWords(
      pick(app, "baseContractAmountInWords", "BaseContractAmountInWords"),
      baseContractAmount
    );

    const box = el("div", "portal-form");
    box.appendChild(el("div", "portal-stage__subtitle", "اطلاعات درخواست ضمانت‌نامه"));
    box.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        "نام شرکت، شناسه ملی و نماینده از پروفایل User/Company خوانده می‌شود و در این فرم تکرار نمی‌شود."
      )
    );

    box.appendChild(
      selectField("نوع ضمانت‌نامه درخواستی", "gGuaranteeType", model.GUARANTEE_TYPES, pick(app, "guaranteeType", "GuaranteeType"))
    );
    const gtSelect = card.querySelector("#gGuaranteeType");
    if (gtSelect && !gtSelect.dataset.wiredChange) {
      gtSelect.dataset.wiredChange = "1";
      gtSelect.addEventListener("change", () => render());
    }
    box.appendChild(field("موضوع ضمانت‌نامه (موضوع قرارداد پایه)", "gContractSubject", "text", pick(app, "contractSubject", "ContractSubject")));
    box.appendChild(
      selectField("ضمانت‌نامه مرتبط با فروش محصول دانش‌بنیان", "gKnowledgeBased", [
        { value: "false", label: "خیر" },
        { value: "true", label: "بله" },
      ], pick(app, "isKnowledgeBasedProduct", "IsKnowledgeBasedProduct") ? "true" : "false")
    );
    box.appendChild(field("نام دقیق ذی‌نفع ضمانت‌نامه", "gBeneficiaryName", "text", pick(app, "beneficiaryName", "BeneficiaryName")));
    box.appendChild(field("شناسه ملی ذی‌نفع", "gBeneficiaryNationalId", "text", pick(app, "beneficiaryNationalId", "BeneficiaryNationalId")));
    box.appendChild(
      selectField("نوع شرکت ذی‌نفع", "gBeneficiaryCompanyType", model.BENEFICIARY_COMPANY_TYPES, pick(app, "beneficiaryCompanyType", "BeneficiaryCompanyType"))
    );
    box.appendChild(
      selectField("دسته‌بندی متقاضی", "gApplicantCategory", model.APPLICANT_CATEGORIES, pick(app, "applicantCategory", "ApplicantCategory"))
    );
    const categorySelect = card.querySelector("#gApplicantCategory");
    if (categorySelect && !categorySelect.dataset.wiredChange) {
      categorySelect.dataset.wiredChange = "1";
      categorySelect.addEventListener("change", () => render());
    }
    if (show("applicantCategoryOther")) {
      box.appendChild(field("دسته‌بندی سایر (توضیح)", "gApplicantCategoryOther", "text", pick(app, "applicantCategoryOther", "ApplicantCategoryOther")));
    }
    box.appendChild(
      selectField("نوع شرکت متقاضی (حقوقی)", "gApplicantLegalForm", model.APPLICANT_LEGAL_FORMS, pick(app, "applicantLegalForm", "ApplicantLegalForm"))
    );
    if (show("baseContractNumber")) {
      box.appendChild(
        field(
          model.applicationFieldLabel("baseContractNumber", "شماره قرارداد پایه / مناقصه", ctx),
          "gBaseContractNumber",
          "text",
          pick(app, "baseContractNumber", "BaseContractNumber")
        )
      );
    }
    if (show("baseContractAmount")) {
      box.appendChild(field("مبلغ قرارداد پایه (ریال)", "gBaseContractAmount", "number", pick(app, "baseContractAmount", "BaseContractAmount")));
    }
    if (show("baseContractAmountInWords")) {
      box.appendChild(
        field("مبلغ قرارداد پایه (حروف)", "gBaseContractAmountWords", "text", baseContractAmountWords, {
          placeholder: "در صورت خالی بودن، هنگام ذخیره از مبلغ ریالی محاسبه می‌شود",
        })
      );
    }
    if (show("priceAdjustmentRatePercent")) {
      box.appendChild(
        field("نرخ تعدیل قرارداد (٪ — نه مبلغ ریالی)", "gPriceAdjustmentRate", "number", pick(app, "priceAdjustmentRatePercent", "PriceAdjustmentRatePercent"), {
          min: 0,
          max: 999.99,
          step: 0.01,
          placeholder: "مثلاً 15.5",
        })
      );
    }
    if (show("executionProvince")) {
      box.appendChild(field("استان محل اجرا", "gExecutionProvince", "text", pick(app, "executionProvince", "ExecutionProvince")));
    }
    box.appendChild(field("مبلغ ضمانت‌نامه درخواستی (ریال)", "gRequestedAmount", "number", pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount")));
    box.appendChild(field("مدت اعتبار اولیه (روز)", "gInitialValidityDays", "number", pick(app, "initialValidityDays", "InitialValidityDays")));
    box.appendChild(field("اعتبار از تاریخ", "gValidityFrom", "date", pick(app, "validityFrom", "ValidityFrom")));
    box.appendChild(field("اعتبار تا تاریخ", "gValidityTo", "date", pick(app, "validityTo", "ValidityTo")));
    box.appendChild(field("تضمین و وثایق قابل ارائه", "gCollateral", "textarea", pick(app, "collateralDescription", "CollateralDescription")));

    card.appendChild(box);
  }

  function nextUploadFieldId(prefix) {
    uploadFieldCounter += 1;
    return (prefix || "g-upload") + "-" + uploadFieldCounter;
  }

  function appendFileUploadRow(parent, options) {
    const row = el("div", "portal-upload-row");
    const inputId = options.id || nextUploadFieldId("g-doc");

    const meta = el("div", "portal-upload-row__meta");
    const title = el("div", "portal-upload-row__title", options.title || "");
    if (options.required) {
      const req = el("span", "portal-upload-row__required", "ضروری");
      title.appendChild(document.createTextNode(" "));
      title.appendChild(req);
    }
    meta.appendChild(title);
    if (options.hint) meta.appendChild(el("div", "muted", options.hint));
    row.appendChild(meta);

    const control = el("div", "portal-upload-row__control");
    const input = document.createElement("input");
    input.type = "file";
    input.id = inputId;
    input.className = "portal-file-input";
    input.accept = ".pdf,.png,.jpg,.jpeg,.doc,.docx,application/pdf,image/*";
    if (options.uploadType != null) input.dataset.uploadType = String(options.uploadType);

    const picker = document.createElement("label");
    picker.className = "portal-file-btn";
    picker.htmlFor = inputId;
    picker.textContent = "انتخاب فایل";

    const status = el("span", "portal-upload-row__status muted");
    if (options.uploadedLabel) {
      status.textContent = options.uploadedLabel;
      status.classList.add("is-uploaded");
      row.classList.add("portal-upload-row--done");
    }

    input.addEventListener("change", () => {
      if (input.dataset.uploading === "1") return;
      const file = input.files && input.files[0];
      if (!file) return;
      status.textContent = "در حال بارگذاری: " + file.name;
      status.classList.remove("is-uploaded");
      input.dataset.uploading = "1";
      handleUpload(input)
        .catch((e) => setError(e.message || String(e)))
        .finally(() => {
          input.dataset.uploading = "0";
        });
    });

    control.appendChild(input);
    control.appendChild(picker);
    control.appendChild(status);
    row.appendChild(control);
    parent.appendChild(row);
  }

  function renderUploads(card) {
    const { saved: savedGt, form: formGt, effective: validateGt } = resolveGuaranteeTypes(card);
    const defs = model.uploadDocumentDefs(savedGt, formGt);
    const wrap = el("div", "card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "بارگذاری مدارک"));
    wrap.appendChild(el("div", "muted", "پس از انتخاب فایل، بارگذاری (presign → S3 → confirm) خودکار انجام می‌شود."));

    const requiredDefs = model.requiredDocumentsForSubmit(validateGt);
    const uploadedRequired = requiredDefs.filter((d) => documentForType(d.type)).length;
    wrap.appendChild(
      el("div", "muted", "مدارک ضروری: " + uploadedRequired + " از " + requiredDefs.length)
    );

    const labelFor = (gt) => (model.GUARANTEE_TYPES.find((t) => t.value === gt) || {}).label || "—";
    wrap.appendChild(
      el(
        "div",
        "muted",
        "نوع ذخیره‌شده: " + labelFor(savedGt) + " · انتخاب فعلی فرم: " + labelFor(formGt)
      )
    );

    if (savedGt && formGt && savedGt !== formGt) {
      wrap.appendChild(
        el(
          "div",
          "portal-stage__hint",
          "نوع ضمانت‌نامه فرم با مقدار ذخیره‌شده فرق دارد. قبل از ارسال «ذخیره درخواست» را بزنید؛ تا آن وقت مدارک هر دو نوع در فهرست می‌مانند."
        )
      );
    }

    const conditional = defs.filter(
      (d) => d.whenGuaranteeType != null || (d.whenGuaranteeTypes && d.whenGuaranteeTypes.length)
    );
    const general = defs.filter(
      (d) => d.whenGuaranteeType == null && !(d.whenGuaranteeTypes && d.whenGuaranteeTypes.length)
    );

    function appendDocRows(list, validateForGt) {
      list.forEach((doc) => {
        const existing = documentForType(doc.type);
        const fileName = existing && pick(existing, "fileName", "FileName");
        const isRequired = model.isDocRequiredForType(doc, validateForGt);
        appendFileUploadRow(wrap, {
          id: "g-doc-" + doc.type,
          title: doc.label,
          hint: doc.hint,
          uploadType: doc.type,
          required: isRequired,
          uploadedLabel: existing ? "✓ بارگذاری شده" + (fileName ? ": " + fileName : "") : null,
        });
      });
    }

    if (conditional.length) {
      wrap.appendChild(el("div", "card__title", "مدارک مخصوص نوع ضمانت‌نامه (اول این بخش را تکمیل کنید)"));
      appendDocRows(conditional, validateGt);
    }
    if (general.length) {
      wrap.appendChild(el("div", "card__title", "مدارک عمومی"));
      appendDocRows(general, validateGt);
    }
    card.appendChild(wrap);
  }

  function renderCancellationUploads(parent) {
    const docs = model.CANCELLATION_DOCUMENTS || [];
    if (!docs.length) return;

    const wrap = el("div", "card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "مدارک ابطال"));
    wrap.appendChild(el("div", "muted", "پس از انتخاب فایل، بارگذاری (presign → S3 → confirm) خودکار انجام می‌شود."));

    docs.forEach((doc) => {
      const existing = documentForType(doc.type);
      const fileName = existing && pick(existing, "fileName", "FileName");
      appendFileUploadRow(wrap, {
        id: "g-cancel-doc-" + doc.type,
        title: doc.label,
        hint: doc.hint || "",
        uploadType: doc.type,
        required: !!doc.required,
        uploadedLabel: existing ? "✓ بارگذاری شده" + (fileName ? ": " + fileName : "") : null,
      });
    });

    parent.appendChild(wrap);
  }

  function cancellationDocumentsComplete() {
    return (model.CANCELLATION_DOCUMENTS || [])
      .filter((doc) => doc.required)
      .every((doc) => documentForType(doc.type));
  }

  function documentsForType(documentType) {
    const t = model.normalizeDocumentType(documentType);
    return state.documents.filter(
      (d) => model.normalizeDocumentType(pick(d, "documentType", "DocumentType")) === t
    );
  }

  function renderWorkflowStageUploads(card, status, canAct, step) {
    const stage = model.WORKFLOW_STAGE_DOCUMENTS[status];
    if (!stage) return;

    const wrap = el("div", "card portal-card portal-card--nested portal-stage-upload");
    wrap.appendChild(el("div", "card__title", stage.title));
    if (stage.subtitle) wrap.appendChild(el("div", "muted", stage.subtitle));
    const responsible = model.unitRoleLabels(step.unit);
    if (responsible) {
      wrap.appendChild(el("div", "portal-stage__hint", "مسئول بارگذاری: " + responsible));
    }
    if (stage.autoAdvanceHint) wrap.appendChild(el("div", "muted", stage.autoAdvanceHint));

    if (!canAct) {
      const role = getSessionRole();
      const alert = el("div", "alert alert--warn portal-stage-upload__role-hint");
      alert.appendChild(
        el(
          "div",
          "",
          "بارگذاری فقط با نقش مسئول این مرحله ممکن است."
        )
      );
      alert.appendChild(el("div", "muted", "نقش شما: «" + (role || "—") + "» · لازم: " + responsible));
      const loginHint =
        step.unit === "applicant"
          ? "از تب Auth با متقاضی (Applicant) وارد شوید."
          : step.unit === "legal"
            ? "از تب Auth با LegalExpert (۲۰) یا LegalManager (۲۱) وارد شوید."
            : step.unit === "financial"
              ? "از تب Auth با FinancialExpert (۳۰) یا FinancialManager (۳۱) وارد شوید."
              : step.unit === "credit"
                ? "از تب Auth با CreditExpert (۵۰) یا CreditManager (۵۱) وارد شوید."
                : "با حساب دارای نقش مسئول این مرحله وارد شوید.";
      alert.appendChild(el("div", "muted", loginHint));
      wrap.appendChild(alert);
      card.appendChild(wrap);
      return;
    }

    wrap.appendChild(el("div", "muted", "پس از انتخاب فایل، بارگذاری (presign → S3 → confirm) خودکار انجام می‌شود."));

    stage.docs.forEach((doc) => {
      const existing = documentForType(doc.type);
      const versions = documentsForType(doc.type);
      const fileName = existing && pick(existing, "fileName", "FileName");
      appendFileUploadRow(wrap, {
        id: "g-wf-doc-" + status + "-" + doc.type,
        title: doc.label,
        hint: doc.hint || "",
        uploadType: doc.type,
        required: !!doc.required,
        uploadedLabel: existing ? "✓ بارگذاری شده" + (fileName ? ": " + fileName : "") : null,
      });
      if (versions.length > 1) {
        wrap.appendChild(
          el("div", "muted", "نسخه‌های قبلی این مدرک: " + versions.length)
        );
      }
    });

    card.appendChild(wrap);
  }

  async function uploadDocument(documentType, file) {
    const mimeType = file.type || "application/octet-stream";
    const presignRes = await apiCall({
      method: "POST",
      path: gPath("/" + state.caseId + "/documents/presign"),
      body: {
        documentType: Number(documentType),
        fileName: file.name,
        mimeType,
        fileSize: file.size,
      },
    });
    const presign = unwrap(presignRes.body) || {};
    const uploadUrl = presign.url || presign.Url || presign.presignedUrl || presign.PresignedUrl;
    const s3Key = presign.s3Key || presign.S3Key;
    if (!uploadUrl || !s3Key) throw new Error("پاسخ presign فاقد url یا s3Key است.");

    const putRes = await fetch(uploadUrl, {
      method: "PUT",
      body: file,
      headers: { "Content-Type": mimeType },
    });
    if (!putRes.ok) throw new Error("بارگذاری فایل در فضای ذخیره‌سازی ناموفق بود.");

    await apiCall({
      method: "POST",
      path: gPath("/" + state.caseId + "/documents/confirm?s3Key=" + encodeURIComponent(s3Key) + "&originalFileName=" + encodeURIComponent(file.name)),
      body: null,
      json: false,
    });
  }

  async function handleUpload(input) {
    if (state.busy) {
      setInfo("عملیات قبلی هنوز در حال انجام است. لطفاً چند ثانیه صبر کنید.");
      scrollToPortalMessage();
      return;
    }
    if (!state.caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const file = input.files && input.files[0];
    if (!file) return;
    const documentType = Number(input.dataset.uploadType);
    if (!Number.isFinite(documentType) || documentType <= 0) {
      throw new Error("نوع مدرک نامعتبر است.");
    }

    state.busy = true;
    setError("");
    setInfo("در حال بارگذاری مدرک…");
    const statusBefore = pickStatus(state.caseData);
    try {
      await uploadDocument(documentType, file);
      input.value = "";
      const status = input.closest(".portal-upload-row__control")?.querySelector(".portal-upload-row__status");
      if (status) {
        status.textContent = "✓ بارگذاری شد: " + file.name;
        status.classList.add("is-uploaded");
        status.closest(".portal-upload-row")?.classList.add("portal-upload-row--done");
      }
    } catch (e) {
      setError(e.message || String(e));
      scrollToPortalMessage();
      throw e;
    } finally {
      state.busy = false;
    }
    finishPortalMutation("مدرک با موفقیت بارگذاری شد.", statusBefore);
  }

  function renderStage() {
    const host = qs("#gPortalStages");
    if (!host) return;
    host.innerHTML = "";
    if (!state.caseData) return;

    const status = pickStatus(state.caseData);
    const step = stepForCase(status);
    const role = getSessionRole();

    const card = el("div", "portal-stage card portal-card");
    card.appendChild(el("div", "portal-stage__title", step.title + " (وضعیت " + status + ")"));
    card.appendChild(el("div", "portal-stage__meta muted", "نقش جاری: " + (role || "نامشخص")));

    const canAct = model.canActOnCase(role, step.unit);
    const reviewWithComments = status === 3 || status === 8 || status === 18 || status === 20;
    const approvalFormStage = status === 4;

    if (!canAct) {
      card.appendChild(
        el(
          "div",
          "portal-stage__hint",
          "با نقش فعلی («" + (role || "—") + "») اقدام این مرحله فعال نیست."
        )
      );
    }

    if ((status === 5 || status === 10) && canViewFundCreditCapacity()) {
      renderFundCreditCapacityBlock(card);
    }

    const workflowUploadStatus =
      status === 6 ||
      status === 7 ||
      status === 9 ||
      status === 11 ||
      (status === 20 && currentAmendmentType() !== 3);
    if (workflowUploadStatus) {
      renderWorkflowStageUploads(card, status, canAct, step);
    }

    if (shouldShowCaseDossier(status)) {
      renderCaseDossier(card);
    }

    if (!reviewWithComments && !approvalFormStage) {
      renderPrimaryActions(card);
    }

    if (status === 1 || status === 2) {
      if (status === 2) {
        renderApplicantRevisionInbox(card);
      }
      renderProfileSummary(card);
      renderApplicationForm(card);
      const saveBtn = el("button", "btn btn--primary", "ذخیره درخواست");
      saveBtn.type = "button";
      saveBtn.addEventListener("click", () =>
        void handleAction({ id: "save-app", method: "PUT", path: "/application" })
      );
      card.appendChild(saveBtn);
      renderUploads(card);
    } else if (status === 3) {
      renderCreditReviewStage(card, canAct);
    } else if (status === 8) {
      renderFinancialReviewStage(card, canAct);
    } else if (status === 4) {
      renderApprovalFormStage(card, canAct);
    } else if (status === 12) {
      const role = getSessionRole();
      const canEditAmendment = canAct && (role === "Applicant" || role === "Admin");
      if (canEditAmendment) {
        renderAmendmentCreationForm(card, status, true);
      }
    } else if (status === 14 || status === 16 || status === 17 || status === 18 || status === 19 || status === 20 || status === 22 || status === 23) {
      const role = getSessionRole();
      const canEditAmendment = canAct && (role === "Applicant" || role === "Admin");
      const canReviewAmendment =
        canAct &&
        (
          (status === 18 && (role === "CreditExpert" || role === "CreditManager" || role === "Admin")) ||
          (status === 19 && (role === "CEO" || role === "Admin")) ||
          (status === 20 && (role === "LegalExpert" || role === "LegalManager" || role === "Admin"))
        );
      renderAmendmentStage(card, status, (status === 18 || status === 19 || (status === 20 && currentAmendmentType() !== 3)) ? canReviewAmendment : canEditAmendment);
    }

    host.appendChild(card);
  }

  function actionsForStatus(status) {
    if (status >= 16 && status <= 23) return [];
    const map = {
      1: [{ id: "begin-de", label: "شروع ورود اطلاعات", method: "POST", path: "/application/begin" }],
      2: [
        { id: "submit-app", label: "ارسال به واحد اعتبارات", method: "POST", path: "/application/submit" },
        { id: "cancel-case", label: "لغو پرونده", method: "POST", path: "/cancel", needsReason: true, variant: "warn" },
      ],
      3: [
        { id: "credit-approve", label: "تأیید اعتبارات", method: "POST", path: "/credit/approve" },
        { id: "credit-revision", label: "درخواست اصلاح", method: "POST", path: "/credit/revision-request" },
      ],
      4: [
        { id: "approval-save", label: "ذخیره فرم تصویب", method: "PUT", path: "/approval-form" },
        { id: "approval-submit", label: "ارسال فرم تصویب", method: "POST", path: "/approval-form/submit" },
      ],
      5: [
        { id: "ceo-ok", label: "تأیید مدیرعامل", method: "POST", path: "/ceo/initial/approve" },
        { id: "ceo-no", label: "رد", method: "POST", path: "/ceo/initial/reject", needsMessage: true },
        { id: "ceo-cancel", label: "لغو پرونده", method: "POST", path: "/ceo/initial/cancel", needsMessage: true, variant: "warn" },
      ],
      6: [
        {
          id: "draft-ok",
          label: "تأیید بارگذاری پیش‌قرارداد (اگر خودکار جلو نرفت)",
          method: "POST",
          path: "/legal/draft-uploaded",
        },
      ],
      7: [
        { id: "signed-submit", label: "ارسال قرارداد امضاشده", method: "POST", path: "/signed-package/submit" },
        { id: "cancel-case", label: "لغو پرونده", method: "POST", path: "/cancel", needsReason: true, variant: "warn" },
      ],
      8: [
        { id: "fin-approve", label: "تأیید مدارک مالی", method: "POST", path: "/attachments/approve" },
        { id: "fin-revision", label: "درخواست اصلاح", method: "POST", path: "/attachments/revision-request" },
      ],
      9: [{ id: "final-ok", label: "تأیید قرارداد نهایی", method: "POST", path: "/legal/final-uploaded" }],
      10: [
        { id: "ceo-final-ok", label: "تأیید نهایی", method: "POST", path: "/ceo/final/approve" },
        { id: "ceo-final-no", label: "رد نهایی", method: "POST", path: "/ceo/final/reject", needsMessage: true },
        { id: "ceo-final-cancel", label: "لغو پرونده", method: "POST", path: "/ceo/final/cancel", needsMessage: true, variant: "warn" },
      ],
      11: [{ id: "issue-ok", label: "تأیید صدور", method: "POST", path: "/issuance/uploaded" }],
    };
    return map[status] || [];
  }

  async function handleAction(action) {
    if (state.busy) {
      setInfo("عملیات قبلی هنوز در حال انجام است. لطفاً چند ثانیه صبر کنید.");
      scrollToPortalMessage();
      return;
    }
    if (!state.caseId) {
      setError("شناسه پرونده تنظیم نشده است.");
      scrollToPortalMessage();
      return;
    }
    if (!state.panel.getActiveSession()?.accessToken) {
      setError("ابتدا وارد شوید.");
      scrollToPortalMessage();
      return;
    }

    state.busy = true;
    setError("");
    setInfo("در حال انجام عملیات…");
    const statusBefore = pickStatus(state.caseData);
    let successMessage = "";
    try {
      let body = null;
      if (action.needsMessage) {
        const msg = prompt("پیام / توضیح:");
        if (!msg) {
          cancelPortalAction();
          return;
        }
        body = { message: msg, comment: msg };
      } else if (action.needsReason) {
        const reason = prompt("دلیل لغو پرونده:");
        if (!reason) {
          cancelPortalAction();
          return;
        }
        if (!confirm("آیا از لغو این پرونده اطمینان دارید؟")) {
          cancelPortalAction();
          return;
        }
        body = { reason };
      } else if (action.id === "credit-approve") {
        body = { internalComment: readValue("gCreditInternalComment") || null };
      } else if (action.id === "credit-revision") {
        const message = readValue("gCreditRevision");
        if (!message) throw new Error("پیام اصلاح برای متقاضی الزامی است.");
        body = { message };
      } else if (action.id === "fin-approve") {
        body = { internalComment: readValue("gFinInternalComment") || null };
      } else if (action.id === "fin-revision") {
        const message = readValue("gFinRevision");
        if (!message) throw new Error("پیام اصلاح برای متقاضی الزامی است.");
        body = { message };
      } else if (action.id === "save-amendment") {
        body = readAmendmentForm();
        if (!body.amendmentType) throw new Error("نوع اصلاحیه الزامی است.");
        if (body.amendmentType === 3) {
          body = readCancellationForm();
          if (!body.reason) throw new Error("علت ابطال الزامی است.");
          action.path = "/amendment/cancellation/create";
        } else {
          if (body.amendmentType === 1 && !body.newValidityTo) throw new Error("تاریخ اعتبار جدید الزامی است.");
          if (body.amendmentType === 2 && !(body.newGuaranteeAmount > 0)) throw new Error("مبلغ جدید باید بزرگ‌تر از صفر باشد.");
          if (!body.reason) throw new Error("علت اصلاحیه الزامی است.");
        }
      } else if (action.id === "submit-amendment") {
        const payload = readAmendmentForm();
        if (!payload.amendmentType) throw new Error("نوع اصلاحیه الزامی است.");
        if (payload.amendmentType === 3) {
          const cancellationPayload = readCancellationForm();
          if (!cancellationPayload.reason) throw new Error("علت ابطال الزامی است.");
          if (!cancellationDocumentsComplete()) throw new Error("مدارک الزامی ابطال ناقص است.");
          await apiCall({
            method: "POST",
            path: gPath("/" + state.caseId + "/amendment/cancellation/create"),
            body: cancellationPayload,
          });
          action.path = "/amendment/cancellation/submit";
          action.submitCancellation = true;
          body = readCancellationSubmitForm();
        } else {
          if (payload.amendmentType === 1 && !payload.newValidityTo) throw new Error("تاریخ اعتبار جدید الزامی است.");
          if (payload.amendmentType === 2 && !(payload.newGuaranteeAmount > 0)) throw new Error("مبلغ جدید باید بزرگ‌تر از صفر باشد.");
          if (!payload.reason) throw new Error("علت اصلاحیه الزامی است.");
          await apiCall({
            method: "POST",
            path: gPath("/" + state.caseId + "/amendment/create"),
            body: payload,
          });
          body = {};
        }
      } else if (action.id === "ceo-amendment-reject") {
        const message = readValue("gAmendmentApproveComment");
        if (!message) throw new Error("علت رد اصلاحیه الزامی است.");
        body = { message };
      } else if (action.id === "ceo-amendment-approve") {
        body = { comment: readValue("gAmendmentApproveComment") || null };
      } else if (action.id === "approve-amendment") {
        if (pickStatus(state.caseData) === 20 && currentAmendmentType() !== 3 && !documentForType(32)) {
          throw new Error("قرارداد اصلاحیه بارگذاری نشده است.");
        }
        body = {
          comment: readValue("gAmendmentApproveComment") || null,
          internalComment: readValue("gAmendmentInternalComment") || null,
        };
      } else if (action.id === "approve-cancellation") {
        body = {
          comment: readValue("gAmendmentApproveComment") || null,
          internalComment: readValue("gAmendmentInternalComment") || null,
        };
      } else if (action.id === "reject-amendment") {
        const message = readValue("gAmendmentRejectReason");
        if (!message) throw new Error("علت رد اصلاحیه الزامی است.");
        body = { message };
      } else if (action.id === "amendment-revision") {
        const message = readValue("gAmendmentRejectReason");
        if (!message) throw new Error("توضیح درخواست اصلاح الزامی است.");
        body = { message };
      } else if (action.id === "approval-save" || action.id === "approval-submit") {
        body = readApprovalForm();
      }
      if (action.id === "approval-submit") {
        if (!confirm("آیا از ارسال فرم تصویب به مدیرعامل اطمینان دارید؟")) {
          cancelPortalAction();
          return;
        }
        await apiCall({
          method: "PUT",
          path: gPath("/" + state.caseId + "/approval-form"),
          body: readApprovalForm(),
        });
        body = {};
      }
      if (action.id === "save-app") {
        body = readApplicationForm();
        if (
          body.priceAdjustmentRatePercent != null &&
          (body.priceAdjustmentRatePercent > 999.99 || body.priceAdjustmentRatePercent < 0)
        ) {
          throw new Error(
            "نرخ تعدیل باید بین ۰ تا ۹۹۹٫۹۹ (درصد) باشد — مبلغ ریالی را در فیلد مبلغ ضمانت‌نامه وارد کنید."
          );
        }
      }
      if (action.id === "submit-app") {
        if (!confirm("آیا از ارسال پرونده به واحد اعتبارات اطمینان دارید؟")) {
          cancelPortalAction();
          return;
        }
        const saveBody = readApplicationForm();
        if (
          saveBody.priceAdjustmentRatePercent != null &&
          (saveBody.priceAdjustmentRatePercent > 999.99 || saveBody.priceAdjustmentRatePercent < 0)
        ) {
          throw new Error(
            "نرخ تعدیل باید بین ۰ تا ۹۹۹٫۹۹ (درصد) باشد — مبلغ ریالی را در فیلد مبلغ ضمانت‌نامه وارد کنید."
          );
        }
        await apiCall({
          method: "PUT",
          path: gPath("/" + state.caseId + "/application"),
          body: saveBody,
        });
        const caseRes = await apiCall({
          method: "GET",
          path: gPath("/" + state.caseId),
        });
        state.caseData = unwrap(caseRes.body);
        const docsRes = await apiCall({
          method: "GET",
          path: gPath("/" + state.caseId + "/documents"),
        });
        state.documents = unwrap(docsRes.body) || [];
        const gt = savedGuaranteeType() || formGuaranteeType(document);
        if (!requiredDocumentsComplete(gt, document)) {
          throw new Error(formatMissingDocumentsError(missingRequiredDocuments(gt, document), gt));
        }
      }

      const res = await apiCall({
        method: action.method,
        path: gPath("/" + state.caseId + action.path),
        body: action.id === "submit-app" || action.id === "approval-submit" || (action.id === "submit-amendment" && !action.submitCancellation) ? {} : body,
      });
      void res;
      successMessage = resolveActionSuccessMessage(action.id) || "عملیات با موفقیت انجام شد.";
    } catch (e) {
      setError(e.message || String(e));
      scrollToPortalMessage();
    } finally {
      state.busy = false;
    }
    if (successMessage) {
      finishPortalMutation(successMessage, statusBefore);
    }
  }

  function render() {
    try {
      renderSummary();
      renderActionBar();
      renderStepper();
      renderStage();
      renderActionHint();
    } catch (err) {
      console.error("[guarantee-portal] render failed", err);
      setError("خطا در نمایش فرم: " + (err.message || String(err)));
      scrollToPortalMessage();
    }
  }

  function wire() {
    qs("#gPortalRefreshCase")?.addEventListener("click", async () => {
      try {
        setError("");
        await refreshCase();
      } catch (e) {
        setError(e.message || String(e));
      }
    });

    qs("#gCaseTitleSave")?.addEventListener("click", async () => {
      try {
        if (!state.caseId) return;
        setError("");
        const title = qs("#gCaseTitleInput")?.value?.trim() || null;
        await state.panel.apiRequest({
          method: "PUT",
          path: gPath("/" + state.caseId + "/title"),
          body: { title },
        });
        await refreshCase();
        setInfo("عنوان ذخیره شد.");
      } catch (e) {
        setError(e.message || String(e));
      }
    });

    qs("#gApplicantType")?.addEventListener("change", syncCompanyRow);
    syncCompanyRow();

    qs("#gLoadCompanies")?.addEventListener("click", async () => {
      try {
        setError("");
        await loadMyCompanies();
      } catch (e) {
        setError(e.message || String(e));
      }
    });

    qs("#gCreateCase")?.addEventListener("click", async () => {
      try {
        setError("");
        await createCase();
      } catch (e) {
        setError(e.message || String(e));
      }
    });

    qs("#gLoadCase")?.addEventListener("click", async () => {
      try {
        setError("");
        state.caseId = qs("#gCaseIdInput")?.value?.trim() || "";
        state.panel.setGuaranteeCaseId(state.caseId);
        await refreshCase();
      } catch (e) {
        setError(e.message || String(e));
      }
    });

    document.addEventListener("testpanel:session-changed", () => {
      if (state.caseId) refreshCase();
      else render();
    });

    document.addEventListener("testpanel:case-changed", (ev) => {
      if (ev.detail?.module !== "guarantee") return;
      state.caseId = ev.detail?.caseId || state.panel.getGuaranteeCaseId() || "";
      if (state.caseId) refreshCase();
    });

    document.addEventListener("testpanel:open-comment-step", (ev) => {
      if (ev.detail?.module && ev.detail.module !== "guarantee") return;
      const target =
        (ev.detail?.commentId &&
          document.querySelector(`#guaranteePortalRoot [data-comment-id="${ev.detail.commentId}"]`)) ||
        (ev.detail?.phase != null &&
          document.querySelector(`#guaranteePortalRoot [data-comment-phase="${ev.detail.phase}"]`));
      target?.scrollIntoView({ behavior: "smooth", block: "center" });
      target?.classList.add("is-highlight");
    });

    qs("#gPortalStages")?.addEventListener("change", (ev) => {
      if (ev.target && ev.target.id === "gGuaranteeType") render();
    });
  }

  window.initGuaranteePortal = function initGuaranteePortal(panel) {
    state.panel = panel;
    state.caseId = panel.getGuaranteeCaseId() || "";
    if (qs("#gCaseIdInput")) qs("#gCaseIdInput").value = state.caseId;
    wire();
    if (state.caseId) refreshCase();
    else render();
  };
})();
