/* global LoanWorkflowModel */
(function () {
  const model = window.LoanWorkflowModel;
  const state = { panel: null, caseId: "", caseData: null, documents: [], installments: [], comments: [], history: [], payments: [], busy: false };

  function lPath(suffix) {
    return state.panel.loanCasesBasePath() + suffix;
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

  const PRIVILEGED_VIEW_ROLES = ["Admin", "CEO", "TechnicalExpert"];

  function canViewFullCaseDossier() {
    return PRIVILEGED_VIEW_ROLES.includes(getSessionRole());
  }

  function el(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }

  function qs(sel) {
    return document.querySelector(sel);
  }

  function setError(msg) {
    const box = qs("#lPortalError");
    if (!msg) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.textContent = msg;
    box.classList.remove("hidden");
  }

  function setInfo(msg) {
    const box = qs("#lPortalInfo");
    if (!msg) {
      box.classList.add("hidden");
      return;
    }
    box.textContent = msg;
    box.classList.remove("hidden");
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
      module: "loan",
      host: "lStageRollback",
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

  function canViewFundCreditCapacity() {
    const role = getSessionRole();
    return window.FundCreditCapacityUi && window.FundCreditCapacityUi.canViewFundCreditCapacity(role);
  }

  function renderFundCreditCapacityBlock() {
    const card = el("div", "card portal-card fund-capacity-card-wrap");
    if (window.FundCreditCapacityUi) {
      window.FundCreditCapacityUi.renderFundCreditCapacityWidget(card, state.caseData, getSessionRole());
    }
    return card;
  }

  function captureApplicationDraft() {
    const amount = qs("#lRequestedAmount");
    if (!amount) return null;
    return {
      requestedAmount: amount.value,
      facilitySubject: qs("#lFacilitySubject")?.value ?? "",
      offeredGuarantees: qs("#lOfferedGuarantees")?.value ?? "",
    };
  }

  function captureApprovalDraft() {
    const amount = qs("#lApprovedAmount");
    if (!amount) return null;
    return {
      approvedAmount: amount.value,
      repaymentMonths: qs("#lRepaymentMonths")?.value ?? "",
      annualProfitRatePercent: qs("#lAnnualProfitRate")?.value ?? "",
      gracePeriodMonths: qs("#lGracePeriodMonths")?.value ?? "",
      facilityType: qs("#lFacilityType")?.value ?? "",
    };
  }

  function readApprovalForm() {
    const graceRaw = qs("#lGracePeriodMonths")?.value;
    return {
      approvedAmount: qs("#lApprovedAmount")?.value,
      repaymentMonths: qs("#lRepaymentMonths")?.value,
      annualProfitRatePercent: qs("#lAnnualProfitRate")?.value,
      gracePeriodMonths: graceRaw === "" || graceRaw == null ? null : graceRaw,
      facilityType: Number(qs("#lFacilityType")?.value) || 3,
    };
  }

  function computeExpectedTotalProfit(approvedAmount, ratePercent, repaymentMonths) {
    const approved = Number(approvedAmount);
    const rate = Number(ratePercent);
    const months = Number(repaymentMonths);
    if (!Number.isFinite(approved) || approved <= 0) return null;
    if (!Number.isFinite(rate) || rate < 0) return null;
    if (!Number.isFinite(months) || months <= 0) return null;
    return Math.round(approved * (rate / 100) * (months / 12));
  }

  function updateExpectedProfitPreview(previewEl, inputs) {
    const preview = previewEl || qs("#lExpectedProfitPreview");
    if (!preview) return;
    const approved = inputs?.amount?.value ?? qs("#lApprovedAmount")?.value;
    const months = inputs?.months?.value ?? qs("#lRepaymentMonths")?.value;
    const rate = inputs?.rate?.value ?? qs("#lAnnualProfitRate")?.value;
    const profit = computeExpectedTotalProfit(approved, rate, months);
    preview.textContent = profit != null ? "سود کل برآوردی: " + formatRial(profit) : "سود کل برآوردی: —";
  }

  function buildApprovalDetailBody(form) {
    const app = getApplication();
    const approved = Number(form.approvedAmount) || null;
    const repaymentMonths = Number(form.repaymentMonths) || null;
    const rate = Number(form.annualProfitRatePercent);
    const grace = form.gracePeriodMonths == null || form.gracePeriodMonths === "" ? null : Number(form.gracePeriodMonths);
    const contractSubject =
      pick(app, "facilitySubject", "FacilitySubject") || pickDetailField("contractSubject", "ContractSubject");
    const collateral =
      pick(app, "offeredGuarantees", "OfferedGuarantees") || pickDetailField("collateralDescription", "CollateralDescription");
    const expectedProfit = computeExpectedTotalProfit(approved, rate, repaymentMonths);
    const repaymentCheck =
      pickDetailField("repaymentCheckAmount", "RepaymentCheckAmount") ??
      (approved && expectedProfit ? approved + expectedProfit : approved || null);
    return {
      debtToAssetRatio: pickDetailField("debtToAssetRatio", "DebtToAssetRatio"),
      currentRatio: pickDetailField("currentRatio", "CurrentRatio"),
      profitabilityRatioPercent: pickDetailField("profitabilityRatioPercent", "ProfitabilityRatioPercent"),
      creditLimitWithCheck: pickDetailField("creditLimitWithCheck", "CreditLimitWithCheck"),
      isCreditLineActive: pick(getApprovalDetail(), "isCreditLineActive", "IsCreditLineActive") ?? null,
      remainingCreditAfterGrant: pickDetailField("remainingCreditAfterGrant", "RemainingCreditAfterGrant"),
      approvedAmount: approved,
      facilityType: form.facilityType ?? 3,
      contractSubject: contractSubject || null,
      brokerageAndRelatedContract: pickDetailField("brokerageAndRelatedContract", "BrokerageAndRelatedContract"),
      approvedAmountInWords: pickDetailField("approvedAmountInWords", "ApprovedAmountInWords"),
      repaymentMonths,
      gracePeriodMonths: Number.isFinite(grace) ? grace : null,
      annualProfitRatePercent: Number.isFinite(rate) ? rate : null,
      dailyPenaltyRatePercent: pickDetailField("dailyPenaltyRatePercent", "DailyPenaltyRatePercent"),
      expectedTotalProfit: expectedProfit,
      collateralDescription: collateral || null,
      guarantorsDescription: pickDetailField("guarantorsDescription", "GuarantorsDescription"),
      otherNotes: pickDetailField("otherNotes", "OtherNotes"),
      repaymentCheckAmount: repaymentCheck,
    };
  }

  async function saveApprovalDetail(formValues) {
    const form = formValues || readApprovalForm();
    if (!Number(form.approvedAmount) || Number(form.approvedAmount) <= 0) {
      throw new Error("مبلغ تأییدشده را وارد کنید.");
    }
    if (!Number(form.repaymentMonths) || Number(form.repaymentMonths) <= 0) {
      throw new Error("مدت بازپرداخت (ماه) را وارد کنید.");
    }
    if (!Number.isFinite(Number(form.annualProfitRatePercent)) || Number(form.annualProfitRatePercent) < 0) {
      throw new Error("نرخ سود سالانه را وارد کنید.");
    }
    await state.panel.apiRequest({
      method: "PUT",
      path: lPath("/" + state.caseId + "/approval-detail"),
      body: buildApprovalDetailBody(form),
    });
  }

  async function reloadCaseData(applicationDraft, approvalDraft) {
    const [caseRes] = await Promise.all([
      state.panel.apiRequest({ method: "GET", path: lPath("/" + state.caseId) }),
      loadDocuments(),
      loadInstallments(),
      loadComments(),
      loadHistory(),
      loadPayments(),
    ]);
    state.caseData = unwrap(caseRes.body);
    const caseInstallments = pick(state.caseData, "installments", "Installments");
    if (Array.isArray(caseInstallments) && caseInstallments.length) {
      state.installments = caseInstallments;
    }
    const casePayments = pick(state.caseData, "payments", "Payments");
    if (Array.isArray(casePayments) && casePayments.length) {
      state.payments = casePayments;
    }
    renderCase(applicationDraft, approvalDraft);
  }

  async function runRefreshCase(previousStatus) {
    if (!state.caseId) return;
    const applicationDraft = captureApplicationDraft();
    const approvalDraft = captureApprovalDraft();
    const refreshOnce = function () {
      return reloadCaseData(applicationDraft, approvalDraft);
    };
    if (previousStatus != null && window.PortalCaseRefresh) {
      await window.PortalCaseRefresh.refreshUntilChanged(
        refreshOnce,
        function () {
          return pickStatus(state.caseData);
        },
        previousStatus
      );
    } else {
      await refreshOnce();
    }
    await refreshStageRollback();
  }

  const refreshCase = window.PortalCaseRefresh
    ? window.PortalCaseRefresh.createCoalescedRefresh(runRefreshCase)
    : runRefreshCase;

  async function loadDocuments() {
    if (!state.caseId) return;
    const res = await state.panel.apiRequest({ method: "GET", path: lPath("/" + state.caseId + "/documents") });
    state.documents = unwrap(res.body) || [];
  }

  async function loadInstallments() {
    if (!state.caseId) return;
    try {
      const res = await state.panel.apiRequest({ method: "GET", path: lPath("/" + state.caseId + "/installments") });
      state.installments = unwrap(res.body) || [];
    } catch (_) {
      state.installments = [];
    }
  }

  async function loadPayments() {
    if (!state.caseId) return;
    try {
      const res = await state.panel.apiRequest({ method: "GET", path: lPath("/" + state.caseId + "/payments") });
      state.payments = unwrap(res.body) || [];
    } catch (_) {
      state.payments = [];
    }
  }

  async function loadComments() {
    if (!state.caseId) return;
    try {
      const includeInternal = isInternalUser() ? "true" : "false";
      const res = await state.panel.apiRequest({
        method: "GET",
        path: lPath("/" + state.caseId + "/comments?includeInternal=" + includeInternal),
      });
      state.comments = unwrap(res.body) || [];
    } catch (_) {
      state.comments = [];
    }
  }

  async function loadHistory() {
    if (!state.caseId) return;
    try {
      const res = await state.panel.apiRequest({ method: "GET", path: lPath("/" + state.caseId + "/history") });
      state.history = unwrap(res.body) || [];
    } catch (_) {
      state.history = [];
    }
  }

  function isInternalUser() {
    const role = getSessionRole();
    if (window.WorkflowModel && typeof window.WorkflowModel.isInternalRole === "function") {
      return window.WorkflowModel.isInternalRole(role);
    }
    return role && role !== "Applicant";
  }

  function shouldShowCompletedCaseDossier(status) {
    const value = model.coerceStatus ? model.coerceStatus(status) : Number(status) || 0;
    return value === 17 || value === 18;
  }

  function shouldShowCaseDossier(status) {
    const value = model.coerceStatus ? model.coerceStatus(status) : Number(status) || 0;
    if (shouldShowCompletedCaseDossier(value)) return true;
    if (canViewFullCaseDossier() && value >= 1) return true;
    return isInternalUser() && [3, 5, 7, 9, 11, 13, 14, 15, 16].includes(value);
  }

  function formatCaseDate(iso) {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleString("fa-IR");
    } catch {
      return String(iso);
    }
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

  function formatUploadedAt(doc) {
    const raw = pick(doc, "uploadedAt", "UploadedAt");
    if (!raw) return "";
    try {
      return new Date(raw).toLocaleString("fa-IR");
    } catch {
      return String(raw);
    }
  }

  async function downloadLoanDocument(documentId) {
    if (!state.caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const session = state.panel.getActiveSession();
    const headers = {};
    if (session?.accessToken) headers.Authorization = "Bearer " + session.accessToken;
    const url = state.panel.makeUrl(
      lPath("/" + state.caseId + "/documents/" + encodeURIComponent(documentId) + "/download")
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

  function renderAttachmentsDownloadSection(parent) {
    UIComponents.renderAttachments(parent, state.documents, {
      module: "loan",
      onDownload: (docId) => {
        void downloadLoanDocument(docId).catch((e) => setError(e.message || String(e)));
      },
    });
  }

  function renderApplicationReadOnly(parent) {
    const app = getApplication();
    if (!app || !Object.keys(app).length) {
      parent.appendChild(el("div", "muted", "فرم درخواست هنوز ثبت نشده است."));
      return;
    }
    renderReadOnlyBlock(parent, "درخواست متقاضی (ثبت‌شده)", [
      ["مبلغ درخواستی (ریال)", formatRial(pick(app, "requestedAmount", "RequestedAmount"))],
      ["موضوع تسهیلات", pick(app, "facilitySubject", "FacilitySubject")],
      ["تضامین و وثایق", pick(app, "offeredGuarantees", "OfferedGuarantees")],
      ["دسته‌بندی متقاضی", labelFromOptions(pick(app, "applicantCategory", "ApplicantCategory"), model.APPLICANT_CATEGORIES)],
    ]);
  }

  function renderApprovalFormReadOnly(parent) {
    const detail = getApprovalDetail();
    if (!detail || !Object.keys(detail).length) {
      parent.appendChild(el("div", "muted", "فرم تصویب هنوز ثبت نشده است."));
      return;
    }

    const profit = getExpectedTotalProfit();
    const approved = getApprovedAmount();
    const repaymentCheck = getRepaymentCheckAmount();
    const contractSubject = resolveDetailOrApp("contractSubject", "ContractSubject", "facilitySubject", "FacilitySubject");
    const collateral = resolveDetailOrApp("collateralDescription", "CollateralDescription", "offeredGuarantees", "OfferedGuarantees");
    const facilityType = pickDetailField("facilityType", "FacilityType");
    const creditActive = pick(detail, "isCreditLineActive", "IsCreditLineActive");

    const rows = [];
    const pushText = (label, value) => {
      const text = displayDetailText(value);
      if (text !== "—") rows.push([label, text]);
    };
    const pushRial = (label, value) => {
      if (value == null || value === "" || Number(value) <= 0) return;
      rows.push([label, formatRialOptional(value)]);
    };
    const pushNumber = (label, value) => {
      if (value == null || value === "") return;
      rows.push([label, String(value)]);
    };

    pushRial("مبلغ تأییدشده (ریال)", approved);
    if (facilityType != null) rows.push(["نوع تسهیلات", labelFromOptions(facilityType, model.FACILITY_TYPES)]);
    pushText("موضوع قرارداد", contractSubject);
    pushText("کارگزاری / قرارداد مرتبط", pickDetailField("brokerageAndRelatedContract", "BrokerageAndRelatedContract"));
    pushNumber("مدت بازپرداخت (ماه)", pickDetailField("repaymentMonths", "RepaymentMonths"));
    pushNumber("دوره تنفس (ماه)", pickDetailField("gracePeriodMonths", "GracePeriodMonths"));
    pushNumber("نرخ سود سالانه (٪)", pickDetailField("annualProfitRatePercent", "AnnualProfitRatePercent"));
    pushNumber("نرخ جریمه روزانه (٪)", pickDetailField("dailyPenaltyRatePercent", "DailyPenaltyRatePercent"));
    pushRial("سود کل برآوردی (ریال)", profit > 0 ? profit : null);
    pushRial("مبلغ چک بازپرداخت", repaymentCheck);
    pushNumber("نسبت بدهی به دارایی", pickDetailField("debtToAssetRatio", "DebtToAssetRatio"));
    pushNumber("نسبت جاری", pickDetailField("currentRatio", "CurrentRatio"));
    pushNumber("نسبت سودآوری (٪)", pickDetailField("profitabilityRatioPercent", "ProfitabilityRatioPercent"));
    pushRial("سقف اعتبار", pickDetailField("creditLimitWithCheck", "CreditLimitWithCheck"));
    if (creditActive === true || creditActive === false) {
      rows.push(["خط اعتباری فعال", creditActive ? "بله" : "خیر"]);
    }
    pushRial("اعتبار باقی‌مانده پس از اعطا", pickDetailField("remainingCreditAfterGrant", "RemainingCreditAfterGrant"));
    pushText("وثایق / تضمین", collateral);
    pushText("ضامنین", pickDetailField("guarantorsDescription", "GuarantorsDescription"));
    pushText("سایر توضیحات", pickDetailField("otherNotes", "OtherNotes"));

    if (!rows.length) {
      parent.appendChild(el("div", "muted", "فرم تصویب هنوز ثبت نشده است."));
      return;
    }
    renderReadOnlyBlock(parent, null, rows);
  }

  function renderCompanyProfile(parent) {
    const company = pick(state.caseData, "company", "Company");
    if (!company) return;
    renderReadOnlyBlock(parent, "اطلاعات شرکت", [
      ["نام شرکت", pick(company, "name", "Name")],
      ["شناسه ملی", pick(company, "nationalId", "NationalId")],
      ["تلفن", pick(company, "phoneNumber", "PhoneNumber")],
      ["آدرس", pick(company, "address", "Address")],
    ]);
  }

  function renderAllDocumentsArchive(parent) {
    if (!state.documents.length) {
      parent.appendChild(el("div", "muted", "هنوز مدرکی بارگذاری نشده است."));
      return;
    }
    const byType = new Map();
    state.documents.forEach((doc) => {
      const type = Number(pick(doc, "documentType", "DocumentType"));
      if (!byType.has(type)) byType.set(type, []);
      byType.get(type).push(doc);
    });
    const wrap = el("div", "portal-doc-archive");
    Array.from(byType.keys())
      .sort((a, b) => a - b)
      .forEach((type) => {
        const versions = byType
          .get(type)
          .slice()
          .sort((a, b) => Number(pick(b, "version", "Version") ?? 0) - Number(pick(a, "version", "Version") ?? 0));
        const block = el("div", "portal-doc-archive__type");
        const typeLabel =
          (model.DOCUMENT_LABELS && model.DOCUMENT_LABELS[type]) ||
          (model.DATA_ENTRY_DOCUMENTS || []).find((d) => d.type === type)?.label ||
          "نوع " + type;
        block.appendChild(el("div", "portal-doc-archive__type-title", typeLabel));
        versions.forEach((doc) => {
          const id = pick(doc, "id", "Id");
          const ver = pick(doc, "version", "Version") ?? 1;
          const name = pick(doc, "fileName", "FileName") || "فایل";
          const row = el("div", "portal-doc-archive__row");
          const meta = el("div", "portal-doc-archive__meta");
          meta.textContent = "نسخه " + ver + " — " + name + (formatUploadedAt(doc) ? " · " + formatUploadedAt(doc) : "");
          row.appendChild(meta);
          if (id) {
            const btn = el("button", "btn btn--small", "دانلود");
            btn.type = "button";
            btn.addEventListener("click", () => {
              void downloadLoanDocument(id).catch((e) => setError(e.message || String(e)));
            });
            row.appendChild(btn);
          }
          block.appendChild(row);
        });
        wrap.appendChild(block);
      });
    parent.appendChild(wrap);
  }

  function renderPaymentsReadOnly(parent) {
    const payments = state.payments || pick(state.caseData, "payments", "Payments") || [];
    if (!payments.length) {
      parent.appendChild(el("div", "muted", "پرداختی ثبت نشده است."));
      return;
    }
    const list = el("div", "portal-payments-list");
    payments.forEach((payment) => {
      const amount = Number(pick(payment, "amount", "Amount")) || 0;
      const date = pick(payment, "paymentDate", "PaymentDate") || "—";
      const txn = pick(payment, "transactionNumber", "TransactionNumber") || "—";
      const stage = pick(payment, "stageNumber", "StageNumber") || "—";
      const creator = pick(payment, "createdByFullName", "CreatedByFullName") || pick(payment, "createdByUserId", "CreatedByUserId") || "";
      list.appendChild(
        el(
          "div",
          "portal-payments-list__item",
          formatRial(amount) +
            " · " +
            date +
            " · " +
            txn +
            " · مرحله " +
            stage +
            (creator ? " · " + creator : "") +
            (pick(payment, "receiptS3Key", "ReceiptS3Key") ? " · دارای رسید" : "")
        )
      );
    });
    parent.appendChild(list);
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
          module: "loan",
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

  function renderInstallmentsReadOnly(parent) {
    const card = el("div", "card portal-card portal-card--nested");
    card.appendChild(el("div", "card__title", "جدول اقساط (ثبت‌شده)"));
    renderLoanAmountSummary(card);
    appendInstallmentTable(card, false);
    parent.appendChild(card);
  }

  function renderCaseDossier() {
    const details = document.createElement("details");
    details.className = "portal-dossier card portal-card";
    details.open = true;

    const summary = document.createElement("summary");
    summary.className = "portal-dossier__summary card__title";
    summary.textContent = "پرونده کامل — اطلاعات ثبت‌شده، مدارک، پرداخت‌ها و اقساط";
    details.appendChild(summary);

    const body = el("div", "portal-dossier__body");
    const status = pickStatus(state.caseData);
    body.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        shouldShowCompletedCaseDossier(status)
          ? "پرونده تکمیل شده است. تمام اطلاعات ثبت‌شده در زیر قابل مشاهده است."
          : "خلاصه درخواست، فرم تصویب، همه فایل‌ها و جدول اقساط برای بررسی این مرحله."
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

    renderCompanyProfile(body);

    const appWrap = el("div", "card portal-card portal-card--nested");
    renderApplicationReadOnly(appWrap);
    body.appendChild(appWrap);

    const afWrap = el("div", "card portal-card portal-card--nested");
    afWrap.appendChild(el("div", "card__title", "فرم تصویب (ثبت‌شده)"));
    renderApprovalFormReadOnly(afWrap);
    body.appendChild(afWrap);

    const docsWrap = el("div", "card portal-card portal-card--nested");
    docsWrap.appendChild(el("div", "card__title", "همه مدارک و پیوست‌ها"));
    renderAllDocumentsArchive(docsWrap);
    body.appendChild(docsWrap);

    renderInstallmentsReadOnly(body);

    const paymentsWrap = el("div", "card portal-card portal-card--nested");
    paymentsWrap.appendChild(el("div", "card__title", "پرداخت‌های ثبت‌شده"));
    renderPaymentsReadOnly(paymentsWrap);
    body.appendChild(paymentsWrap);

    const commentsWrap = el("div", "card portal-card portal-card--nested");
    commentsWrap.appendChild(el("div", "card__title", "تاریخچه نظرات و درخواست‌های اصلاح"));
    renderDossierComments(commentsWrap);
    body.appendChild(commentsWrap);

    const historyWrap = el("div", "card portal-card portal-card--nested");
    historyWrap.appendChild(el("div", "card__title", "تاریخچه گردش کار"));
    renderFullWorkflowHistory(historyWrap);
    body.appendChild(historyWrap);

    details.appendChild(body);
    return details;
  }

  async function uploadDocument(docType, file) {
    const mimeType = file.type || "application/octet-stream";
    const presignRes = await state.panel.apiRequest({
      method: "POST",
      path: lPath("/" + state.caseId + "/documents/presign"),
      body: {
        documentType: Number(docType),
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
      headers: { "Content-Type": mimeType },
      body: file,
    });
    if (!putRes.ok) throw new Error("بارگذاری فایل با کد " + putRes.status + " ناموفق بود.");

   await state.panel.apiRequest({
     method: "POST",
      path: lPath("/" + state.caseId + "/documents/confirm?s3Key=" + encodeURIComponent(s3Key) + "&originalFileName=" + encodeURIComponent(file.name)),
      body: null,
      json: false,
    });
    await loadDocuments();
    setInfo("فایل بارگذاری شد.");
    return s3Key;
  }

  function renderCase(applicationDraft, approvalDraft) {
    const status = pickStatus(state.caseData);
    const step = model.stepForStatus(status);
    const role = getSessionRole();

    qs("#lPortalEmpty").classList.add("hidden");
    qs("#lPortalHeader").classList.remove("hidden");
    qs("#lCaseNumber").textContent = pick(state.caseData, "caseNumber", "CaseNumber") || "—";
    const title = pick(state.caseData, "title", "Title") || "—";
    if (qs("#lCaseTitle")) qs("#lCaseTitle").textContent = title;
    const titleInput = qs("#lCaseTitleInput");
    if (titleInput && document.activeElement !== titleInput) {
      titleInput.value = pick(state.caseData, "title", "Title") || "";
    }
    const lCaseIdEl = qs("#lCaseId");
    if (lCaseIdEl) lCaseIdEl.textContent = state.caseId;
    qs("#lCaseStatus").textContent = step.title + " (" + status + ")";
    qs("#lCaseRole").textContent = role || "—";

    const company = pick(state.caseData, "company", "Company");
    qs("#lCaseCompany").textContent = company ? pick(company, "name", "Name") || "—" : "—";

    const hint = qs("#lPortalActionHint");
    if (model.canActOnCase(status, role)) {
      hint.textContent = "اقدام شما لازم است";
      hint.classList.remove("hidden");
    } else {
      hint.classList.add("hidden");
    }

    renderStepper();
    renderStages(status, role, applicationDraft, approvalDraft);
  }

  function renderStepper() {
    const root = qs("#lPortalStepper");
    if (!root) return;
    root.innerHTML = "";
    if (!state.caseData) return;

    const current = pickStatus(state.caseData);
    const track = el("div", "portal-stepper__track");
    const currentIndex = model.getStepOrderIndex(current);

    model.getStepperSteps().forEach((step, index) => {
      const item = el("div", "portal-stepper__item");
      if (currentIndex >= 0) {
        if (index < currentIndex) item.classList.add("is-done");
        if (index === currentIndex) item.classList.add("is-current");
        if (index > currentIndex) item.classList.add("is-upcoming");
      } else if (step.id < current) {
        item.classList.add("is-done");
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
    });

    root.appendChild(track);
  }

  function renderStages(status, role, applicationDraft, approvalDraft) {
    const host = qs("#lPortalStages");
    host.innerHTML = "";

    if (shouldShowCaseDossier(status)) {
      host.appendChild(renderCaseDossier());
    }

    if ([1, 2, 4].includes(status)) {
      host.appendChild(renderApplicationForm(status, applicationDraft));
      host.appendChild(renderDocumentGrid(model.DATA_ENTRY_DOCUMENTS));
      if (status === 1) {
        host.appendChild(actionBtn("شروع ورود اطلاعات", () => postEmpty("/application/begin")));
      }
      if ([2, 4].includes(status)) {
        host.appendChild(actionBtn("ارسال درخواست", () => postEmpty("/application/submit")));
      }
    }

    if (status === 3 && ["CreditExpert", "CreditManager", "Admin"].includes(role)) {
      host.appendChild(renderApprovalForm(approvalDraft));
      host.appendChild(
        actionBtn("تأیید اعتبارات", async () => {
          await saveApprovalDetail();
          await postJson("/credit/approve", {});
        })
      );
      host.appendChild(revisionBtn("/credit/revision-request"));
    }

    if (status === 5 && canViewFundCreditCapacity()) {
      host.appendChild(renderFundCreditCapacityBlock());
    }

    if (status === 5 && ["CEO", "Admin"].includes(role)) {
      host.appendChild(actionBtn("تأیید مدیرعامل (اول)", () => postJson("/ceo/initial/approve", {})));
      host.appendChild(revisionBtn("/ceo/initial/reject"));
    }

    if (status === 7 && ["LegalExpert", "LegalManager", "Admin"].includes(role)) {
      host.appendChild(renderDocumentGrid([{ type: 13, label: "قرارداد خام", required: true }]));
      host.appendChild(renderInstallmentEditor());
      host.appendChild(
        actionBtn("تکمیل قرارداد و اقساط", async () => {
          setError("");
          if (!state.installments.length) {
            setError("حداقل یک قساط ثبت کنید.");
            return;
          }
          const installmentErr = validateInstallmentsComplete();
          if (installmentErr) {
            setError(installmentErr);
            return;
          }
          if (!hasDocumentType(13)) {
            setError("قرارداد خام را بارگذاری کنید.");
            return;
          }
          await postEmpty("/legal/setup-complete");
        })
      );
    }

    if ([8, 10, 12].includes(status) && role === "Applicant") {
      host.appendChild(renderDocumentGrid([
        { type: 15, label: "قرارداد امضاشده", required: true },
        { type: 16, label: "پیوست ۱", required: false },
        { type: 17, label: "پیوست ۲", required: false },
      ]));
      host.appendChild(actionBtn("ارسال قرارداد امضاشده", () => postEmpty("/signed-package/submit")));
    }

    if (status === 9 && ["LegalExpert", "LegalManager", "Admin"].includes(role)) {
      host.appendChild(actionBtn("تأیید حقوقی", () => postJson("/legal/approve", {})));
      host.appendChild(revisionBtn("/legal/revision-request"));
    }

    if (status === 11 && ["FinancialExpert", "FinancialManager", "Admin"].includes(role)) {
      host.appendChild(actionBtn("تأیید مالی", () => postJson("/financial/approve", {})));
      host.appendChild(revisionBtn("/financial/revision-request"));
    }

    if (status === 13 && ["LegalExpert", "LegalManager", "Admin"].includes(role)) {
      host.appendChild(renderDocumentGrid([{ type: 22, label: "قرارداد نهایی", required: true }]));
      host.appendChild(actionBtn("تأیید بارگذاری قرارداد نهایی", () => postEmpty("/legal/final-uploaded")));
    }

    if (status === 14 && canViewFundCreditCapacity()) {
      host.appendChild(renderFundCreditCapacityBlock());
    }

    if (status === 14 && ["CEO", "Admin"].includes(role)) {
      host.appendChild(actionBtn("تأیید نهایی مدیرعامل", () => postJson("/ceo/final/approve", {})));
      host.appendChild(revisionBtn("/ceo/final/reject"));
    }

    const showFinancialDisbursement = status === 15 && ["FinancialExpert", "FinancialManager", "Admin"].includes(role);
    const showApplicantRepayment = status === 16 && model.canActOnCase(status, role);
    if (showFinancialDisbursement) {
      host.appendChild(renderPaymentForm());
    }
    if (showApplicantRepayment) {
      host.appendChild(renderApplicantRepaymentForm());
    }
    if (status === 16 && !showApplicantRepayment) {
      host.appendChild(renderInstallmentDashboard());
    }
  }

  function renderApplicationForm(status, draft) {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "درخواست تسهیلات"));
    const app = pick(state.caseData, "application", "Application") || {};
    const fields = [
      ["lRequestedAmount", "مبلغ (ریال)", draft?.requestedAmount ?? pick(app, "requestedAmount", "RequestedAmount")],
      ["lFacilitySubject", "موضوع تسهیلات", draft?.facilitySubject ?? pick(app, "facilitySubject", "FacilitySubject")],
      ["lOfferedGuarantees", "تضامین و وثایق", draft?.offeredGuarantees ?? pick(app, "offeredGuarantees", "OfferedGuarantees")],
    ];
    fields.forEach(([id, label, val]) => {
      const row = el("div", "formrow");
      row.appendChild(el("label", "", label));
      const input = document.createElement("input");
      input.id = id;
      input.value = val != null ? String(val) : "";
      row.appendChild(input);
      card.appendChild(row);
    });
    if ([1, 2, 4].includes(status)) {
      card.appendChild(
        actionBtn("ذخیره درخواست", async () => {
          await state.panel.apiRequest({
            method: "PUT",
            path: lPath("/" + state.caseId + "/application"),
            body: {
              requestedAmount: Number(qs("#lRequestedAmount").value) || null,
              facilitySubject: qs("#lFacilitySubject").value,
              offeredGuarantees: qs("#lOfferedGuarantees").value,
              applicantCategory: 1,
            },
          });
          await refreshCase();
        })
      );
    }
    return card;
  }

  function addApprovalField(card, id, label, value, attrs) {
    const row = el("div", "formrow");
    row.appendChild(el("label", "", label));
    const input = document.createElement("input");
    input.id = id;
    input.value = value != null && value !== "" ? String(value) : "";
    if (attrs) Object.assign(input, attrs);
    row.appendChild(input);
    card.appendChild(row);
    return input;
  }

  function renderApprovalForm(draft) {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "فرم تصویب"));
    const detail = getApprovalDetail();

    const amountInput = addApprovalField(
      card,
      "lApprovedAmount",
      "مبلغ تأییدشده (ریال)",
      draft?.approvedAmount ?? pick(detail, "approvedAmount", "ApprovedAmount") ?? "",
      { type: "number", min: "0", step: "1" }
    );

    const facilityRow = el("div", "formrow");
    facilityRow.appendChild(el("label", "", "نوع تسهیلات"));
    const facilitySel = document.createElement("select");
    facilitySel.id = "lFacilityType";
    const selectedFacility = Number(draft?.facilityType ?? pick(detail, "facilityType", "FacilityType") ?? 3);
    model.FACILITY_TYPES.forEach((ft) => {
      const opt = document.createElement("option");
      opt.value = String(ft.value);
      opt.textContent = ft.label;
      if (ft.value === selectedFacility) opt.selected = true;
      facilitySel.appendChild(opt);
    });
    facilityRow.appendChild(facilitySel);
    card.appendChild(facilityRow);

    const monthsInput = addApprovalField(
      card,
      "lRepaymentMonths",
      "مدت بازپرداخت (ماه)",
      draft?.repaymentMonths ?? pick(detail, "repaymentMonths", "RepaymentMonths") ?? "",
      { type: "number", min: "1", step: "1" }
    );
    const rateInput = addApprovalField(
      card,
      "lAnnualProfitRate",
      "نرخ سود سالانه (٪)",
      draft?.annualProfitRatePercent ?? pick(detail, "annualProfitRatePercent", "AnnualProfitRatePercent") ?? "",
      { type: "number", min: "0", step: "0.01" }
    );
    addApprovalField(
      card,
      "lGracePeriodMonths",
      "دوره تنفس (ماه)",
      draft?.gracePeriodMonths ?? pick(detail, "gracePeriodMonths", "GracePeriodMonths") ?? "",
      { type: "number", min: "0", step: "1" }
    );

    const preview = el("div", "muted");
    preview.id = "lExpectedProfitPreview";
    card.appendChild(preview);

    const profitInputs = { amount: amountInput, months: monthsInput, rate: rateInput };
    [amountInput, monthsInput, rateInput].forEach((input) => {
      input.addEventListener("input", () => updateExpectedProfitPreview(preview, profitInputs));
    });
    updateExpectedProfitPreview(preview, profitInputs);

    card.appendChild(
      actionBtn("ذخیره فرم تصویب", async () => {
        await saveApprovalDetail();
        await refreshCase();
        setInfo("فرم تصویب ذخیره شد.");
      })
    );
    return card;
  }

  function renderDocumentGrid(docs) {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "مدارک"));
    docs.forEach((d) => {
      const row = el("div", "formrow");
      row.appendChild(el("label", "", d.label + (d.required ? " (ضروری)" : "")));
      const input = document.createElement("input");
      input.type = "file";
      input.addEventListener("change", () => {
        const file = input.files && input.files[0];
        if (file) void uploadDocument(d.type, file).catch((e) => setError(e.message));
      });
      row.appendChild(input);
      card.appendChild(row);
    });
    return card;
  }

  function formatRial(value) {
    return Number(value || 0).toLocaleString("fa-IR") + " ریال";
  }

  function formatRialOptional(value) {
    if (value == null || value === "") return "—";
    const n = Number(value);
    if (!Number.isFinite(n)) return "—";
    return n.toLocaleString("fa-IR") + " ریال";
  }

  function displayDetailText(value) {
    if (value == null || value === "" || value === "—") return "—";
    return String(value);
  }

  function pickDetailField(camel, pascal) {
    const value = pick(getApprovalDetail(), camel, pascal);
    if (value == null || value === "" || value === "—") return null;
    return value;
  }

  function resolveDetailOrApp(detailCamel, detailPascal, appCamel, appPascal) {
    const detail = pickDetailField(detailCamel, detailPascal);
    if (detail != null) return detail;
    const app = pick(getApplication(), appCamel, appPascal);
    if (app == null || app === "" || app === "—") return null;
    return app;
  }

  function getApprovalDetail() {
    return pick(state.caseData, "approvalDetail", "ApprovalDetail") || {};
  }

  function getApplication() {
    return pick(state.caseData, "application", "Application") || {};
  }

  function getApprovedAmount() {
    const detail = getApprovalDetail();
    const approved = Number(pick(detail, "approvedAmount", "ApprovedAmount"));
    if (Number.isFinite(approved) && approved > 0) return approved;
    const app = getApplication();
    return Number(pick(app, "requestedAmount", "RequestedAmount")) || 0;
  }

  function getRequestedAmount() {
    return Number(pick(getApplication(), "requestedAmount", "RequestedAmount")) || 0;
  }

  function getExpectedTotalProfit() {
    const detail = getApprovalDetail();
    const stored = Number(pick(detail, "expectedTotalProfit", "ExpectedTotalProfit"));
    if (Number.isFinite(stored) && stored > 0) return stored;
    const approved = getApprovedAmount();
    const rate = Number(pick(detail, "annualProfitRatePercent", "AnnualProfitRatePercent"));
    const months = Number(pick(detail, "repaymentMonths", "RepaymentMonths"));
    if (approved > 0 && rate > 0 && months > 0) {
      return Math.round(approved * (rate / 100) * (months / 12));
    }
    return 0;
  }

  function getRepaymentCheckAmount() {
    const stored = Number(pick(getApprovalDetail(), "repaymentCheckAmount", "RepaymentCheckAmount"));
    if (Number.isFinite(stored) && stored > 0) return stored;
    const installmentTotal = getInstallmentTotals().total;
    if (installmentTotal > 0) return installmentTotal;
    const approved = getApprovedAmount();
    const profit = getExpectedTotalProfit();
    if (approved > 0 && profit > 0) return approved + profit;
    return approved > 0 ? approved : 0;
  }

  function getPaymentDisbursementAmount() {
    const totals = getInstallmentTotals();
    if (totals.total > 0) return totals.total;
    const approved = getApprovedAmount();
    const profit = getExpectedTotalProfit();
    if (approved > 0 && profit > 0) return approved + profit;
    return approved;
  }

  function getInstallmentTotals() {
    return state.installments.reduce(
      (acc, i) => {
        acc.principal += Number(pick(i, "principalAmount", "PrincipalAmount")) || 0;
        acc.profit += Number(pick(i, "profitAmount", "ProfitAmount")) || 0;
        acc.total += Number(pick(i, "totalAmount", "TotalAmount")) || 0;
        return acc;
      },
      { principal: 0, profit: 0, total: 0, count: state.installments.length }
    );
  }

  function getRemainingAmounts() {
    const approved = getApprovedAmount();
    const expectedProfit = getExpectedTotalProfit();
    const totals = getInstallmentTotals();
    return {
      principal: Math.max(0, approved - totals.principal),
      profit: Math.max(0, expectedProfit - totals.profit),
      total: Math.max(0, approved + expectedProfit - totals.total),
    };
  }

  function validateInstallmentsComplete() {
    if (!state.installments.length) return "حداقل یک قساط ثبت کنید.";
    const approved = getApprovedAmount();
    if (approved <= 0) return null;
    const totals = getInstallmentTotals();
    if (Math.abs(totals.principal - approved) > 1) {
      return (
        "مجموع اصل اقساط (" +
        formatRial(totals.principal) +
        ") باید برابر مبلغ تأییدشده (" +
        formatRial(approved) +
        ") باشد."
      );
    }
    const expectedProfit = getExpectedTotalProfit();
    if (expectedProfit > 0 && Math.abs(totals.profit - expectedProfit) > 1) {
      return (
        "مجموع سود اقساط (" +
        formatRial(totals.profit) +
        ") باید برابر سود کل (" +
        formatRial(expectedProfit) +
        ") باشد."
      );
    }
    return null;
  }

  function renderLoanAmountSummary(card) {
    const detail = getApprovalDetail();
    const approved = getApprovedAmount();
    const requested = getRequestedAmount();
    const expectedProfit = getExpectedTotalProfit();
    const repaymentMonths = pick(detail, "repaymentMonths", "RepaymentMonths");
    const gracePeriodMonths = pick(detail, "gracePeriodMonths", "GracePeriodMonths");
    const profitRate = pick(detail, "annualProfitRatePercent", "AnnualProfitRatePercent");
    const totals = getInstallmentTotals();
    const remaining = getRemainingAmounts();
    const hasApprovalDetail = Object.keys(detail).length > 0;

    const summary = el("div", "portal-summary portal-summary--nested");
    const grid = el("div", "portal-summary__grid");
    const items = [
      ["مبلغ تأییدشده", approved > 0 ? formatRial(approved) : "—"],
      ["مبلغ درخواستی", requested > 0 ? formatRial(requested) : "—"],
      ["سود کل مورد انتظار", expectedProfit > 0 ? formatRial(expectedProfit) : "—"],
      ["مدت بازپرداخت", repaymentMonths ? repaymentMonths + " ماه" : "—"],
      ["دوره تنفس", gracePeriodMonths != null ? gracePeriodMonths + " ماه" : "—"],
      ["نرخ سود سالانه", profitRate != null ? profitRate + "٪" : "—"],
      ["مجموع اصل ثبت‌شده", formatRial(totals.principal)],
      ["مجموع سود ثبت‌شده", formatRial(totals.profit)],
      ["مجموع کل ثبت‌شده", formatRial(totals.total)],
    ];
    items.forEach(([label, value]) => {
      const cell = el("div");
      cell.appendChild(el("span", "muted", label));
      cell.appendChild(el("div", "portal-summary__value", value));
      grid.appendChild(cell);
    });
    summary.appendChild(grid);

    const statusBox = el("div", "muted");
    const principalOk = approved > 0 && Math.abs(totals.principal - approved) <= 1;
    const profitOk = expectedProfit <= 0 || Math.abs(totals.profit - expectedProfit) <= 1;
    if (!hasApprovalDetail && !requested) {
      statusBox.textContent = "اطلاعات تصویب و درخواست در پاسخ API یافت نشد.";
      statusBox.className = "alert alert--warn";
    } else if (!approved) {
      statusBox.textContent = "مبلغ تأییدشده در فرم تصویب ثبت نشده است.";
    } else if (totals.count === 0) {
      statusBox.textContent = "اقساط را طوری ثبت کنید که مجموع اصل برابر " + formatRial(approved) + " شود.";
    } else if (!principalOk) {
      statusBox.textContent =
        remaining.principal > 0
          ? "اصل باقی‌مانده: " + formatRial(remaining.principal)
          : "مجموع اصل ثبت‌شده (" + formatRial(totals.principal) + ") بیش از مبلغ تأییدشده (" + formatRial(approved) + ") است.";
      statusBox.className = "alert alert--warn";
    } else if (!profitOk) {
      statusBox.textContent =
        totals.profit > expectedProfit
          ? "مجموع سود ثبت‌شده (" + formatRial(totals.profit) + ") بیش از سود کل (" + formatRial(expectedProfit) + ") است."
          : "سود باقی‌مانده: " + formatRial(remaining.profit);
      statusBox.className = "alert alert--warn";
    } else {
      statusBox.textContent = "مجموع اصل و سود اقساط با مبلغ تأییدشده و سود کل همخوان است.";
      statusBox.className = "alert alert--ok";
    }
    summary.appendChild(statusBox);
    card.appendChild(summary);
  }

  function hasDocumentType(docType) {
    return state.documents.some((d) => Number(pick(d, "documentType", "DocumentType")) === docType);
  }

  function installmentToRequestItem(i) {
    const date = pick(i, "installmentDate", "InstallmentDate");
    return {
      rowNumber: Number(pick(i, "rowNumber", "RowNumber")) || 0,
      installmentDate: date ? String(date).slice(0, 10) : new Date().toISOString().slice(0, 10),
      principalAmount: Number(pick(i, "principalAmount", "PrincipalAmount")) || 0,
      profitAmount: Number(pick(i, "profitAmount", "ProfitAmount")) || 0,
      totalAmount: Number(pick(i, "totalAmount", "TotalAmount")) || 0,
      fundShareOfPrincipal: Number(pick(i, "fundShareOfPrincipal", "FundShareOfPrincipal")) || 0,
      fundShareOfProfit: Number(pick(i, "fundShareOfProfit", "FundShareOfProfit")) || 0,
      fundShareOfTotal: Number(pick(i, "fundShareOfTotal", "FundShareOfTotal")) || 0,
      isGracePeriod: Boolean(pick(i, "isGracePeriod", "IsGracePeriod")),
    };
  }

  function nextInstallmentRowNumber() {
    return state.installments.reduce((max, i) => Math.max(max, Number(pick(i, "rowNumber", "RowNumber")) || 0), 0) + 1;
  }

  function buildInstallmentPayload(newItem) {
    return { installments: state.installments.map(installmentToRequestItem).concat(newItem), nextRow: newItem.rowNumber };
  }

  function setInstallmentModalError(msg) {
    const box = qs("#lInstModalError");
    if (!box) return;
    if (!msg) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.textContent = msg;
    box.classList.remove("hidden");
  }

  function syncInstallmentTotals() {
    const principal = Number(qs("#lInstPrincipal")?.value) || 0;
    const profit = Number(qs("#lInstProfit")?.value) || 0;
    const totalEl = qs("#lInstTotal");
    const fundPrincipalEl = qs("#lInstFundPrincipal");
    const fundProfitEl = qs("#lInstFundProfit");
    const fundTotalEl = qs("#lInstFundTotal");
    if (totalEl && document.activeElement !== totalEl) totalEl.value = String(principal + profit);
    if (fundPrincipalEl && document.activeElement !== fundPrincipalEl) fundPrincipalEl.value = String(principal);
    if (fundProfitEl && document.activeElement !== fundProfitEl) fundProfitEl.value = String(profit);
    const total = Number(totalEl?.value) || principal + profit;
    if (fundTotalEl && document.activeElement !== fundTotalEl) fundTotalEl.value = String(total);
  }

  function openInstallmentModal() {
    const modal = qs("#lInstallmentModal");
    if (!modal) {
      setError("فرم ثبت قساط یافت نشد. صفحه را با Ctrl+F5 بروزرسانی کنید.");
      return;
    }
    setInstallmentModalError("");
    const nextRow = nextInstallmentRowNumber();
    qs("#lInstRowNumber").value = String(nextRow);
    qs("#lInstDate").value = new Date().toISOString().slice(0, 10);
    ["lInstPrincipal", "lInstProfit", "lInstTotal", "lInstFundPrincipal", "lInstFundProfit", "lInstFundTotal"].forEach((id) => {
      const input = qs("#" + id);
      if (input) input.value = "";
    });
    qs("#lInstGrace").checked = false;
    syncInstallmentTotals();
    const remaining = getRemainingAmounts();
    const hint = qs("#lInstModalHint");
    if (hint) {
      const parts = [];
      if (remaining.principal > 0) parts.push("اصل باقی‌مانده: " + formatRial(remaining.principal));
      if (remaining.profit > 0) parts.push("سود باقی‌مانده: " + formatRial(remaining.profit));
      hint.textContent = parts.length ? parts.join(" · ") : "تمام مبلغ تسهیلات در اقساط ثبت شده است.";
    }
    modal.classList.remove("hidden");
  }

  function closeInstallmentModal() {
    setInstallmentModalError("");
    qs("#lInstallmentModal")?.classList.add("hidden");
  }

  function readInstallmentForm() {
    const rowNumber = Number(qs("#lInstRowNumber")?.value);
    const installmentDate = qs("#lInstDate")?.value;
    const principalAmount = Number(qs("#lInstPrincipal")?.value);
    const profitAmount = Number(qs("#lInstProfit")?.value);
    const totalAmount = Number(qs("#lInstTotal")?.value);
    const fundShareOfPrincipal = Number(qs("#lInstFundPrincipal")?.value);
    const fundShareOfProfit = Number(qs("#lInstFundProfit")?.value);
    const fundShareOfTotal = Number(qs("#lInstFundTotal")?.value);
    const isGracePeriod = Boolean(qs("#lInstGrace")?.checked);
    if (!installmentDate) throw new Error("تاریخ قسط را وارد کنید.");
    if (!Number.isFinite(principalAmount) || principalAmount < 0) throw new Error("مبلغ اصل معتبر نیست.");
    if (!Number.isFinite(profitAmount) || profitAmount < 0) throw new Error("مبلغ سود معتبر نیست.");
    if (!Number.isFinite(totalAmount) || totalAmount < 0) throw new Error("مبلغ کل معتبر نیست.");
    if (!Number.isFinite(fundShareOfPrincipal) || fundShareOfPrincipal < 0) throw new Error("سهم صندوق از اصل معتبر نیست.");
    if (!Number.isFinite(fundShareOfProfit) || fundShareOfProfit < 0) throw new Error("سهم صندوق از سود معتبر نیست.");
    if (!Number.isFinite(fundShareOfTotal) || fundShareOfTotal < 0) throw new Error("سهم صندوق از کل معتبر نیست.");
    return {
      rowNumber,
      installmentDate,
      principalAmount,
      profitAmount,
      totalAmount,
      fundShareOfPrincipal,
      fundShareOfProfit,
      fundShareOfTotal,
      isGracePeriod,
    };
  }

  async function afterInstallmentChange(infoMessage) {
    await loadInstallments();
    rerenderInstallmentEditor();
    setInfo(infoMessage);
  }

  function rerenderInstallmentEditor() {
    const existing = qs("#lInstallmentEditorCard");
    if (!existing || !existing.parentNode) return;
    existing.replaceWith(renderInstallmentEditor());
  }

  async function saveInstallmentFromModal() {
    setError("");
    setInstallmentModalError("");
    const newItem = readInstallmentForm();
    const payload = buildInstallmentPayload(newItem);
    await state.panel.apiRequest({
      method: "PUT",
      path: lPath("/" + state.caseId + "/installments"),
      body: { installments: payload.installments },
    });
    closeInstallmentModal();
    await afterInstallmentChange("قساط ردیف " + payload.nextRow + " ثبت شد.");
  }

  function wireInstallmentModal() {
    const modal = qs("#lInstallmentModal");
    if (!modal) return;
    ["lInstPrincipal", "lInstProfit"].forEach((id) => {
      qs("#" + id)?.addEventListener("input", syncInstallmentTotals);
    });
    qs("#lInstTotal")?.addEventListener("input", () => {
      const total = Number(qs("#lInstTotal")?.value) || 0;
      const fundTotalEl = qs("#lInstFundTotal");
      if (fundTotalEl && document.activeElement !== fundTotalEl) fundTotalEl.value = String(total);
    });
    qs("#lInstModalCancel")?.addEventListener("click", closeInstallmentModal);
    modal.addEventListener("click", (e) => {
      if (e.target === modal) closeInstallmentModal();
    });
    qs("#lInstModalSave")?.addEventListener("click", () => {
      void saveInstallmentFromModal().catch((e) => setInstallmentModalError(e.message || String(e)));
    });
  }

  async function deleteInstallment(rowNumber) {
    setError("");
    const remaining = state.installments
      .filter((i) => Number(pick(i, "rowNumber", "RowNumber")) !== rowNumber)
      .map(installmentToRequestItem);
    await state.panel.apiRequest({
      method: "PUT",
      path: lPath("/" + state.caseId + "/installments"),
      body: { installments: remaining },
    });
    await afterInstallmentChange("قساط ردیف " + rowNumber + " حذف شد.");
  }

  function appendInstallmentTable(card, editable) {
    if (!state.installments.length) {
      card.appendChild(el("div", "muted", "اقساطی ثبت نشده است. با «افزودن قساط» ردیف‌های جدول را تکمیل کنید."));
      return;
    }
    const instLabels = ["ردیف", "تاریخ", "اصل", "سود", "کل", "تنفس", "وضعیت"];
    const wrap = el("div", "data-table-wrap");
    const table = document.createElement("table");
    table.className = "data-table";
    table.innerHTML =
      "<thead><tr>" +
      instLabels.map((h) => "<th>" + h + "</th>").join("") +
      (editable ? "<th></th>" : "") +
      "</tr></thead>";
    const tbody = document.createElement("tbody");
    state.installments.forEach((i) => {
      const tr = document.createElement("tr");
      const paid = pick(i, "isPaid", "IsPaid");
      const grace = pick(i, "isGracePeriod", "IsGracePeriod");
      const rowNum = pick(i, "rowNumber", "RowNumber");
      const values = [
        rowNum,
        pick(i, "installmentDate", "InstallmentDate"),
        formatRial(pick(i, "principalAmount", "PrincipalAmount")),
        formatRial(pick(i, "profitAmount", "ProfitAmount")),
        formatRial(pick(i, "totalAmount", "TotalAmount")),
        grace ? "بله" : "خیر",
        paid ? "پرداخت‌شده" : "پرداخت‌نشده",
      ];
      values.forEach((val, idx) => {
        const td = document.createElement("td");
        td.setAttribute("data-label", instLabels[idx]);
        td.textContent = val;
        tr.appendChild(td);
      });
      if (editable) {
        const actionTd = document.createElement("td");
        actionTd.setAttribute("data-label", "");
        const delBtn = el("button", "btn btn--small btn--warn", "حذف");
        delBtn.type = "button";
        delBtn.addEventListener("click", () => {
          void deleteInstallment(Number(rowNum)).catch((e) => setError(e.message || String(e)));
        });
        actionTd.appendChild(delBtn);
        tr.appendChild(actionTd);
      }
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    wrap.appendChild(table);
    card.appendChild(wrap);
  }

  function renderInstallmentEditor() {
    const card = el("div", "card portal-card");
    card.id = "lInstallmentEditorCard";
    card.appendChild(el("div", "card__title", "جدول اقساط"));
    renderLoanAmountSummary(card);
    const row = el("div", "row");
    const btn = el("button", "btn btn--primary", "افزودن قساط");
    btn.type = "button";
    btn.addEventListener("click", () => {
      openInstallmentModal();
    });
    row.appendChild(btn);
    card.appendChild(row);
    appendInstallmentTable(card, true);
    return card;
  }

  function appendFormField(card, id, label, type, value) {
    const row = el("div", "formrow");
    row.appendChild(el("label", "", label));
    const input = document.createElement(type === "textarea" ? "textarea" : "input");
    input.id = id;
    if (type !== "textarea") input.type = type;
    if (value != null && value !== "") input.value = String(value);
    row.appendChild(input);
    card.appendChild(row);
    return input;
  }

  function nextPaymentStageNumber() {
    return (state.payments || []).reduce(
      (max, p) => Math.max(max, Number(pick(p, "stageNumber", "StageNumber")) || 0),
      0
    ) + 1;
  }

  function findPaymentReceiptS3Key() {
    const docs = (state.documents || []).filter(
      (d) => Number(pick(d, "documentType", "DocumentType")) === 23 && !pick(d, "isDeleted", "IsDeleted")
    );
    if (!docs.length) return null;
    const latest = docs.reduce((a, b) => {
      const va = Number(pick(a, "version", "Version")) || 0;
      const vb = Number(pick(b, "version", "Version")) || 0;
      return vb >= va ? b : a;
    });
    return pick(latest, "s3Key", "S3Key") || null;
  }

  function renderPaymentForm() {
    const approved = getApprovedAmount();
    const profit = getExpectedTotalProfit();
    const disbursement = getPaymentDisbursementAmount();
    const today = new Date().toISOString().slice(0, 10);
    const defaultStage = nextPaymentStageNumber();
    const existingReceiptKey = findPaymentReceiptS3Key();

    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "ثبت پرداخت"));

    if (disbursement > 0) {
      const parts = [];
      if (approved > 0) parts.push("اصل: " + formatRial(approved));
      if (profit > 0) parts.push("سود: " + formatRial(profit));
      card.appendChild(
        el("p", "muted", "مبلغ کل قابل پرداخت (اصل + سود): " + formatRial(disbursement) + (parts.length ? " (" + parts.join(" · ") + ")" : ""))
      );
    }
    card.appendChild(
      el(
        "p",
        "muted",
        "مبلغ، تاریخ، شماره تراکنش و رسید پرداخت را وارد کنید. پس از ثبت، پرونده به مرحله بازپرداخت منتقل می‌شود."
      )
    );

    const payments = state.payments || [];
    if (payments.length) {
      card.appendChild(el("div", "card__title", "پرداخت‌های ثبت‌شده"));
      const list = el("div", "portal-payments-list");
      payments.forEach((payment) => {
        const amount = Number(pick(payment, "amount", "Amount")) || 0;
        const date = pick(payment, "paymentDate", "PaymentDate") || "—";
        const txn = pick(payment, "transactionNumber", "TransactionNumber") || "—";
        const stage = pick(payment, "stageNumber", "StageNumber") || "—";
        const hasReceipt = !!(pick(payment, "receiptS3Key", "ReceiptS3Key"));
        list.appendChild(
          el(
            "div",
            "portal-payments-list__item",
            formatRial(amount) + " · " + date + " · " + txn + " · مرحله " + stage + (hasReceipt ? " · دارای رسید" : "")
          )
        );
      });
      card.appendChild(list);
    }

    appendFormField(card, "lPayAmount", "مبلغ (ریال)", "number", disbursement > 0 ? disbursement : "");
    appendFormField(card, "lPayDate", "تاریخ پرداخت", "date", today);
    appendFormField(card, "lPayTxn", "شماره تراکنش", "text", "");
    appendFormField(card, "lPayStage", "شماره مرحله پرداخت", "number", defaultStage);
    appendFormField(card, "lPayNotes", "یادداشت (اختیاری)", "textarea", "");

    const receiptRow = el("div", "formrow");
    receiptRow.appendChild(el("label", "", "رسید پرداخت"));
    const receiptWrap = el("div", "row");
    const receiptInput = document.createElement("input");
    receiptInput.type = "file";
    receiptInput.id = "lPayReceipt";
    receiptInput.accept = "image/*,.pdf";
    if (existingReceiptKey) receiptInput.dataset.s3Key = existingReceiptKey;
    const receiptStatus = el("span", "muted", existingReceiptKey ? "✓ رسید بارگذاری شده" : "");
    receiptInput.addEventListener("change", () => {
      const file = receiptInput.files && receiptInput.files[0];
      if (!file) return;
      void uploadDocument(23, file)
        .then((s3Key) => {
          receiptInput.dataset.s3Key = s3Key || findPaymentReceiptS3Key() || "";
          receiptStatus.textContent = "✓ رسید بارگذاری شد";
        })
        .catch((e) => setError(e.message));
    });
    receiptWrap.appendChild(receiptInput);
    receiptWrap.appendChild(receiptStatus);
    receiptRow.appendChild(receiptWrap);
    card.appendChild(receiptRow);

    card.appendChild(
      actionBtn("ثبت پرداخت", async () => {
        const amount = Number(qs("#lPayAmount")?.value);
        if (!(amount > 0)) {
          setError("مبلغ پرداخت باید بزرگ‌تر از صفر باشد.");
          return;
        }
        const paymentDate = qs("#lPayDate")?.value;
        if (!paymentDate) {
          setError("تاریخ پرداخت الزامی است.");
          return;
        }
        const transactionNumber = (qs("#lPayTxn")?.value || "").trim();
        if (!transactionNumber) {
          setError("شماره تراکنش الزامی است.");
          return;
        }
        const stageNumber = Number(qs("#lPayStage")?.value);
        if (!(stageNumber > 0)) {
          setError("شماره مرحله پرداخت باید بزرگ‌تر از صفر باشد.");
          return;
        }
        const notes = (qs("#lPayNotes")?.value || "").trim() || null;
        const receiptS3Key = receiptInput.dataset.s3Key || findPaymentReceiptS3Key() || null;

        await state.panel.apiRequest({
          method: "POST",
          path: lPath("/" + state.caseId + "/payments"),
          body: { amount, paymentDate, transactionNumber, stageNumber, notes, receiptS3Key },
        });
        await refreshCase();
        setInfo("پرداخت ثبت شد.");
      })
    );
    return card;
  }

  function renderInstallmentDashboard() {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "داشبورد اقساط متقاضی"));
    renderLoanAmountSummary(card);
    appendInstallmentTable(card, false);
    return card;
  }

  function unpaidInstallments() {
    return (state.installments || []).filter((i) => !pick(i, "isPaid", "IsPaid") && !pick(i, "isGracePeriod", "IsGracePeriod"));
  }

  function allRepayableInstallmentsPaid() {
    return unpaidInstallments().length === 0 && (state.installments || []).length > 0;
  }

  function renderApplicantRepaymentForm() {
    const card = el("div", "card portal-card");
    card.id = "lApplicantRepaymentCard";
    card.appendChild(el("div", "card__title", "ثبت پرداخت قسط"));
    card.appendChild(
      el(
        "p",
        "muted",
        "مبلغ، تاریخ، شماره تراکنش و رسید پرداخت را برای قساط انتخاب‌شده وارد کنید. پس از تسویه همه اقساط، «تکمیل بازپرداخت» را بزنید."
      )
    );
    renderLoanAmountSummary(card);
    appendInstallmentTable(card, false);

    const unpaid = unpaidInstallments();
    if (!state.installments.length) {
      card.appendChild(el("div", "muted", "جدول اقساط هنوز ثبت نشده است."));
      return card;
    }

    if (!unpaid.length) {
      card.appendChild(
        actionBtn("تکمیل بازپرداخت", async () => {
          await postEmpty("/repayment/complete");
          setInfo("بازپرداخت تکمیل شد.");
        })
      );
      return card;
    }

    const today = new Date().toISOString().slice(0, 10);
    const firstUnpaid = unpaid[0];
    const defaultAmount = Number(pick(firstUnpaid, "totalAmount", "TotalAmount")) || 0;

    if (unpaid.length > 1) {
      const row = el("div", "formrow");
      row.appendChild(el("label", "", "قساط مورد پرداخت"));
      const select = document.createElement("select");
      select.id = "lRepayInstallment";
      unpaid.forEach((i) => {
        const opt = document.createElement("option");
        opt.value = pick(i, "id", "Id");
        opt.dataset.amount = String(pick(i, "totalAmount", "TotalAmount") || "");
        opt.dataset.date = String(pick(i, "installmentDate", "InstallmentDate") || "").slice(0, 10);
        opt.textContent =
          "ردیف " +
          pick(i, "rowNumber", "RowNumber") +
          " · " +
          formatRial(pick(i, "totalAmount", "TotalAmount")) +
          " · " +
          (pick(i, "installmentDate", "InstallmentDate") || "—");
        select.appendChild(opt);
      });
      select.addEventListener("change", () => {
        const opt = select.selectedOptions[0];
        const amountInput = qs("#lRepayAmount");
        const dateInput = qs("#lRepayDate");
        if (amountInput && opt) amountInput.value = opt.dataset.amount || "";
        if (dateInput && opt && opt.dataset.date) dateInput.value = opt.dataset.date;
      });
      row.appendChild(select);
      card.appendChild(row);
    } else {
      const hidden = document.createElement("input");
      hidden.type = "hidden";
      hidden.id = "lRepayInstallment";
      hidden.value = pick(firstUnpaid, "id", "Id");
      card.appendChild(hidden);
    }

    appendFormField(card, "lRepayAmount", "مبلغ (ریال)", "number", defaultAmount);
    appendFormField(card, "lRepayDate", "تاریخ پرداخت", "date", pick(firstUnpaid, "installmentDate", "InstallmentDate")?.slice?.(0, 10) || today);
    appendFormField(card, "lRepayTxn", "شماره تراکنش", "text", "");
    appendFormField(card, "lRepayNotes", "یادداشت (اختیاری)", "textarea", "");

    const receiptRow = el("div", "formrow");
    receiptRow.appendChild(el("label", "", "رسید پرداخت"));
    const receiptWrap = el("div", "row");
    const receiptInput = document.createElement("input");
    receiptInput.type = "file";
    receiptInput.id = "lRepayReceipt";
    receiptInput.accept = "image/*,.pdf";
    const receiptStatus = el("span", "muted", "");
    receiptInput.addEventListener("change", () => {
      const file = receiptInput.files && receiptInput.files[0];
      if (!file) return;
      void uploadDocument(23, file)
        .then((s3Key) => {
          receiptInput.dataset.s3Key = s3Key || "";
          receiptStatus.textContent = "✓ رسید بارگذاری شد";
        })
        .catch((e) => setError(e.message));
    });
    receiptWrap.appendChild(receiptInput);
    receiptWrap.appendChild(receiptStatus);
    receiptRow.appendChild(receiptWrap);
    card.appendChild(receiptRow);

    card.appendChild(
      actionBtn("ثبت پرداخت قسط", async () => {
        const installmentId = qs("#lRepayInstallment")?.value;
        const amount = Number(qs("#lRepayAmount")?.value);
        const paidDate = qs("#lRepayDate")?.value;
        const transactionNumber = (qs("#lRepayTxn")?.value || "").trim();
        const notes = (qs("#lRepayNotes")?.value || "").trim() || null;
        const receiptS3Key = receiptInput.dataset.s3Key || null;

        if (!installmentId) throw new Error("قساط انتخاب نشده است.");
        if (!(amount > 0)) throw new Error("مبلغ پرداخت باید بزرگ‌تر از صفر باشد.");
        if (!paidDate) throw new Error("تاریخ پرداخت الزامی است.");
        if (!transactionNumber) throw new Error("شماره تراکنش الزامی است.");
        if (!receiptS3Key) throw new Error("رسید پرداخت را بارگذاری کنید.");

        await state.panel.apiRequest({
          method: "POST",
          path: lPath("/" + state.caseId + "/installments/" + installmentId + "/mark-paid"),
          body: { paidDate, amount, transactionNumber, receiptS3Key, notes },
        });
        await refreshCase();
        setInfo("پرداخت قسط ثبت شد.");
      })
    );

    if (unpaid.length) {
      card.appendChild(el("p", "muted", unpaid.length + " قساط هنوز پرداخت نشده است."));
    }

    return card;
  }

  function actionBtn(label, handler) {
    const btn = el("button", "btn btn--primary", label);
    btn.type = "button";
    btn.addEventListener("click", () => {
      void handler().catch((e) => {
        setError(e.message || String(e));
      });
    });
    const wrap = el("div", "row");
    wrap.appendChild(btn);
    return wrap;
  }

  function revisionBtn(path) {
    const wrap = el("div", "row");
    const input = document.createElement("input");
    input.placeholder = "پیام اصلاح";
    const btn = el("button", "btn btn--warn", "درخواست اصلاح");
    btn.type = "button";
    btn.addEventListener("click", () => {
      void postJson(path, { message: input.value || "اصلاح لازم است" }).catch((e) => setError(e.message));
    });
    wrap.appendChild(input);
    wrap.appendChild(btn);
    return wrap;
  }

  async function postEmpty(path) {
    setError("");
    const statusBefore = pickStatus(state.caseData);
    const res = await state.panel.apiRequest({ method: "POST", path: lPath("/" + state.caseId + path) });
    if (!res.ok) {
      throw new Error((res.body && (res.body.message || res.body.Message)) || "درخواست ناموفق بود.");
    }
    await refreshCase(statusBefore);
    setInfo("عملیات انجام شد.");
  }

  async function postJson(path, body) {
    setError("");
    const statusBefore = pickStatus(state.caseData);
    const res = await state.panel.apiRequest({ method: "POST", path: lPath("/" + state.caseId + path), body });
    if (!res.ok) {
      throw new Error((res.body && (res.body.message || res.body.Message)) || "درخواست ناموفق بود.");
    }
    await refreshCase(statusBefore);
    setInfo("عملیات انجام شد.");
  }

  function wirePortalHeader() {
    qs("#lPortalRefreshCase")?.addEventListener("click", () => {
      void refreshCase().catch((e) => setError(e.message));
    });
    qs("#lCaseTitleSave")?.addEventListener("click", () => {
      void (async () => {
        if (!state.caseId) {
          setError("شناسه پرونده مشخص نیست.");
          return;
        }
        setError("");
        const title = qs("#lCaseTitleInput")?.value?.trim() || null;
        await state.panel.apiRequest({
          method: "PUT",
          path: lPath("/" + state.caseId + "/title"),
          body: { title },
        });
        await refreshCase();
        setInfo("عنوان ذخیره شد.");
      })().catch((e) => setError(e.message || String(e)));
    });
  }

  function wireCreateCase() {
    qs("#lApplicantType").addEventListener("change", () => {
      qs("#lCompanyRow").style.display = qs("#lApplicantType").value === "2" ? "" : "none";
    });
    qs("#lLoadCompanies").addEventListener("click", () => {
      void (async () => {
        const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/identity/companies/mine" });
        const list = unwrap(res.body) || [];
        const sel = qs("#lCompanyId");
        sel.innerHTML = "";
        list.forEach((c) => {
          const opt = document.createElement("option");
          opt.value = pick(c, "id", "Id");
          opt.textContent = pick(c, "name", "Name");
          sel.appendChild(opt);
        });
      })().catch((e) => setError(e.message));
    });
    qs("#lCreateCase").addEventListener("click", () => {
      void (async () => {
        setError("");
        const applicantType = Number(qs("#lApplicantType").value);
        const companyId = qs("#lCompanyId").value || null;
        const title = qs("#lCaseTitleHub")?.value?.trim() || qs("#lCaseTitleInput")?.value?.trim() || null;
        const res = await state.panel.apiRequest({
          method: "POST",
          path: lPath(""),
          body: { applicantType, companyId: companyId || null, title },
        });
        const data = unwrap(res.body);
        state.caseId = pick(data, "id", "Id");
        state.panel.setLoanCaseId(state.caseId);
        await refreshCase();
        setInfo("پرونده تسهیلات ایجاد شد.");
      })().catch((e) => setError(e.message));
    });
    qs("#lLoadCase").addEventListener("click", () => {
      state.caseId = qs("#lCaseIdInput").value.trim();
      if (!state.caseId) return;
      void refreshCase().catch((e) => setError(e.message));
    });
  }

  window.initLoanPortal = function initLoanPortal(panel) {
    state.panel = panel;
    wireInstallmentModal();
    wirePortalHeader();
    if (qs("#lCreateCase")) wireCreateCase();
    const id = panel.getLoanCaseId && panel.getLoanCaseId();
    if (id) {
      state.caseId = id;
      void refreshCase();
    }
    document.addEventListener("testpanel:case-changed", (ev) => {
      if (ev.detail?.module !== "loan") return;
      state.caseId = ev.detail?.caseId || panel.getLoanCaseId() || "";
      if (state.caseId) void refreshCase().catch((e) => setError(e.message));
    });
    document.addEventListener("testpanel:open-comment-step", (ev) => {
      if (ev.detail?.module && ev.detail.module !== "loan") return;
      const target =
        (ev.detail?.commentId &&
          document.querySelector(`#loanPortalRoot [data-comment-id="${ev.detail.commentId}"]`)) ||
        (ev.detail?.phase != null &&
          document.querySelector(`#loanPortalRoot [data-comment-phase="${ev.detail.phase}"]`));
      target?.scrollIntoView({ behavior: "smooth", block: "center" });
      target?.classList.add("is-highlight");
    });
  };
})();
