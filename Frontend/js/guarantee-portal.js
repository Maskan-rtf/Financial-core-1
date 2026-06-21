/* global GuaranteeWorkflowModel */
(function () {
  const model = window.GuaranteeWorkflowModel;
  const state = { panel: null, caseId: "", caseData: null, documents: [], comments: [], history: [], busy: false };
  let uploadFieldCounter = 0;

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
    return (model.stepForStatus(status) || {}).phase || 0;
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

  async function refreshCase() {
    if (!state.caseId) return;
    setError("");
    const res = await state.panel.apiRequest({ method: "GET", path: gPath("/" + state.caseId) });
    state.caseData = unwrap(res.body);
    const docs = await state.panel.apiRequest({ method: "GET", path: gPath("/" + state.caseId + "/documents") });
    state.documents = unwrap(docs.body) || [];
    const commentsRes = await state.panel.apiRequest({
      method: "GET",
      path: gPath("/" + state.caseId + "/comments?includeInternal=" + (isInternalUser() ? "true" : "false")),
    });
    state.comments = unwrap(commentsRes.body) || [];
    try {
      const historyRes = await state.panel.apiRequest({ method: "GET", path: gPath("/" + state.caseId + "/history") });
      state.history = unwrap(historyRes.body) || [];
    } catch (_) {
      state.history = [];
    }
    render();
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
    const step = model.stepForStatus(st);
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

  function renderActionHint() {
    const box = qs("#gPortalActionHint");
    if (!box || !state.caseData) return;

    const status = pickStatus(state.caseData);
    const role = getSessionRole();
    const step = model.stepForStatus(status);
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
      text = "پرونده تکمیل شده است.";
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

  function shouldShowCaseDossier(status) {
    return isInternalUser() && status >= 3 && status <= 11;
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
    body.appendChild(el("div", "muted portal-stage__hint", "خلاصه درخواست متقاضی، فرم تصویب (در صورت وجود) و همه فایل‌های بارگذاری‌شده."));

    renderProfileSummary(body);
    renderApplicantApplicationReadOnly(body);

    const afWrap = el("div", "card portal-card portal-card--nested");
    afWrap.appendChild(el("div", "card__title", "فرم تصویب (ثبت‌شده)"));
    renderApprovalFormReadOnly(afWrap);
    body.appendChild(afWrap);

    const docsWrap = el("div", "card portal-card portal-card--nested");
    docsWrap.appendChild(el("div", "card__title", "همه مدارک و پیوست‌ها"));
    renderAllDocumentsArchive(docsWrap);
    body.appendChild(docsWrap);

    const commentsWrap = el("div", "card portal-card portal-card--nested");
    commentsWrap.appendChild(el("div", "card__title", "تاریخچه نظرات و درخواست‌های اصلاح"));
    renderDossierComments(commentsWrap);
    body.appendChild(commentsWrap);

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

    const gt = model.normalizeGuaranteeType(pick(app, "guaranteeType", "GuaranteeType"));
    const kb = pick(app, "isKnowledgeBasedProduct", "IsKnowledgeBasedProduct");

    renderReadOnlyBlock(card, "درخواست متقاضی (ثبت‌شده در ورود اطلاعات — فقط نمایش)", [
      ["نوع ضمانت‌نامه", labelFromOptions(gt, model.GUARANTEE_TYPES)],
      ["موضوع ضمانت‌نامه (قرارداد پایه)", pick(app, "contractSubject", "ContractSubject")],
      ["محصول دانش‌بنیان", kb === true || kb === "true" ? "بله" : kb === false || kb === "false" ? "خیر" : "—"],
      ["نام ذی‌نفع", pick(app, "beneficiaryName", "BeneficiaryName")],
      ["شناسه ملی ذی‌نفع", pick(app, "beneficiaryNationalId", "BeneficiaryNationalId")],
      ["نوع شرکت ذی‌نفع", labelFromOptions(pick(app, "beneficiaryCompanyType", "BeneficiaryCompanyType"), model.BENEFICIARY_COMPANY_TYPES)],
      ["دسته‌بندی متقاضی", labelFromOptions(pick(app, "applicantCategory", "ApplicantCategory"), model.APPLICANT_CATEGORIES)],
      ["دسته‌بندی سایر", pick(app, "applicantCategoryOther", "ApplicantCategoryOther")],
      ["نوع شرکت متقاضی (حقوقی)", labelFromOptions(pick(app, "applicantLegalForm", "ApplicantLegalForm"), model.APPLICANT_LEGAL_FORMS)],
      ["موضوع تسهیلات", pick(app, "facilitySubject", "FacilitySubject")],
      ["شماره قرارداد پایه / مناقصه", pick(app, "baseContractNumber", "BaseContractNumber")],
      ["مبلغ قرارداد پایه (ریال)", pick(app, "baseContractAmount", "BaseContractAmount")],
      ["مبلغ قرارداد پایه (حروف)", pick(app, "baseContractAmountInWords", "BaseContractAmountInWords")],
      ["نرخ تعدیل قرارداد (٪)", pick(app, "priceAdjustmentRatePercent", "PriceAdjustmentRatePercent")],
      ["استان محل اجرا", pick(app, "executionProvince", "ExecutionProvince")],
      ["مبلغ ضمانت‌نامه درخواستی (ریال)", pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount")],
      ["مدت اعتبار اولیه (روز)", pick(app, "initialValidityDays", "InitialValidityDays")],
      ["اعتبار از", formatDateInput(pick(app, "validityFrom", "ValidityFrom"))],
      ["اعتبار تا", formatDateInput(pick(app, "validityTo", "ValidityTo"))],
      ["تضمین و وثایق قابل ارائه", pick(app, "collateralDescription", "CollateralDescription")],
    ]);
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
    return {
      guaranteeType: gt || null,
      guaranteeAmount: pick(app, "requestedGuaranteeAmount", "RequestedGuaranteeAmount") ?? null,
      guaranteeAmountInWords:
        pick(saved, "guaranteeAmountInWords", "GuaranteeAmountInWords") ||
        pick(app, "baseContractAmountInWords", "BaseContractAmountInWords") ||
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

  function renderPrimaryActions(parent) {
    const status = pickStatus(state.caseData);
    const step = model.stepForStatus(status);
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
    return {
      guaranteeType: gt ? Number(gt) : null,
      contractSubject: text("gContractSubject"),
      isKnowledgeBasedProduct: qs("#gKnowledgeBased")?.value === "true",
      beneficiaryName: text("gBeneficiaryName"),
      beneficiaryNationalId: text("gBeneficiaryNationalId"),
      beneficiaryCompanyType: num("gBeneficiaryCompanyType"),
      applicantCategory: Number(qs("#gApplicantCategory")?.value || 0),
      applicantCategoryOther: text("gApplicantCategoryOther"),
      applicantLegalForm: num("gApplicantLegalForm"),
      facilitySubject: text("gFacilitySubject"),
      baseContractNumber: text("gBaseContractNumber"),
      baseContractAmount: num("gBaseContractAmount"),
      baseContractAmountInWords: text("gBaseContractAmountWords"),
      priceAdjustmentRatePercent: num("gPriceAdjustmentRate"),
      executionProvince: text("gExecutionProvince"),
      requestedGuaranteeAmount: num("gRequestedAmount"),
      initialValidityDays: num("gInitialValidityDays"),
      validityFrom: text("gValidityFrom") || null,
      validityTo: text("gValidityTo") || null,
      collateralDescription: text("gCollateral"),
    };
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
    set("gFacilitySubject", pick(app, "facilitySubject", "FacilitySubject"));
    set("gBaseContractNumber", pick(app, "baseContractNumber", "BaseContractNumber"));
    set("gBaseContractAmount", pick(app, "baseContractAmount", "BaseContractAmount"));
    set("gBaseContractAmountWords", pick(app, "baseContractAmountInWords", "BaseContractAmountInWords"));
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
    box.appendChild(field("دسته‌بندی سایر (توضیح)", "gApplicantCategoryOther", "text", pick(app, "applicantCategoryOther", "ApplicantCategoryOther")));
    box.appendChild(
      selectField("نوع شرکت متقاضی (حقوقی)", "gApplicantLegalForm", model.APPLICANT_LEGAL_FORMS, pick(app, "applicantLegalForm", "ApplicantLegalForm"))
    );
    box.appendChild(field("موضوع تسهیلات درخواستی", "gFacilitySubject", "text", pick(app, "facilitySubject", "FacilitySubject")));
    box.appendChild(field("شماره قرارداد پایه / مناقصه", "gBaseContractNumber", "text", pick(app, "baseContractNumber", "BaseContractNumber")));
    box.appendChild(field("مبلغ قرارداد پایه (ریال)", "gBaseContractAmount", "number", pick(app, "baseContractAmount", "BaseContractAmount")));
    box.appendChild(field("مبلغ قرارداد پایه (حروف)", "gBaseContractAmountWords", "text", pick(app, "baseContractAmountInWords", "BaseContractAmountInWords")));
    box.appendChild(
      field("نرخ تعدیل قرارداد (٪ — نه مبلغ ریالی)", "gPriceAdjustmentRate", "number", pick(app, "priceAdjustmentRatePercent", "PriceAdjustmentRatePercent"), {
        min: 0,
        max: 999.99,
        step: 0.01,
        placeholder: "مثلاً 15.5",
      })
    );
    box.appendChild(field("استان محل اجرا", "gExecutionProvince", "text", pick(app, "executionProvince", "ExecutionProvince")));
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
    const presignRes = await state.panel.apiRequest({
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

    await fetch(uploadUrl, {
      method: "PUT",
      body: file,
      headers: { "Content-Type": mimeType },
    });

   await state.panel.apiRequest({
     method: "POST",
      path: gPath("/" + state.caseId + "/documents/confirm?s3Key=" + encodeURIComponent(s3Key) + "&originalFileName=" + encodeURIComponent(file.name)),
      body: null,
      json: false,
    });
  }

  async function handleUpload(input) {
    if (!state.caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const file = input.files && input.files[0];
    if (!file) return;
    const documentType = Number(input.dataset.uploadType);
    if (!Number.isFinite(documentType) || documentType <= 0) {
      throw new Error("نوع مدرک نامعتبر است.");
    }
    await uploadDocument(documentType, file);
    input.value = "";
    const status = input.closest(".portal-upload-row__control")?.querySelector(".portal-upload-row__status");
    if (status) {
      status.textContent = "✓ بارگذاری شد: " + file.name;
      status.classList.add("is-uploaded");
      status.closest(".portal-upload-row")?.classList.add("portal-upload-row--done");
    }
    await refreshCase();
  }

  function renderStage() {
    const host = qs("#gPortalStages");
    if (!host) return;
    host.innerHTML = "";
    if (!state.caseData) return;

    const status = pickStatus(state.caseData);
    const step = model.stepForStatus(status);
    const role = getSessionRole();

    const card = el("div", "portal-stage card portal-card");
    card.appendChild(el("div", "portal-stage__title", step.title + " (وضعیت " + status + ")"));
    card.appendChild(el("div", "portal-stage__meta muted", "نقش جاری: " + (role || "نامشخص")));

    const canAct = model.canActOnCase(role, step.unit);
    const reviewWithComments = status === 3 || status === 8;
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

    const workflowUploadStatus = status === 6 || status === 7 || status === 9 || status === 11;
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
    }

    host.appendChild(card);
  }

  function actionsForStatus(status) {
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
    if (state.busy) return;
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
    setInfo("");
    try {
      let body = null;
      if (action.needsMessage) {
        const msg = prompt("پیام / توضیح:");
        if (!msg) return;
        body = { message: msg, comment: msg };
      } else if (action.needsReason) {
        const reason = prompt("دلیل لغو پرونده:");
        if (!reason) return;
        if (!confirm("آیا از لغو این پرونده اطمینان دارید؟")) return;
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
      } else if (action.id === "approval-save" || action.id === "approval-submit") {
        body = readApprovalForm();
      }
      if (action.id === "approval-submit") {
        if (!confirm("آیا از ارسال فرم تصویب به مدیرعامل اطمینان دارید؟")) return;
        await state.panel.apiRequest({
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
        if (!confirm("آیا از ارسال پرونده به واحد اعتبارات اطمینان دارید؟")) return;
        const saveBody = readApplicationForm();
        if (
          saveBody.priceAdjustmentRatePercent != null &&
          (saveBody.priceAdjustmentRatePercent > 999.99 || saveBody.priceAdjustmentRatePercent < 0)
        ) {
          throw new Error(
            "نرخ تعدیل باید بین ۰ تا ۹۹۹٫۹۹ (درصد) باشد — مبلغ ریالی را در فیلد مبلغ ضمانت‌نامه وارد کنید."
          );
        }
        await state.panel.apiRequest({
          method: "PUT",
          path: gPath("/" + state.caseId + "/application"),
          body: saveBody,
        });
        const caseRes = await state.panel.apiRequest({
          method: "GET",
          path: gPath("/" + state.caseId),
        });
        state.caseData = unwrap(caseRes.body);
        const docsRes = await state.panel.apiRequest({
          method: "GET",
          path: gPath("/" + state.caseId + "/documents"),
        });
        state.documents = unwrap(docsRes.body) || [];
        const gt = savedGuaranteeType() || formGuaranteeType(document);
        if (!requiredDocumentsComplete(gt, document)) {
          throw new Error(formatMissingDocumentsError(missingRequiredDocuments(gt, document), gt));
        }
      }

      const res = await state.panel.apiRequest({
        method: action.method,
        path: gPath("/" + state.caseId + action.path),
        body: action.id === "submit-app" || action.id === "approval-submit" ? {} : body,
      });
      if (!res.ok) {
        const msg =
          (res.body && (res.body.message || res.body.Message)) ||
          "درخواست با کد " + res.status + " ناموفق بود.";
        throw new Error(msg);
      }

      await refreshCase();

      if (action.id === "submit-app") {
        setInfo("پرونده با موفقیت به واحد اعتبارات ارسال شد — وضعیت: بررسی اعتبارات.");
      } else if (action.id === "save-app") {
        setInfo("درخواست ذخیره شد.");
      } else if (action.id === "credit-approve") {
        setInfo("بررسی اعتبارات تأیید شد.");
      } else if (action.id === "credit-revision") {
        setInfo("درخواست اصلاح برای متقاضی ثبت شد.");
      } else if (action.id === "fin-approve") {
        setInfo("مدارک مالی تأیید شد.");
      } else if (action.id === "fin-revision") {
        setInfo("درخواست اصلاح برای متقاضی ثبت شد.");
      } else if (action.id === "approval-save") {
        setInfo("فرم تصویب ذخیره شد.");
      } else if (action.id === "approval-submit") {
        setInfo("فرم تصویب ارسال شد — پرونده به تأیید مدیرعامل رفت.");
      } else if (action.id === "cancel-case" || action.id === "ceo-cancel" || action.id === "ceo-final-cancel") {
        setInfo("پرونده لغو شد.");
      }
    } catch (e) {
      setError(e.message || String(e));
      scrollToPortalMessage();
    } finally {
      state.busy = false;
    }
  }

  function render() {
    renderSummary();
    renderStepper();
    renderStage();
    renderActionHint();
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
