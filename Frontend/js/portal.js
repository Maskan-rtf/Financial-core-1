/* global WorkflowModel */
(function () {
  const model = window.WorkflowModel;
  const state = {
    panel: null,
    caseId: "",
    caseData: null,
    history: [],
    documents: [],
    documentsLatest: [],
    documentVersionGroups: [],
    comments: [],
    payments: [],
    paymentsSummary: null,
    selectedUnit: "all",
    busy: false,
  };

  const qs = (sel, root) => (root || document).querySelector(sel);

  function setPortalError(message) {
    const box = qs("#portalError");
    if (!box) return;
    if (!message) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.classList.remove("hidden");
    box.textContent = message;
  }

  function setPortalInfo(message) {
    const box = qs("#portalInfo");
    if (!box) return;
    if (!message) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.classList.remove("hidden");
    box.textContent = message;
  }

  function pickId(obj) {
    if (!obj || typeof obj !== "object") return "";
    return obj.id || obj.Id || "";
  }

  function pickStatus(obj) {
    if (!obj || typeof obj !== "object") return 0;
    const raw = obj.currentStatus ?? obj.CurrentStatus ?? 0;
    return typeof model.coerceStatus === "function" ? model.coerceStatus(raw) : Number(raw) || 0;
  }

  function pickPhase(obj) {
    if (!obj || typeof obj !== "object") return 0;
    const raw = obj.currentPhase ?? obj.CurrentPhase ?? 0;
    return typeof model.coercePhase === "function" ? model.coercePhase(raw) : Number(raw) || 0;
  }

  function pickCaseNumber(obj) {
    if (!obj || typeof obj !== "object") return "";
    return obj.caseNumber || obj.CaseNumber || "";
  }

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function casesPath(suffix) {
    return state.panel.casesBasePath() + suffix;
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
      module: "investment",
      host: "portalStageRollback",
      getCaseId: function () {
        return state.caseId;
      },
      getRole: getSessionRole,
      onSuccess: async function (msg) {
        setPortalInfo(msg);
        await refreshCase();
      },
      onError: setPortalError,
    });
  }

  function isInternalSession() {
    return model.isInternalRole(getSessionRole());
  }

  function readCaseId() {
    if (state.panel && state.panel.getCaseModule() === "guarantee") return "";
    if (state.panel && typeof state.panel.getInvestmentCaseId === "function") {
      return state.panel.getInvestmentCaseId() || "";
    }
    const input = qs("#currentCaseId");
    return input ? input.value.trim() : "";
  }

  function isCaseNotFoundError(error) {
    const msg = (error && error.message) || String(error || "");
    return /یافت نشد|not found|404/i.test(msg);
  }

  function readValue(id) {
    const node = qs("#" + id);
    return node ? node.value.trim() : "";
  }

  function readNumber(id) {
    const value = Number(readValue(id));
    return Number.isFinite(value) ? value : 0;
  }

  function todayIsoDate() {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, "0");
    const dd = String(today.getDate()).padStart(2, "0");
    return yyyy + "-" + mm + "-" + dd;
  }

  /** API expects yyyy-MM-dd for DateOnly (HTML date input or fallback to today). */
  function readPaymentDateIso(id) {
    const raw = readValue(id);
    if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) return raw;
    if (raw) throw new Error("تاریخ پرداخت باید به صورت YYYY-MM-DD باشد (از تقویم فیلد انتخاب کنید).");
    return todayIsoDate();
  }

  function buildRecordPaymentPayload() {
    const amount = readNumber("payAmount");
    if (!(amount > 0)) throw new Error("مبلغ قسط باید بزرگ‌تر از صفر باشد.");

    const transactionNumber = readValue("payTxn");
    if (!transactionNumber) throw new Error("شماره تراکنش الزامی است.");

    const method = readNumber("payMethod");
    if (!(method >= 1 && method <= 4)) throw new Error("روش پرداخت نامعتبر است (۱ انتقال، ۲ چک، ۳ نقد، ۴ سایر).");

    const status = readNumber("payStatus");
    if (!(status >= 1 && status <= 4)) throw new Error("وضعیت پرداخت نامعتبر است (۱ در انتظار، ۲ تأییدشده، …).");

    const receiptInput = qs('input[data-upload-type="10"]');
    const receiptDoc = documentForType(10);
    const receiptS3Key =
      (receiptInput && receiptInput.dataset.s3Key) ||
      (receiptDoc && (receiptDoc.s3Key || receiptDoc.S3Key)) ||
      null;

    return {
      amount,
      paymentDate: readPaymentDateIso("payDate"),
      transactionNumber,
      method,
      status,
      notes: null,
      receiptS3Key,
    };
  }

  function readChecked(id) {
    const node = qs("#" + id);
    return !!(node && node.checked);
  }

  function setFieldValue(id, value) {
    const node = qs("#" + id);
    if (!node || value == null) return;
    node.value = String(value);
  }

  function pickDataEntry1(obj) {
    if (!obj || typeof obj !== "object") return null;
    return obj.dataEntry1 || obj.DataEntry1 || obj.applicantProfile || obj.ApplicantProfile || null;
  }

  function pickDataEntry2(obj) {
    if (!obj || typeof obj !== "object") return null;
    return obj.dataEntry2 || obj.DataEntry2 || obj.attractionBasis || obj.AttractionBasis || null;
  }

  function pickFinancialWorksheet(obj) {
    if (!obj || typeof obj !== "object") return null;
    return obj.financialWorksheet || obj.FinancialWorksheet || null;
  }

  function pickValuations(obj) {
    if (!obj || typeof obj !== "object") return [];
    return obj.valuations || obj.Valuations || [];
  }

  function pickProp(obj, camel, pascal) {
    if (!obj) return "";
    const value = obj[camel] ?? obj[pascal];
    return value == null ? "" : value;
  }

  function pickCompany(obj) {
    if (!obj || typeof obj !== "object") return null;
    return obj.company || obj.Company || null;
  }

  function pickApplicant(obj) {
    if (!obj || typeof obj !== "object") return null;
    return obj.applicant || obj.Applicant || null;
  }

  function pickApplicantContact() {
    const applicant = pickApplicant(state.caseData);
    if (applicant) {
      return {
        fullName: pickProp(applicant, "fullName", "FullName"),
        email: pickProp(applicant, "email", "Email"),
        phone: pickProp(applicant, "phoneNumber", "PhoneNumber"),
      };
    }
    const de1 = pickDataEntry1(state.caseData);
    if (de1) {
      return {
        fullName: pickProp(de1, "representativeFullName", "RepresentativeFullName"),
        email: pickProp(de1, "contactEmail", "ContactEmail"),
        phone: "",
      };
    }
    const session = state.panel.getActiveSession();
    const user = session && session.raw && (session.raw.user || session.raw.User);
    if (user) {
      const first = user.firstName || user.FirstName || "";
      const last = user.lastName || user.LastName || "";
      return {
        fullName: (first + " " + last).trim(),
        email: user.email || user.Email || "",
        phone: user.phoneNumber || user.PhoneNumber || "",
      };
    }
    return { fullName: "", email: "", phone: "" };
  }

  function renderProfileSummary(card) {
    const company = pickCompany(state.caseData);
    const contact = pickApplicantContact();
    const wrap = el("div", "portal-profile-summary card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", "اطلاعات پروفایل (فقط نمایش)"));
    const rows = [
      ["نام شرکت", pickProp(company, "name", "Name")],
      ["شناسه ملی شرکت", pickProp(company, "nationalId", "NationalId")],
      ["تلفن شرکت", pickProp(company, "phoneNumber", "PhoneNumber")],
      ["نام و نام خانوادگی نماینده", contact.fullName],
      ["ایمیل", contact.email],
    ];
    rows.forEach(([label, value]) => {
      const row = el("div", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value || "—"));
      wrap.appendChild(row);
    });
    card.appendChild(wrap);
    return wrap;
  }

  function patchLocalDataEntry2(basis) {
    if (!state.caseData || typeof state.caseData !== "object") return;
    const entry = {
      investmentAttractionBasis: basis,
      InvestmentAttractionBasis: basis,
    };
    state.caseData = Object.assign({}, state.caseData, {
      dataEntry2: entry,
      DataEntry2: entry,
    });
  }

  function de2BasisFromCase() {
    const de2 = pickDataEntry2(state.caseData);
    if (!de2) return "";
    return String(pickProp(de2, "investmentAttractionBasis", "InvestmentAttractionBasis") || "").trim();
  }

  function hasSavedDataEntry2() {
    const formBasis = readValue("de2Basis");
    const savedBasis = de2BasisFromCase();
    if (savedBasis && formBasis && savedBasis === formBasis.trim()) return true;
    return !!savedBasis;
  }

  async function ensureDataEntry2Saved(caseId) {
    const basis = readValue("de2Basis");
    if (!basis) throw new Error("مبنای درخواست جذب سرمایه‌گذار الزامی است.");
    const savedBasis = de2BasisFromCase();
    if (savedBasis === basis.trim()) {
      return;
    }
    await state.panel.apiRequest({
      method: "PUT",
      path: casesPath("/" + caseId + "/data-entry2"),
      body: { investmentAttractionBasis: basis },
    });
    patchLocalDataEntry2(basis);
  }

  function de2RequiredDocumentsComplete() {
    const required = model.DATA_ENTRY_2_DOCUMENTS.filter((d) => d.required);
    return required.every((d) => documentForType(d.type));
  }

  function hasSavedDataEntry1() {
    const de1 = pickDataEntry1(state.caseData);
    if (!de1) return false;
    const amount = Number(pickProp(de1, "requestedAmount", "RequestedAmount"));
    const stage = Number(pickProp(de1, "businessStage", "BusinessStage"));
    return amount > 0 && (stage === 1 || stage === 2);
  }

  function populateFormsFromCase() {
    const de1 = pickDataEntry1(state.caseData);
    if (de1) {
      setFieldValue("de1Amount", pickProp(de1, "requestedAmount", "RequestedAmount"));
      setFieldValue("de1Stage", String(pickProp(de1, "businessStage", "BusinessStage") || ""));
    }

    const de2 = pickDataEntry2(state.caseData);
    if (de2) {
      setFieldValue(
        "de2Basis",
        pickProp(de2, "investmentAttractionBasis", "InvestmentAttractionBasis")
      );
    }
  }

  async function uploadDocument(caseId, documentType, file) {
    const mimeType = file.type || "application/octet-stream";
    const presignRes = await state.panel.apiRequest({
      method: "POST",
      path: casesPath("/" + caseId + "/documents/presign"),
      body: {
        documentType: Number(documentType),
        fileName: file.name,
        mimeType,
        fileSize: file.size,
      },
    });
    const presign = unwrap(presignRes.body) || {};
    const uploadUrl = presign.url || presign.Url;
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
      path: casesPath("/" + caseId + "/documents/confirm?s3Key=" + encodeURIComponent(s3Key) + "&originalFileName=" + encodeURIComponent(file.name)),
     body: null,
     json: false,
   });
    return s3Key;
  }

  async function downloadDocument(documentId) {
    const caseId = readCaseId();
    if (!caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const session = state.panel.getActiveSession();
    const headers = {};
    if (session && session.accessToken) {
      headers.Authorization = "Bearer " + session.accessToken;
    }
    const url = state.panel.makeUrl(
      casesPath("/" + caseId + "/documents/" + encodeURIComponent(documentId) + "/download")
    );
    const res = await fetch(url, { method: "GET", headers });
    if (!res.ok) throw new Error("دانلود سند با کد " + res.status + " ناموفق بود.");
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

  async function uploadContract(caseId, kind, file) {
    const docType = kind === "signed" ? 9 : 7;
    return uploadDocument(caseId, docType, file);
  }

  async function postComment(caseId, phase, message, isInternal) {
    if (!message) throw new Error("متن نظر الزامی است.");
    await state.panel.apiRequest({
      method: "POST",
      path: casesPath("/" + caseId + "/comments"),
      body: {
        phase,
        message,
        isInternal: !!isInternal,
        parentId: null,
      },
    });
  }

  async function fetchInvestmentCaseData(caseId) {
    const session = state.panel.getActiveSession();
    if (!session) throw new Error("ابتدا وارد سامانه شوید.");

    let caseRes;
    try {
      caseRes = await state.panel.apiRequest({ method: "GET", path: casesPath("/" + caseId) });
    } catch (error) {
      if (isCaseNotFoundError(error)) {
        if (typeof state.panel.clearInvestmentCaseId === "function") state.panel.clearInvestmentCaseId();
        state.caseId = "";
        state.caseData = null;
        state.history = [];
        state.documents = [];
        state.documentsLatest = [];
        state.documentVersionGroups = [];
        state.comments = [];
        state.payments = [];
        state.paymentsSummary = null;
        render();
        return false;
      }
      throw error;
    }
    state.caseData = unwrap(caseRes.body);

    const historyRes = await state.panel.apiRequest({ method: "GET", path: casesPath("/" + caseId + "/history") });
    state.history = unwrap(historyRes.body) || [];

    try {
      const docsRes = await state.panel.apiRequest({ method: "GET", path: casesPath("/" + caseId + "/documents") });
      state.documents = unwrap(docsRes.body) || [];
    } catch {
      state.documents = [];
    }

    try {
      const latestRes = await state.panel.apiRequest({
        method: "GET",
        path: casesPath("/" + caseId + "/documents/latest"),
      });
      state.documentsLatest = unwrap(latestRes.body) || [];
    } catch {
      state.documentsLatest = [];
    }

    const caseStatus = pickStatus(state.caseData);
    const role = getSessionRole();
    let versionScope = null;
    if (isInternalSession() && (caseStatus === 3 || caseStatus === 5)) {
      versionScope = "data-entry";
    } else if (caseStatus === 9 && role === "Applicant") {
      versionScope = "preliminary";
    } else if (isInternalSession() && caseStatus >= 8 && caseStatus <= 12) {
      versionScope = "contracts";
    } else if (caseStatus === 8) {
      versionScope = "preliminary";
    }

    if (versionScope) {
      try {
        const groupsRes = await state.panel.apiRequest({
          method: "GET",
          path:
            casesPath("/" + caseId + "/documents/version-groups?scope=" + encodeURIComponent(versionScope)),
        });
        state.documentVersionGroups = unwrap(groupsRes.body) || [];
      } catch {
        state.documentVersionGroups = [];
      }
    } else {
      state.documentVersionGroups = [];
    }

    try {
      const includeInternal = isInternalSession() ? "true" : "false";
      const commentsRes = await state.panel.apiRequest({
        method: "GET",
        path: casesPath("/" + caseId + "/comments?includeInternal=" + includeInternal),
      });
      const payload = unwrap(commentsRes.body);
      state.comments = Array.isArray(payload) ? payload : payload ? [payload] : [];
    } catch {
      state.comments = [];
    }

    await refreshPayments(caseId);
    return true;
  }

  async function refreshCase(previousStatus) {
    const caseId = readCaseId();
    state.caseId = caseId;
    if (!caseId) {
      state.caseData = null;
      state.history = [];
      state.documents = [];
      state.documentsLatest = [];
      state.documentVersionGroups = [];
      state.comments = [];
      state.payments = [];
      state.paymentsSummary = null;
      render();
      await refreshStageRollback();
      return;
    }

    const applyRefresh = async function () {
      const loaded = await fetchInvestmentCaseData(caseId);
      if (loaded === false) return;
      state.panel.setCurrentCaseId(caseId);
      render();
    };

    if (previousStatus != null && window.PortalCaseRefresh) {
      await window.PortalCaseRefresh.refreshUntilChanged(
        applyRefresh,
        function () {
          return pickStatus(state.caseData);
        },
        previousStatus
      );
    } else {
      await applyRefresh();
    }
    await refreshStageRollback();
  }

  async function refreshPayments(caseId) {
    const status = pickStatus(state.caseData);
    const allowPayments = isInternalSession() || status === 16 || status === 19;
    if (!allowPayments) {
      state.payments = [];
      state.paymentsSummary = null;
      return;
    }
    try {
      const res = await state.panel.apiRequest({ method: "GET", path: casesPath("/" + caseId + "/payments") });
      const payload = unwrap(res.body);
      state.payments = payload.payments || payload.Payments || [];
      state.paymentsSummary = payload.summary || payload.Summary || null;
    } catch {
      state.payments = [];
      state.paymentsSummary = null;
    }
  }

  function formatMoney(value) {
    const amount = Number(value);
    if (!Number.isFinite(amount)) return "—";
    return amount.toLocaleString("fa-IR") + " ریال";
  }

  function paymentStatusLabel(status) {
    const labels = {
      1: "در انتظار تأیید",
      2: "تأیید شده",
      3: "لغو شده",
      4: "ناموفق",
    };
    return labels[Number(status)] || String(status);
  }

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  function field(label, id, type, value) {
    const row = el("div", "formrow");
    row.appendChild(el("label", "", label));
    const input = document.createElement(type === "textarea" ? "textarea" : "input");
    input.id = id;
    if (type === "textarea") input.className = "portal-textarea";
    else input.type = type || "text";
    if (value != null && value !== "") input.value = value;
    row.appendChild(input);
    return row;
  }

  function selectField(label, id, options, selectedValue) {
    const row = el("motion", "formrow");
    row.appendChild(el("label", "", label));
    const select = document.createElement("select");
    select.id = id;
    select.className = "portal-select";
    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = "یک گزینه را انتخاب کنید";
    select.appendChild(placeholder);
    options.forEach((opt) => {
      const option = document.createElement("option");
      option.value = String(opt.value);
      option.textContent = opt.label;
      select.appendChild(option);
    });
    if (selectedValue != null && selectedValue !== "") select.value = String(selectedValue);
    row.appendChild(select);
    return row;
  }

  function actionRow(buttons) {
    const row = el("div", "row");
    buttons.forEach((btn) => row.appendChild(btn));
    return row;
  }

  function createButton(label, className, action, extra) {
    const btn = el("button", "btn " + (className || ""), label);
    btn.type = "button";
    btn.dataset.action = action;
    if (extra) {
      Object.keys(extra).forEach((key) => {
        btn.dataset[key] = extra[key];
      });
    }
    return btn;
  }

  let uploadFieldCounter = 0;

  function nextUploadFieldId(prefix) {
    uploadFieldCounter += 1;
    return (prefix || "upload") + "-" + uploadFieldCounter;
  }

  const EXTRA_DOCUMENT_LABELS = {
    7: "پیش‌قرارداد",
    8: "پیش‌نویس قرارداد",
    9: "قرارداد امضاشده",
    10: "رسید پرداخت",
    11: "بیزینس پلن",
    12: "اسناد معرفی شرکت",
    13: "لیست بیمه کارکنان",
    14: "تراز آزمایشی",
    15: "مجوز فعالیت",
    16: "جواز کسب و پروانه‌ها",
    17: "اعتبارسنجی مدیران",
    18: "صورتجلسه هیئت‌مدیره",
    19: "برنامه جذب سرمایه",
  };

  function documentForType(documentType) {
    const t = Number(documentType);
    const fromLatest = state.documentsLatest.find(
      (d) => Number(d.documentType ?? d.DocumentType) === t
    );
    if (fromLatest) return fromLatest;
    const matches = state.documents.filter((d) => Number(d.documentType ?? d.DocumentType) === t);
    if (!matches.length) return null;
    return matches.reduce((best, cur) => {
      const bv = Number(best.version ?? best.Version ?? 0);
      const cv = Number(cur.version ?? cur.Version ?? 0);
      return cv > bv ? cur : best;
    });
  }

  function documentMetaForType(documentType) {
    const t = Number(documentType);
    return (
      model.DATA_ENTRY_1_DOCUMENTS.find((d) => d.type === t) ||
      model.DATA_ENTRY_2_DOCUMENTS.find((d) => d.type === t) ||
      null
    );
  }

  function documentTypeLabel(documentType) {
    const meta = documentMetaForType(documentType);
    if (meta) return meta.label;
    return EXTRA_DOCUMENT_LABELS[Number(documentType)] || "نوع " + documentType;
  }

  function businessStageLabel(value) {
    const n = Number(value);
    const stage = model.BUSINESS_STAGES.find((s) => s.value === n);
    return stage ? stage.label : value ? String(value) : "—";
  }

  function renderReadOnlySummary(card, title, rows) {
    const wrap = el("div", "portal-profile-summary card portal-card portal-card--nested");
    wrap.appendChild(el("motion", "card__title", title));
    rows.forEach(([label, value]) => {
      const row = el("motion", "portal-profile-summary__row");
      row.appendChild(el("span", "portal-profile-summary__label muted", label));
      row.appendChild(el("span", "portal-profile-summary__value", value || "—"));
      wrap.appendChild(row);
    });
    card.appendChild(wrap);
  }

  function renderApplicantDocumentsReadOnly(card, title, documentDefs) {
    const wrap = el("motion", "card portal-card portal-card--nested");
    wrap.appendChild(el("motion", "card__title", title || "مدارک بارگذاری‌شده توسط متقاضی"));
    wrap.appendChild(
      el("motion", "muted", "فقط مشاهده — کارشناس مدرک جدید بارگذاری نمی‌کند.")
    );
    const requiredTypes = documentDefs.filter((d) => d.required);
    const uploadedRequired = requiredTypes.filter((d) => documentForType(d.type)).length;
    if (requiredTypes.length) {
      wrap.appendChild(
        el("motion", "muted", "مدارک ضروری متقاضی: " + uploadedRequired + " از " + requiredTypes.length)
      );
    }
    documentDefs.forEach((doc) => {
      const existing = documentForType(doc.type);
      const fileName = existing && (existing.fileName || existing.FileName);
      const row = el("motion", "portal-doc-readonly__row");
      row.appendChild(el("motion", "portal-doc-readonly__label", doc.label));
      const statusText = existing
        ? "✓ " + (fileName || "بارگذاری شده")
        : doc.required
          ? "✗ بارگذاری نشده (ضروری)"
          : "— بارگذاری نشده (اختیاری)";
      row.appendChild(
        el(
          "motion",
          existing ? "portal-doc-readonly__status is-ok" : "portal-doc-readonly__status",
          statusText
        )
      );
      if (existing) {
        const id = existing.id || existing.Id;
        if (id) {
          row.appendChild(createButton("دانلود", "btn--sm", "download-document", { documentId: id }));
        }
      }
      wrap.appendChild(row);
    });
    card.appendChild(wrap);
  }

  function formatUploadedAt(doc) {
    const raw = doc.uploadedAt || doc.UploadedAt;
    if (!raw) return "";
    try {
      return new Date(raw).toLocaleString("fa-IR");
    } catch {
      return String(raw);
    }
  }

  function normalizeVersionGroup(group) {
    const type = Number(group.documentType ?? group.DocumentType);
    const versions = group.versions || group.Versions || [];
    return { type, versions };
  }

  function renderDocumentVersionGroups(parent, title) {
    if (!state.documentVersionGroups.length) return;
    const wrap = el("div", "card portal-card portal-card--nested portal-doc-versions");
    wrap.appendChild(el("div", "card__title", title || "تاریخچه نسخه‌های مدارک"));
    state.documentVersionGroups.forEach((group) => {
      const { type, versions } = normalizeVersionGroup(group);
      if (!versions.length) return;
      const block = el("div", "portal-doc-versions__type");
      block.appendChild(el("div", "portal-doc-versions__type-title", documentTypeLabel(type)));
      const list = el("div", "portal-doc-versions__list");
      versions.forEach((doc) => {
        const id = doc.id || doc.Id;
        const ver = doc.version ?? doc.Version ?? 1;
        const name = doc.fileName || doc.FileName || "فایل";
        const row = el("div", "portal-doc-versions__row");
        row.appendChild(
          el(
            "div",
            "portal-doc-versions__meta",
            "نسخه " + ver + " — " + name + (formatUploadedAt(doc) ? " · " + formatUploadedAt(doc) : "")
          )
        );
        row.appendChild(
          createButton("دانلود", "btn--sm", "download-document", { documentId: id })
        );
        list.appendChild(row);
      });
      block.appendChild(list);
      wrap.appendChild(block);
    });
    parent.appendChild(wrap);
  }

  function appendFileUploadRow(parent, options) {
    const row = el("div", "portal-upload-row");
    const inputId = options.id || nextUploadFieldId(options.idPrefix || "file");

    if (options.title || options.hint) {
      const meta = el("div", "portal-upload-row__meta");
      if (options.title) meta.appendChild(el("div", "portal-upload-row__title", options.title));
      if (options.hint) meta.appendChild(el("div", "muted", options.hint));
      row.appendChild(meta);
    }

    const control = el("div", "portal-upload-row__control");
    const input = document.createElement("input");
    input.type = "file";
    input.id = inputId;
    input.className = "portal-file-input";
    input.accept = options.accept || ".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg";

    if (options.uploadType != null) input.dataset.uploadType = String(options.uploadType);
    if (options.contractKind) input.dataset.contractKind = options.contractKind;
    if (options.contractCommentField) input.dataset.contractCommentField = options.contractCommentField;
    if (options.contractCommentPhase) input.dataset.contractCommentPhase = String(options.contractCommentPhase);

    const picker = document.createElement("label");
    picker.className = "portal-file-btn";
    picker.htmlFor = inputId;
    picker.textContent = options.buttonText || "انتخاب و بارگذاری فایل";

    const status = el("span", "portal-upload-row__status muted");
    if (options.uploadedLabel) {
      status.textContent = options.uploadedLabel;
      status.classList.add("is-uploaded");
      row.classList.add("portal-upload-row--done");
    }
    input.addEventListener("change", () => {
      const file = input.files && input.files[0];
      if (file) {
        status.textContent = "در حال بارگذاری: " + file.name;
        status.classList.remove("is-uploaded");
      } else if (!options.uploadedLabel) {
        status.textContent = "";
      }
    });

    control.appendChild(input);
    control.appendChild(picker);
    control.appendChild(status);
    row.appendChild(control);
    parent.appendChild(row);
    return input;
  }

  function renderStepper() {
    const root = qs("#portalStepper");
    if (!root) return;
    root.innerHTML = "";
    const current = pickStatus(state.caseData);
    const track = el("div", "portal-stepper__track");

    const currentIndex = model.getStepOrderIndex(current);

    model.getStepperSteps().forEach((step, index) => {
      const item = el("div", "portal-stepper__item");
      if (currentIndex >= 0) {
        if (index < currentIndex) item.classList.add("is-done");
        if (index === currentIndex) item.classList.add("is-current");
        if (index > currentIndex) item.classList.add("is-upcoming");
      } else if (model.compareStepOrder(step.status, current) < 0) {
        item.classList.add("is-done");
      } else if (step.status === current) {
        item.classList.add("is-current");
      } else {
        item.classList.add("is-upcoming");
      }

      item.appendChild(el("div", "portal-stepper__index", String(step.status)));
      item.appendChild(el("div", "portal-stepper__title", step.title));
      item.appendChild(el("div", "portal-stepper__unit", model.getUnit(step.unit)?.label || step.unit));
      track.appendChild(item);
    });

    root.appendChild(track);
  }

  function renderUnitTabs() {
    const root = qs("#portalUnitTabs");
    if (!root) return;
    root.innerHTML = "";
    const role = getSessionRole();

    model.UNITS.forEach((unit) => {
      const btn = el("button", "portal-unit-tab", unit.label);
      btn.type = "button";
      if (state.selectedUnit === unit.id) btn.classList.add("is-active");
      if (unit.id !== "all" && role && !model.roleMatchesUnit(role, unit.id)) btn.classList.add("is-muted");
      btn.dataset.unit = unit.id;
      root.appendChild(btn);
    });
  }

  function commentsForPhase(phase) {
    return state.comments.filter((comment) => Number(comment.phase ?? comment.Phase) === Number(phase));
  }

  function renderCommentsHistory(card, phase, title, options) {
    options = options || {};
    const block = el("div", "portal-thread");
    block.dataset.commentPhase = String(phase);
    block.appendChild(el("div", "portal-thread__title", title || options.title || "تاریخچه نظرات"));

    const list = el("div", "portal-thread__list");
    let items = commentsForPhase(phase);
    if (options.revisionOnly) {
      items = items.filter((c) => c.isRevisionRequest || c.IsRevisionRequest);
    }
    if (!items.length) {
      list.appendChild(el("div", "muted", options.emptyText || "هنوز نظری ثبت نشده است."));
      block.appendChild(list);
    } else if (window.UIComponents && UIComponents.renderCommentThreadList) {
      block.appendChild(
        UIComponents.renderCommentThreadList(items, {
          module: "investment",
          history: state.history,
          allComments: state.comments,
        })
      );
    } else {
      items.forEach((comment) => {
        const row = el("div", "portal-thread__item");
        const meta = el("div", "portal-thread__meta");
        const role = comment.senderRole || comment.SenderRole || "";
        const revision = comment.isRevisionRequest || comment.IsRevisionRequest;
        meta.textContent = [role, revision ? "درخواست اصلاح" : "نظر"].filter(Boolean).join(" · ");
        const body = el("div", "portal-thread__message", comment.message || comment.Message || "");
        row.appendChild(meta);
        row.appendChild(body);
        list.appendChild(row);
      });
      block.appendChild(list);
    }
    card.appendChild(block);
  }

  function renderPublicCommentForm(card, phase, prefix) {
    const block = el("div", "portal-thread");
    block.appendChild(el("div", "portal-thread__title", "ثبت نظر عمومی"));
    block.appendChild(field("متن نظر", prefix + "Comment", "textarea"));
    block.appendChild(
      actionRow([
        createButton("ثبت نظر", "btn--primary", "post-comment", {
          commentPhase: String(phase),
          commentPrefix: prefix,
        }),
      ])
    );
    card.appendChild(block);
  }

  function renderUploads(card, prefix, title, documentDefs) {
    const defs = documentDefs || model.APPLICANT_DOCUMENTS;
    const wrap = el("div", "card portal-card portal-card--nested");
    wrap.appendChild(el("div", "card__title", title || "بارگذاری مدارک"));
    wrap.appendChild(el("div", "muted", "پس از انتخاب فایل، بارگذاری به‌صورت خودکار انجام می‌شود."));
    const requiredTypes = defs.filter((d) => d.required).map((d) => d.type);
    const uploadedRequired = requiredTypes.filter((t) => documentForType(t)).length;
    if (requiredTypes.length) {
      wrap.appendChild(
        el("motion", "muted", "مدارک ضروری: " + uploadedRequired + " از " + requiredTypes.length)
      );
    }
    defs.forEach((doc) => {
      const existing = documentForType(doc.type);
      const fileName = existing && (existing.fileName || existing.FileName);
      appendFileUploadRow(wrap, {
        id: prefix + "-doc-" + doc.type,
        title: doc.label,
        hint: doc.hint,
        uploadType: doc.type,
        idPrefix: prefix,
        uploadedLabel: existing
          ? "✓ بارگذاری شده" + (fileName ? ": " + fileName : "")
          : null,
      });
    });
    card.appendChild(wrap);
  }

  function renderPaymentsSection(card) {
    const wrap = el("motion", "card portal-card portal-card--nested");
    wrap.appendChild(el("motion", "card__title", "پرداخت‌های ثبت‌شده"));

    const summary = state.paymentsSummary;
    if (summary) {
      const approved = summary.approvedAmount ?? summary.ApprovedAmount;
      const confirmed = summary.totalConfirmed ?? summary.TotalConfirmed ?? 0;
      const remaining = summary.remainingToComplete ?? summary.RemainingToComplete ?? 0;
      const recorded = summary.totalRecorded ?? summary.TotalRecorded ?? 0;
      if (approved != null && Number(approved) > 0) {
        wrap.appendChild(
          el(
            "motion",
            "muted",
            "مبلغ مصوب: " +
              formatMoney(approved) +
              " — ثبت‌شده (غیرلغو): " +
              formatMoney(recorded) +
              " — تأییدشده: " +
              formatMoney(confirmed) +
              " — باقیمانده تا تکمیل: " +
              formatMoney(remaining)
          )
        );
      }
    }

    const list = el("motion", "portal-payments-list");
    const items = state.payments || [];
    if (!items.length) {
      list.appendChild(el("motion", "muted", "هنوز پرداختی ثبت نشده است."));
    } else {
      items.forEach((payment) => {
        const id = payment.id || payment.Id;
        const amount = payment.amount ?? payment.Amount;
        const status = payment.status ?? payment.Status;
        const txn = payment.transactionNumber || payment.TransactionNumber || "";
        const date = payment.paymentDate || payment.PaymentDate || "";

        const row = el("motion", "portal-payments-list__item");
        row.appendChild(
          el(
            "motion",
            "portal-payments-list__meta",
            formatMoney(amount) +
              " · " +
              date +
              " · " +
              txn +
              " · " +
              paymentStatusLabel(status)
          )
        );

        if (Number(status) === 1) {
          const actions = el("motion", "portal-payments-list__actions");
          actions.appendChild(
            createButton("تأیید", "btn--primary btn--sm", "confirm-payment", { paymentId: id })
          );
          actions.appendChild(
            createButton("لغو", "btn--warn btn--sm", "cancel-payment", { paymentId: id })
          );
          row.appendChild(actions);
        }

        list.appendChild(row);
      });
    }

    wrap.appendChild(list);
    card.appendChild(wrap);
  }

  function valuationTypeLabel(type) {
    const n = Number(type);
    if (n === 1) return "ارزش‌گذاری اولیه";
    if (n === 2) return "ارزش‌گذاری ثانویه";
    return String(type || "—");
  }

  function formatCaseDate(iso) {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleString("fa-IR");
    } catch {
      return String(iso);
    }
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
      const type = Number(doc.documentType ?? doc.DocumentType);
      if (!byType.has(type)) byType.set(type, []);
      byType.get(type).push(doc);
    });

    Array.from(byType.keys())
      .sort((a, b) => a - b)
      .forEach((type) => {
        const versions = byType
          .get(type)
          .slice()
          .sort((a, b) => Number(b.version ?? b.Version ?? 0) - Number(a.version ?? a.Version ?? 0));
        const block = el("div", "portal-doc-archive__type");
        block.appendChild(el("div", "portal-doc-archive__type-title", documentTypeLabel(type)));
        versions.forEach((doc) => {
          const id = doc.id || doc.Id;
          const ver = doc.version ?? doc.Version ?? 1;
          const name = doc.fileName || doc.FileName || "فایل";
          const size = doc.fileSize || doc.FileSize;
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
          if (id) row.appendChild(createButton("دانلود", "btn--sm", "download-document", { documentId: id }));
          block.appendChild(row);
        });
        wrap.appendChild(block);
      });
    parent.appendChild(wrap);
  }

  function renderAllCommentsHistory(parent) {
    const items = state.comments
      .slice()
      .sort(
        (a, b) =>
          new Date(a.createdAt || a.CreatedAt || 0).getTime() -
          new Date(b.createdAt || b.CreatedAt || 0).getTime()
      );
    if (!items.length) {
      parent.appendChild(el("div", "muted", "نظری ثبت نشده است."));
      return;
    }
    if (window.UIComponents && UIComponents.renderCommentThreadList) {
      parent.appendChild(
        UIComponents.renderCommentThreadList(items, {
          module: "investment",
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
      const phase = Number(comment.phase ?? comment.Phase);
      const phaseTitle = model.PHASES[phase] || "فاز " + phase;
      const role = comment.senderRole || comment.SenderRole || "";
      const revision = comment.isRevisionRequest || comment.IsRevisionRequest;
      const internal = comment.isInternal || comment.IsInternal;
      const parts = [phaseTitle, role];
      if (revision) parts.push("درخواست اصلاح");
      else if (internal) parts.push("داخلی");
      const when = formatUploadedAt(comment);
      if (when) parts.push(when);
      row.appendChild(el("div", "portal-thread__meta muted", parts.join(" · ")));
      row.appendChild(el("div", "portal-thread__message", comment.message || comment.Message || "—"));
      list.appendChild(row);
    });
    block.appendChild(list);
    parent.appendChild(block);
  }

  function renderFullWorkflowHistory(parent) {
    if (!state.history.length) {
      parent.appendChild(el("div", "muted", "تاریخچه‌ای ثبت نشده است."));
      return;
    }
    const list = el("div", "portal-history");
    state.history.forEach((item) => {
      const row = el("div", "portal-history__item");
      const fromStatus = item.fromStatus ?? item.FromStatus ?? "";
      const toStatus = item.toStatus ?? item.ToStatus ?? item.status ?? item.Status ?? "";
      const action = item.action || item.Action || "";
      const actor = item.actorRole || item.ActorRole || "";
      const when = formatCaseDate(item.createdAt || item.CreatedAt);
      row.textContent = [when, action, fromStatus && toStatus ? fromStatus + " → " + toStatus : toStatus, actor]
        .filter(Boolean)
        .join(" · ");
      list.appendChild(row);
    });
    parent.appendChild(list);
  }

  function renderCompletedCaseDossier(card) {
    const details = document.createElement("details");
    details.className = "portal-dossier card portal-card portal-card--nested";
    details.open = true;

    const summary = document.createElement("summary");
    summary.className = "portal-dossier__summary card__title";
    summary.textContent = "پرونده کامل — خلاصه اطلاعات ثبت‌شده";
    details.appendChild(summary);

    const body = el("div", "portal-dossier__body");
    body.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        "تمام اطلاعات فرم‌ها، ارزش‌گذاری، کاربرگ مالی، قراردادها، پرداخت‌ها و مدارک این پرونده."
      )
    );

    const metaRows = [
      ["تاریخ ایجاد", formatCaseDate(pickProp(state.caseData, "createdAt", "CreatedAt"))],
      ["تاریخ تکمیل", formatCaseDate(pickProp(state.caseData, "completedAt", "CompletedAt"))],
      ["نوع متقاضی", pickProp(state.caseData, "applicantType", "ApplicantType") || "—"],
    ];
    if (isInternalSession()) {
      metaRows.push(
        ["متقاضی", pickProp(state.caseData, "applicantFullName", "ApplicantFullName") || "—"],
        ["موبایل متقاضی", pickProp(state.caseData, "applicantPhoneNumber", "ApplicantPhoneNumber") || "—"]
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

    const de1 = pickDataEntry1(state.caseData);
    if (de1) {
      renderReadOnlySummary(body, "فرم اولیه", [
        ["مرحله کسب‌وکار", businessStageLabel(pickProp(de1, "businessStage", "BusinessStage"))],
        ["سرمایه مورد نیاز (ریال)", formatMoney(pickProp(de1, "requestedAmount", "RequestedAmount"))],
        ["نام نماینده", pickProp(de1, "representativeFullName", "RepresentativeFullName")],
        ["ایمیل", pickProp(de1, "contactEmail", "ContactEmail")],
      ]);
      renderApplicantDocumentsReadOnly(body, "مدارک فرم اولیه", model.DATA_ENTRY_1_DOCUMENTS);
    }

    const de2 = pickDataEntry2(state.caseData);
    if (de2) {
      renderReadOnlySummary(body, "فرم تکمیلی", [
        ["مبنای درخواست جذب سرمایه‌گذاری", pickProp(de2, "investmentAttractionBasis", "InvestmentAttractionBasis")],
      ]);
      renderApplicantDocumentsReadOnly(body, "مدارک فرم تکمیلی", model.DATA_ENTRY_2_DOCUMENTS);
    }

    const valuations = pickValuations(state.caseData);
    const valWrap = el("div", "card portal-card portal-card--nested");
    valWrap.appendChild(el("div", "card__title", "ارزش‌گذاری‌ها"));
    if (!valuations.length) {
      valWrap.appendChild(el("div", "muted", "ارزش‌گذاری ثبت نشده است."));
    } else {
      valuations.forEach((valuation) => {
        const row = el("div", "portal-profile-summary__row");
        const label =
          valuationTypeLabel(pick(valuation, "type", "Type")) +
          " · " +
          formatCaseDate(pick(valuation, "createdAt", "CreatedAt"));
        row.appendChild(el("span", "portal-profile-summary__label muted", label));
        const value =
          formatMoney(pick(valuation, "amount", "Amount")) +
          (pick(valuation, "notes", "Notes") ? " — " + pick(valuation, "notes", "Notes") : "");
        row.appendChild(el("span", "portal-profile-summary__value", value));
        valWrap.appendChild(row);
      });
    }
    body.appendChild(valWrap);

    const worksheet = pickFinancialWorksheet(state.caseData);
    const wsWrap = el("div", "card portal-card portal-card--nested");
    wsWrap.appendChild(el("div", "card__title", "کاربرگ مالی"));
    if (!worksheet) {
      wsWrap.appendChild(el("div", "muted", "کاربرگ مالی ثبت نشده است."));
    } else {
      [
        ["نام بانک", pick(worksheet, "bankName", "BankName")],
        ["شبا", pick(worksheet, "iban", "Iban")],
        ["مبلغ مصوب (ریال)", formatMoney(pick(worksheet, "approvedAmount", "ApprovedAmount"))],
        ["برنامه پرداخت", pick(worksheet, "paymentSchedule", "PaymentSchedule")],
        ["یادداشت", pick(worksheet, "notes", "Notes")],
      ].forEach(([label, value]) => {
        const row = el("div", "portal-profile-summary__row");
        row.appendChild(el("span", "portal-profile-summary__label muted", label));
        row.appendChild(el("span", "portal-profile-summary__value", value || "—"));
        wsWrap.appendChild(row);
      });
    }
    body.appendChild(wsWrap);

    const contractWrap = el("div", "card portal-card portal-card--nested");
    contractWrap.appendChild(el("div", "card__title", "قراردادها"));
    const contractTypes = [7, 8, 9];
    const hasContracts = contractTypes.some((t) => documentForType(t));
    if (!hasContracts) {
      contractWrap.appendChild(el("div", "muted", "قراردادی بارگذاری نشده است."));
    } else {
      contractTypes.forEach((type) => {
        const doc = documentForType(type);
        if (!doc) return;
        const row = el("div", "portal-doc-readonly__row");
        row.appendChild(el("div", "portal-doc-readonly__label", documentTypeLabel(type)));
        const id = doc.id || doc.Id;
        const name = doc.fileName || doc.FileName || "فایل";
        row.appendChild(el("div", "portal-doc-readonly__status is-ok", "✓ " + name));
        if (id) row.appendChild(createButton("دانلود", "btn--sm", "download-document", { documentId: id }));
        contractWrap.appendChild(row);
      });
    }
    body.appendChild(contractWrap);

    renderPaymentsSection(body);

    const docsWrap = el("div", "card portal-card portal-card--nested");
    docsWrap.appendChild(el("div", "card__title", "همه مدارک و پیوست‌ها"));
    renderAllDocumentsArchive(docsWrap);
    body.appendChild(docsWrap);

    const commentsWrap = el("div", "card portal-card portal-card--nested");
    commentsWrap.appendChild(el("div", "card__title", "تاریخچه نظرات و درخواست‌های اصلاح"));
    renderAllCommentsHistory(commentsWrap);
    body.appendChild(commentsWrap);

    const historyWrap = el("div", "card portal-card portal-card--nested");
    historyWrap.appendChild(el("div", "card__title", "تاریخچه گردش کار"));
    renderFullWorkflowHistory(historyWrap);
    body.appendChild(historyWrap);

    details.appendChild(body);
    card.appendChild(details);
  }

  function pick(obj, ...keys) {
    if (!obj) return "";
    for (const k of keys) {
      if (obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return "";
  }

  function renderDocumentsList(host) {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "اسناد بارگذاری‌شده"));
    const source = state.documentsLatest.length ? state.documentsLatest : state.documents;
    if (!source.length) {
      card.appendChild(el("div", "muted", "هنوز سندی ثبت نشده است."));
    } else {
      const list = el("div", "portal-doc-list");
      source.forEach((doc) => {
        const item = el("div", "portal-doc-list__item portal-doc-list__item--row");
        const type = Number(doc.documentType ?? doc.DocumentType);
        const name = doc.fileName || doc.FileName || "فایل";
        const ver = doc.version ?? doc.Version;
        const meta = el("div", "portal-doc-list__meta");
        meta.textContent =
          documentTypeLabel(type) +
          " — " +
          name +
          (ver != null ? " (نسخه " + ver + ")" : "") +
          (formatUploadedAt(doc) ? " · " + formatUploadedAt(doc) : "");
        item.appendChild(meta);
        const id = doc.id || doc.Id;
        if (id) {
          item.appendChild(
            createButton("دانلود", "btn--sm", "download-document", { documentId: id })
          );
        }
        list.appendChild(item);
      });
      card.appendChild(list);
    }
    host.appendChild(card);
  }

  function renderHistory(host) {
    const card = el("div", "card portal-card");
    card.appendChild(el("div", "card__title", "تاریخچه اخیر"));
    if (!state.history.length) {
      card.appendChild(el("div", "muted", "تاریخچه‌ای ثبت نشده است."));
    } else {
      const list = el("div", "portal-history");
      state.history.slice(0, 12).forEach((item) => {
        const row = el("div", "portal-history__item");
        const status = item.toStatus || item.ToStatus || item.status || item.Status || "";
        const action = item.action || item.Action || "";
        row.textContent = [action, status].filter(Boolean).join(" → ");
        list.appendChild(row);
      });
      card.appendChild(list);
    }
    host.appendChild(card);
  }

  function renderDataEntryStage(card, prefix, phase, isReview, caseStatus) {
    if (!isReview) {
      if (prefix === "de1") {
        const de1 = pickDataEntry1(state.caseData);
        card.appendChild(
          el(
            "div",
            "portal-stage__hint muted",
            "نام و نام خانوادگی و ایمیل از پروفایل کاربر؛ نام شرکت و تلفن از پروفایل شرکت خوانده می‌شود."
          )
        );
        renderProfileSummary(card);
        card.appendChild(
          selectField(
            "مرحله کسب‌وکار",
            "de1Stage",
            model.BUSINESS_STAGES,
            pickProp(de1, "businessStage", "BusinessStage")
          )
        );
        card.appendChild(
          field(
            "سرمایه مورد نیاز (ریال)",
            "de1Amount",
            "number",
            pickProp(de1, "requestedAmount", "RequestedAmount")
          )
        );
        const submitLabel =
          caseStatus === 1 ? "ثبت و ادامه به فرم اولیه" : "ارسال برای بررسی کارشناس";
        card.appendChild(actionRow([
          createButton("ذخیره فرم اولیه", "btn--primary", "save-de1"),
          createButton(submitLabel, caseStatus === 2 ? "btn--primary" : "", "submit-de1"),
        ]));
        const hint =
          caseStatus === 1
            ? "پیش‌نویس: فرم را ذخیره کنید، پیچ‌دک را بارگذاری کنید، سپس «ثبت و ادامه»."
            : "پس از ذخیره و بارگذاری پیچ‌دک (ضروری)، «ارسال برای بررسی کارشناس» را بزنید.";
        card.appendChild(el("motion", "muted", hint));
        renderUploads(card, "de1", "مدارک فرم اولیه", model.DATA_ENTRY_1_DOCUMENTS);
      } else {
        card.appendChild(
          el(
            "div",
            "portal-stage__hint muted",
            "نام شرکت، شناسه ملی، نوع شرکت و تماس نماینده از پروفایل شرکت/کاربر خوانده می‌شود."
          )
        );
        card.appendChild(
          field(
            "مبنای درخواست جذب سرمایه‌گذار",
            "de2Basis",
            "textarea",
            pickProp(pickDataEntry2(state.caseData), "investmentAttractionBasis", "InvestmentAttractionBasis")
          )
        );
        card.appendChild(actionRow([
          createButton("ذخیره فرم تکمیلی", "btn--primary", "save-de2"),
          createButton("ارسال برای بررسی", "", "submit-de2"),
        ]));
        card.appendChild(
          el(
            "motion",
            "muted",
            "پس از ذخیره متن و بارگذاری همه مدارک ضروری، «ارسال برای بررسی» را بزنید."
          )
        );
        renderUploads(card, "de2", "مدارک فرم تکمیلی", model.DATA_ENTRY_2_DOCUMENTS);
      }
      renderCommentsHistory(card, phase, "درخواست‌های اصلاح کارشناس", {
        revisionOnly: true,
        emptyText: "هنوز درخواست اصلاحی ثبت نشده است.",
      });
      return;
    }

    card.appendChild(
      el(
        "motion",
        "portal-stage__hint muted",
        prefix === "de1"
          ? "بررسی فرم اولیه و مدارک متقاضی — تأیید یا درخواست اصلاح."
          : "بررسی فرم تکمیلی و مدارک متقاضی — تأیید یا درخواست اصلاح."
      )
    );

    if (prefix === "de1") {
      const de1 = pickDataEntry1(state.caseData);
      renderProfileSummary(card);
      renderReadOnlySummary(card, "فرم اولیه (ثبت‌شده توسط متقاضی)", [
        ["مرحله کسب‌وکار", businessStageLabel(pickProp(de1, "businessStage", "BusinessStage"))],
        [
          "سرمایه مورد نیاز (ریال)",
          formatMoney(pickProp(de1, "requestedAmount", "RequestedAmount")),
        ],
        ["نام نماینده", pickProp(de1, "representativeFullName", "RepresentativeFullName")],
        ["ایمیل", pickProp(de1, "contactEmail", "ContactEmail")],
      ]);
      renderApplicantDocumentsReadOnly(card, "مدارک فرم اولیه متقاضی", model.DATA_ENTRY_1_DOCUMENTS);
    } else {
      const de2 = pickDataEntry2(state.caseData);
      renderProfileSummary(card);
      renderReadOnlySummary(card, "فرم تکمیلی (ثبت‌شده توسط متقاضی)", [
        [
          "مبنای درخواست جذب سرمایه‌گذار",
          pickProp(de2, "investmentAttractionBasis", "InvestmentAttractionBasis"),
        ],
      ]);
      renderApplicantDocumentsReadOnly(card, "مدارک فرم تکمیلی متقاضی", model.DATA_ENTRY_2_DOCUMENTS);
    }

    renderDocumentVersionGroups(card, "تمام نسخه‌های مدارک متقاضی");
    renderCommentsHistory(card, phase, "درخواست‌های اصلاح قبلی", {
      revisionOnly: true,
      emptyText: "هنوز درخواست اصلاحی ثبت نشده است.",
    });
    card.appendChild(actionRow([
      createButton("تأیید", "btn--primary", prefix === "de1" ? "approve-de1" : "approve-de2"),
      createButton("درخواست اصلاح", "btn--warn", prefix === "de1" ? "revise-de1" : "revise-de2"),
    ]));
    card.appendChild(field("پیام اصلاح (برگشت به متقاضی)", prefix + "Revision", "textarea"));
    card.appendChild(
      field(
        "نظر داخلی هنگام تأیید (متقاضی نمی‌بیند)",
        prefix + "InternalComment",
        "textarea"
      )
    );
  }

  function renderStageForStatus(host, status, activeOnly) {
    const current = pickStatus(state.caseData);
    const isCurrent = status === current;
    if (activeOnly && !isCurrent) return;

    const step = model.getStep(status);
    if (!step) return;
    if (state.selectedUnit !== "all" && step.unit !== state.selectedUnit) return;

    const card = el("div", "card portal-card portal-stage");
    if (isCurrent) card.classList.add("is-current");
    card.dataset.stageStatus = String(status);
    card.appendChild(el("div", "card__title", "مرحله " + status + " — " + step.title));
    card.appendChild(el("div", "muted", model.getUnit(step.unit)?.label || ""));

    const role = getSessionRole();
    const phase = model.phaseForStatus(status);

    if (!isCurrent) {
      card.appendChild(
        el(
          "div",
          "portal-stage__hint",
          model.compareStepOrder(status, current) < 0
            ? "این مرحله گذرانده شده است."
            : "این مرحله هنوز نرسیده است."
        )
      );
      host.appendChild(card);
      return;
    }

    if ((status === 6 || status === 7) && role === "Applicant") {
      card.appendChild(el("div", "portal-stage__hint", "پرونده در انتظار ارزش‌گذاری توسط رئیس سرمایه‌گذاری است. از سمت شما اقدامی لازم نیست."));
      host.appendChild(card);
      return;
    }

    if (status === 16 || status === 19) {
      card.appendChild(
        el(
          "div",
          "portal-stage__hint muted",
          status === 16
            ? "پرونده تکمیل شده است. خلاصه کامل اطلاعات ثبت‌شده در زیر قابل مشاهده است."
            : "پرونده بایگانی شده است. خلاصه کامل اطلاعات ثبت‌شده در زیر قابل مشاهده است."
        )
      );
      renderCompletedCaseDossier(card);
      host.appendChild(card);
      return;
    }

    if (!model.canActOnCase(role, step.unit)) {
      card.appendChild(el("div", "portal-stage__hint", "با نقش فعلی، اقدام این مرحله برای شما فعال نیست. جلسه نقش مناسب را انتخاب کنید."));
      host.appendChild(card);
      return;
    }

    if (status === 1 || status === 2) {
      if (status === 1) {
        card.appendChild(
          el(
            "div",
            "portal-stage__hint",
            "پیش‌نویس: فرم و مدارک را تکمیل کنید، ذخیره کنید، سپس «ثبت و ادامه» را بزنید."
          )
        );
      } else if (status === 2) {
        card.appendChild(
          el(
            "div",
            "portal-stage__hint",
            hasSavedDataEntry1()
              ? "فرم ذخیره شده است. برای ارسال به کارشناس، «ارسال برای بررسی کارشناس» را بزنید."
              : "ابتدا «ذخیره فرم اولیه» را بزنید، سپس ارسال برای بررسی."
          )
        );
      }
      renderDataEntryStage(card, "de1", 1, false, status);
    } else if (status === 3) {
      renderDataEntryStage(card, "de1", 1, true, status);
    } else if (status === 4) {
      renderDataEntryStage(card, "de2", 1, false, status);
    } else if (status === 5) {
      renderDataEntryStage(card, "de2", 1, true, status);
    } else if (status === 6 || status === 7) {
      card.appendChild(field("مبلغ ارزش‌گذاری", "valuationAmount", "number"));
      card.appendChild(field("نوع (1 اولیه / 2 ثانویه)", "valuationType", "number", String(status === 6 ? 1 : 2)));
      card.appendChild(field("یادداشت", "valuationNotes", "textarea"));
      card.appendChild(actionRow([createButton("ثبت ارزش‌گذاری", "btn--primary", "record-valuation")]));
      card.appendChild(
        actionRow([
          createButton("تأیید ارزش‌گذاری اولیه", "btn--primary", "approve-val-initial"),
          createButton("تأیید ارزش‌گذاری ثانویه", "btn--primary", "approve-val-secondary"),
        ])
      );
    } else if (status === 8) {
      renderDocumentVersionGroups(card, "نسخه‌های قبلی پیش‌قرارداد");
      appendFileUploadRow(card, {
        idPrefix: "legal-pre",
        title: "فایل پیش‌قرارداد",
        hint: "پس از انتخاب فایل، پیش‌قرارداد بارگذاری و پرونده به مرحله بعد می‌رود.",
        contractKind: "preliminary",
      });
    } else if (status === 9) {
      renderDocumentVersionGroups(card, "تاریخچه بارگذاری پیش‌قرارداد");
      renderCommentsHistory(card, 3, "گفتگوی پیش‌قرارداد", {
        emptyText: "هنوز نظری ثبت نشده است.",
      });
      renderPublicCommentForm(card, 3, "legalUser");
      card.appendChild(actionRow([
        createButton("تأیید پیش‌قرارداد", "btn--primary", "approve-pre-contract"),
        createButton("درخواست اصلاح", "btn--warn", "revise-pre-contract"),
      ]));
      card.appendChild(field("پیام اصلاح برای واحد حقوقی", "preContractRevision", "textarea"));
    } else if (status === 10) {
      card.appendChild(field("توضیح تدوین قرارداد", "finalizeContractComment", "textarea"));
      appendFileUploadRow(card, {
        idPrefix: "legal-draft",
        title: "نسخه پیش‌نویس قرارداد (اختیاری)",
        hint: "فایل پیش‌نویس را بارگذاری کنید؛ سپس «نهایی‌سازی پیش‌نویس» را بزنید.",
        uploadType: 8,
      });
      card.appendChild(actionRow([createButton("نهایی‌سازی پیش‌نویس", "btn--primary", "finalize-contract")]));
    } else if (status === 11) {
      card.appendChild(actionRow([createButton("تأیید امضا", "btn--primary", "confirm-signature")]));
    } else if (status === 12) {
      appendFileUploadRow(card, {
        idPrefix: "legal-signed",
        title: "قرارداد امضاشده",
        hint: "پس از انتخاب فایل، قرارداد امضاشده بارگذاری و پرونده به مرحله بعد می‌رود.",
        contractKind: "signed",
      });
    } else if (status === 13) {
      card.appendChild(field("نام بانک", "wsBank", "text"));
      card.appendChild(field("شبا", "wsIban", "text"));
      card.appendChild(field("مبلغ مصوب", "wsApproved", "number"));
      card.appendChild(field("برنامه پرداخت", "wsSchedule", "textarea"));
      card.appendChild(field("یادداشت", "wsNotes", "textarea"));
      card.appendChild(actionRow([
        createButton("ذخیره کاربرگ", "btn--primary", "save-worksheet"),
        createButton("ارسال برای واحد مالی", "", "submit-worksheet"),
      ]));
    } else if (status === 14) {
      card.appendChild(
        el(
          "div",
          "portal-stage__hint muted",
          "پس از تأیید، پرونده برای تأیید مدیرعامل ارسال می‌شود و تا آن زمان وارد مرحله پرداخت نمی‌شود."
        )
      );
      card.appendChild(actionRow([
        createButton("تأیید و ارسال به مدیرعامل", "btn--primary", "approve-worksheet"),
        createButton("درخواست اصلاح", "btn--warn", "revise-worksheet"),
      ]));
      renderCommentsHistory(card, 4, "درخواست‌های اصلاح قبلی", {
        revisionOnly: true,
        emptyText: "هنوز درخواست اصلاحی ثبت نشده است.",
      });
      card.appendChild(field("پیام اصلاح برای سرمایه‌گذاری", "wsRevision", "textarea"));
      card.appendChild(
        field("نظر داخلی هنگام تأیید (متقاضی نمی‌بیند)", "wsInternalComment", "textarea")
      );
    } else if (status === 20) {
      card.appendChild(
        el(
          "div",
          "portal-stage__hint muted",
          "تا تأیید شما، واحد مالی نمی‌تواند پرداخت ثبت کند."
        )
      );
      card.appendChild(actionRow([
        createButton("تأیید و انتقال به پرداخت", "btn--primary", "approve-ceo"),
        createButton("درخواست اصلاح کاربرگ", "btn--warn", "revise-ceo"),
      ]));
      card.appendChild(field("پیام اصلاح برای سرمایه‌گذاری/مالی", "ceoRevision", "textarea"));
    } else if (status === 15) {
      card.appendChild(
        el(
          "motion",
          "portal-stage__hint muted",
          "پرداخت می‌تواند چند مرحله‌ای باشد: هر قسط را جدا ثبت کنید. پرونده وقتی تکمیل می‌شود که مجموع پرداخت‌های «تأییدشده» به مبلغ مصوب کاربرگ برسد."
        )
      );
      renderPaymentsSection(card);
      card.appendChild(el("motion", "card__title", "ثبت قسط جدید"));
      card.appendChild(field("مبلغ این قسط", "payAmount", "number"));
      card.appendChild(field("تاریخ پرداخت", "payDate", "date", todayIsoDate()));
      card.appendChild(field("شماره تراکنش (یکتا)", "payTxn", "text"));
      card.appendChild(field("روش (1 انتقال / 2 چک / 3 نقد / 4 سایر)", "payMethod", "number", "1"));
      card.appendChild(field("وضعیت (1 در انتظار / 2 تأییدشده)", "payStatus", "number", "1"));
      const receiptDoc = documentForType(10);
      appendFileUploadRow(card, {
        idPrefix: "payment-receipt",
        title: "رسید پرداخت",
        hint: "پس از انتخاب فایل، رسید از مسیر واحد اسناد (presign → S3 → confirm) بارگذاری می‌شود؛ سپس «ثبت قسط» را بزنید.",
        uploadType: 10,
        buttonText: "انتخاب رسید",
        uploadedLabel: receiptDoc
          ? "✓ بارگذاری شده" +
            (receiptDoc.fileName || receiptDoc.FileName
              ? ": " + (receiptDoc.fileName || receiptDoc.FileName)
              : "")
          : null,
      });
      card.appendChild(actionRow([createButton("ثبت قسط پرداخت", "btn--primary", "record-payment")]));
    }

    host.appendChild(card);
  }

  function renderStageHost() {
    const host = qs("#portalStageHost");
    if (!host) return;
    host.innerHTML = "";

    if (!state.caseData) {
      host.appendChild(el("div", "portal-empty", "برای شروع، یک پرونده انتخاب کنید یا از تب پرونده‌ها پرونده جدید بسازید."));
      return;
    }

    const current = pickStatus(state.caseData);
    renderStageForStatus(host, current, true);

    if (!qs("#caseAttachmentsHost") && current !== 16 && current !== 19) {
      renderDocumentsList(host);
      renderHistory(host);
    }
  }

  function renderSummary() {
    const empty = qs("#portalCaseEmpty");
    const header = qs("#portalCaseHeader");
    if (!empty || !header) return;

    if (!state.caseData) {
      empty.classList.remove("hidden");
      header.classList.add("hidden");
      return;
    }

    empty.classList.add("hidden");
    header.classList.remove("hidden");
    qs("#portalCaseNumber").textContent = pickCaseNumber(state.caseData) || "—";
    const title = pickProp(state.caseData, "title", "Title") || "—";
    const titleEl = qs("#portalCaseTitle");
    if (titleEl) titleEl.textContent = title;
    const titleInput = qs("#portalCaseTitleInput");
    if (titleInput && document.activeElement !== titleInput) {
      titleInput.value = pickProp(state.caseData, "title", "Title") || "";
    }
    const company = pickCompany(state.caseData);
    const companyEl = qs("#portalCaseCompany");
    if (companyEl) {
      companyEl.textContent = company
        ? (pickProp(company, "name", "Name") || "—") +
          (pickProp(company, "nationalId", "NationalId") ? " · " + pickProp(company, "nationalId", "NationalId") : "")
        : "—";
    }
    const caseIdEl = qs("#portalCaseId");
    if (caseIdEl) caseIdEl.textContent = pickId(state.caseData) || state.caseId;
    const statusValue = pickStatus(state.caseData);
    const step = model.getStep(statusValue);
    qs("#portalCaseStatus").textContent = step ? step.title : String(statusValue);
    qs("#portalCasePhase").textContent = model.PHASES[pickPhase(state.caseData)] || "—";
    qs("#portalCaseRole").textContent = getSessionRole() || "بدون نقش";
    renderActionHint();
  }

  function renderActionHint() {
    const box = qs("#portalActionHint");
    if (!box) return;
    const status = pickStatus(state.caseData);
    let text = "";
    if (status === 1) {
      text = hasSavedDataEntry1()
        ? "گام بعدی: «ثبت و ادامه به فرم اولیه» (مدارک اختیاری)."
        : "گام بعدی: «ذخیره فرم اولیه»، سپس «ثبت و ادامه».";
    } else if (status === 2) {
      const pitchOk = !!documentForType(1);
      text = hasSavedDataEntry1()
        ? pitchOk
          ? "گام بعدی: «ارسال برای بررسی کارشناس»."
          : "فرم ذخیره شده؛ پیچ‌دک را بارگذاری کنید."
        : "ابتدا فرم را ذخیره کنید.";
    } else if (status === 3) {
      text = "پرونده در انتظار بررسی کارشناس است.";
    } else if (status === 4) {
      if (!hasSavedDataEntry2()) {
        text = "گام بعدی: متن «مبنای درخواست» را بنویسید و «ذخیره فرم تکمیلی» را بزنید.";
      } else if (!de2RequiredDocumentsComplete()) {
        text = "فرم ذخیره شده؛ همه مدارک ضروری را بارگذاری کنید.";
      } else {
        text = "گام بعدی: «ارسال برای بررسی».";
      }
    } else if (status === 5) {
      text = "پرونده در انتظار بررسی فرم تکمیلی است.";
    }
    if (!text) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.classList.remove("hidden");
    box.textContent = text;
  }

  function render() {
    renderSummary();
    renderStepper();
    renderUnitTabs();
    renderStageHost();
    populateFormsFromCase();
  }

  async function withBusy(fn) {
    if (state.busy) {
      setPortalInfo("عملیات قبلی هنوز در حال انجام است. لطفاً چند ثانیه صبر کنید.");
      return;
    }
    state.busy = true;
    setPortalError("");
    try {
      await fn();
      setPortalInfo("عملیات با موفقیت انجام شد.");
    } catch (error) {
      setPortalInfo("");
      setPortalError(error && error.message ? error.message : String(error));
    } finally {
      state.busy = false;
    }
  }

  async function handleAction(action, trigger) {
    const caseId = readCaseId();
    if (!caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const statusBefore = pickStatus(state.caseData);

    if (action === "post-comment") {
      const phase = Number(trigger.dataset.commentPhase);
      const prefix = trigger.dataset.commentPrefix || "";
      const message = readValue(prefix + "Comment");
      await postComment(caseId, phase, message, false);
    } else if (action === "save-de1") {
      const stage = readNumber("de1Stage");
      if (stage !== 1 && stage !== 2) throw new Error("مرحله کسب‌وکار را انتخاب کنید.");
      const amount = readNumber("de1Amount");
      if (amount <= 0) throw new Error("سرمایه مورد نیاز را وارد کنید.");
      await state.panel.apiRequest({
        method: "PUT",
        path: casesPath("/" + caseId + "/data-entry1"),
        body: {
          businessStage: stage,
          requestedAmount: amount,
        },
      });
    } else if (action === "submit-de1") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry1/submit"),
        body: {},
      });
    } else if (action === "approve-de1") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry1/approve"),
        body: { internalComment: readValue("de1InternalComment") || null },
      });
    } else if (action === "revise-de1") {
      const message = readValue("de1Revision");
      if (!message) throw new Error("پیام اصلاح الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry1/revision-request"),
        body: { message },
      });
    } else if (action === "save-de2") {
      await ensureDataEntry2Saved(caseId);
    } else if (action === "submit-de2") {
      await ensureDataEntry2Saved(caseId);
      if (!de2RequiredDocumentsComplete()) {
        throw new Error("همه مدارک ضروری فرم تکمیلی باید بارگذاری شوند.");
      }
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry2/submit"),
        body: {},
      });
    } else if (action === "approve-de2") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry2/approve"),
        body: { internalComment: readValue("de2InternalComment") || null },
      });
    } else if (action === "revise-de2") {
      const message = readValue("de2Revision");
      if (!message) throw new Error("پیام اصلاح الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/data-entry2/revision-request"),
        body: { message },
      });
    } else if (action === "record-valuation") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/valuations"),
        body: {
          type: readNumber("valuationType"),
          amount: readNumber("valuationAmount"),
          notes: readValue("valuationNotes") || null,
        },
      });
    } else if (action === "approve-val-initial") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/valuations/initial/approve"),
        body: { comment: readValue("valuationNotes") || null },
      });
    } else if (action === "approve-val-secondary") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/valuations/secondary/approve"),
        body: { comment: readValue("valuationNotes") || null },
      });
    } else if (action === "approve-pre-contract") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/contracts/preliminary/approve"),
        body: {},
      });
    } else if (action === "revise-pre-contract") {
      const message = readValue("preContractRevision");
      if (!message) throw new Error("پیام اصلاح الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/contracts/preliminary/revision-request"),
        body: { message },
      });
    } else if (action === "finalize-contract") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/contracts/finalize-draft"),
        body: { comment: readValue("finalizeContractComment") || null },
      });
    } else if (action === "confirm-signature") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/contracts/confirm-signature"),
        body: { comment: readValue("confirmSignatureComment") || null },
      });
    } else if (action === "save-worksheet") {
      await state.panel.apiRequest({
        method: "PUT",
        path: casesPath("/" + caseId + "/financial-worksheet"),
        body: {
          bankName: readValue("wsBank"),
          iban: readValue("wsIban"),
          approvedAmount: readNumber("wsApproved") || null,
          paymentSchedule: readValue("wsSchedule") || null,
          notes: readValue("wsNotes") || null,
        },
      });
    } else if (action === "submit-worksheet") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/financial-worksheet/submit"),
        body: { comment: readValue("wsSubmitComment") || null },
      });
    } else if (action === "approve-worksheet") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/financial-worksheet/approve"),
        body: { internalComment: readValue("wsInternalComment") || null },
      });
    } else if (action === "revise-worksheet") {
      const message = readValue("wsRevision");
      if (!message) throw new Error("پیام اصلاح الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/financial-worksheet/revision-request"),
        body: { message },
      });
    } else if (action === "approve-ceo") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/ceo-approval/approve"),
        body: {},
      });
    } else if (action === "revise-ceo") {
      const message = readValue("ceoRevision");
      if (!message) throw new Error("پیام اصلاح الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/ceo-approval/revision-request"),
        body: { message },
      });
    } else if (action === "record-payment") {
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/payments"),
        body: buildRecordPaymentPayload(),
      });
    } else if (action === "confirm-payment") {
      const paymentId = trigger.dataset.paymentId || readValue("payId");
      if (!paymentId) throw new Error("شناسه پرداخت الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/payments/" + encodeURIComponent(paymentId) + "/confirm"),
        body: null,
        json: false,
      });
    } else if (action === "cancel-payment") {
      const paymentId = trigger.dataset.paymentId || readValue("payId");
      if (!paymentId) throw new Error("شناسه پرداخت الزامی است.");
      await state.panel.apiRequest({
        method: "POST",
        path: casesPath("/" + caseId + "/payments/" + encodeURIComponent(paymentId) + "/cancel"),
        body: null,
        json: false,
      });
    } else {
      return;
    }

    await refreshCase(statusBefore);
  }

  async function handleUpload(input) {
    const caseId = readCaseId();
    if (!caseId) throw new Error("شناسه پرونده تنظیم نشده است.");
    const statusBefore = pickStatus(state.caseData);
    const file = input.files && input.files[0];
    if (!file) return;

    let s3Key = null;
    if (input.dataset.contractKind) {
      s3Key = await uploadContract(caseId, input.dataset.contractKind, file);
    } else {
      const documentType = Number(input.dataset.uploadType);
      s3Key = await uploadDocument(caseId, documentType, file);
    }
    if (s3Key) input.dataset.s3Key = s3Key;
    const status = input.closest(".portal-upload-row__control")?.querySelector(".portal-upload-row__status");
    if (status) {
      status.textContent = "✓ بارگذاری شد: " + file.name;
      status.classList.add("is-uploaded");
      status.closest(".portal-upload-row")?.classList.add("portal-upload-row--done");
    }
    await refreshCase(statusBefore);
  }

  function wireEvents() {
    const refreshBtn = qs("#portalRefreshCase");
    if (refreshBtn) refreshBtn.addEventListener("click", () => withBusy(refreshCase));

    const titleSaveBtn = qs("#portalCaseTitleSave");
    if (titleSaveBtn) {
      titleSaveBtn.addEventListener("click", () =>
        withBusy(async () => {
          if (!state.caseId) return;
          const title = qs("#portalCaseTitleInput")?.value?.trim() || null;
          await state.panel.apiRequest({
            method: "PUT",
            path: casesPath("/" + state.caseId + "/title"),
            body: { title },
          });
          await refreshCase();
          setPortalInfo("عنوان ذخیره شد.");
        })
      );
    }

    const unitTabs = qs("#portalUnitTabs");
    if (unitTabs) {
      unitTabs.addEventListener("click", (event) => {
        const btn = event.target.closest("[data-unit]");
        if (!btn) return;
        state.selectedUnit = btn.dataset.unit;
        render();
      });
    }

    const stageHost = qs("#portalStageHost");
    if (stageHost) {
      stageHost.addEventListener("click", (event) => {
        const downloadBtn = event.target.closest("[data-action='download-document']");
        if (downloadBtn) {
          event.preventDefault();
          withBusy(() => downloadDocument(downloadBtn.dataset.documentId));
          return;
        }
        const btn = event.target.closest("[data-action]");
        if (!btn) return;
        withBusy(() => handleAction(btn.dataset.action, btn));
      });
      stageHost.addEventListener("change", (event) => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || input.type !== "file") return;
        if (!input.dataset.uploadType && !input.dataset.contractKind) return;
        withBusy(() => handleUpload(input));
      });
    }

    document.addEventListener("testpanel:case-changed", (ev) => {
      if (ev.detail?.module && ev.detail.module !== "investment") return;
      state.caseId = readCaseId();
      withBusy(refreshCase);
    });

    document.addEventListener("testpanel:open-comment-step", (ev) => {
      if (ev.detail?.module && ev.detail.module !== "investment") return;
      const unit = ev.detail?.unit;
      if (unit && unit !== "all") state.selectedUnit = unit;
      render();
      const phase = ev.detail?.phase;
      const target =
        (ev.detail?.commentId &&
          document.querySelector(`[data-comment-id="${ev.detail.commentId}"]`)) ||
        (phase != null && document.querySelector(`[data-comment-phase="${phase}"]`));
      target?.scrollIntoView({ behavior: "smooth", block: "center" });
      target?.classList.add("is-highlight");
    });
  }

  async function loadCaseQuietly() {
    if (state.busy) return;
    state.busy = true;
    setPortalError("");
    setPortalInfo("");
    try {
      await refreshCase();
    } catch (error) {
      setPortalError(error && error.message ? error.message : String(error));
    } finally {
      state.busy = false;
    }
  }

  window.initPortal = function initPortal(panel) {
    state.panel = panel;
    wireEvents();
    render();
    void loadCaseQuietly();
  };
})();
