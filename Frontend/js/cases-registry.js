/* global WorkflowModel, GuaranteeWorkflowModel */
(function () {
  const state = {
    panel: null,
    module: "investment",
    cases: [],
    selectedId: null,
    detail: null,
    loadingList: false,
    loadingDetail: false,
  };

  const qs = (sel) => document.querySelector(sel);

  function pick(obj, camel, pascal) {
    if (!obj) return undefined;
    if (obj[camel] !== undefined) return obj[camel];
    if (obj[pascal] !== undefined) return obj[pascal];
    return undefined;
  }

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function unwrapPaged(body) {
    const payload = unwrap(body);
    if (Array.isArray(payload)) return { items: payload, total: payload.length };

    const items = pick(payload, "items", "Items") || [];
    const total = pick(payload, "totalCount", "TotalCount") ?? items.length;
    return { items, total };
  }

  function pretty(obj) {
    if (obj == null) return "";
    try {
      return typeof obj === "string" ? obj : JSON.stringify(obj, null, 2);
    } catch {
      return String(obj);
    }
  }

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  function resolveRole(session) {
    if (!session) return "";
    if (window.WorkflowModel && typeof WorkflowModel.normalizeRole === "function") {
      return WorkflowModel.normalizeRole(session.userRoleText, session.userRoleNumber);
    }
    const n = Number(session.userRoleNumber);
    if (n === 12) return "CEO";
    if (n === 100) return "Admin";
    return String(session.userRoleText || "").trim();
  }

  function canAccessRegistry() {
    const role = resolveRole(state.panel.getActiveSession());
    return role === "CEO" || role === "Admin";
  }

  function apiBase() {
    return state.module === "guarantee"
      ? state.panel.guaranteeCasesBasePath()
      : state.panel.casesBasePath();
  }

  function buildQuery(params) {
    const sp = new URLSearchParams();
    for (const [k, v] of Object.entries(params || {})) {
      if (v === "" || v == null) continue;
      sp.set(k, String(v));
    }
    const s = sp.toString();
    return s ? "?" + s : "";
  }

  function formatDate(value) {
    if (!value) return "—";
    try {
      return new Date(value).toLocaleString("fa-IR");
    } catch {
      return String(value);
    }
  }

  function investmentStatusLabel(status) {
    const n =
      WorkflowModel && typeof WorkflowModel.coerceStatus === "function"
        ? WorkflowModel.coerceStatus(status)
        : Number(status);
    if (WorkflowModel && WorkflowModel.STEPS) {
      const step = WorkflowModel.STEPS.find((s) => s.status === n);
      if (step) return step.title + " (" + n + ")";
    }
    return String(status);
  }

  function investmentPhaseLabel(phase) {
    const n =
      WorkflowModel && typeof WorkflowModel.coercePhase === "function"
        ? WorkflowModel.coercePhase(phase)
        : Number(phase);
    if (WorkflowModel && WorkflowModel.PHASES && WorkflowModel.PHASES[n]) {
      return WorkflowModel.PHASES[n] + " (" + n + ")";
    }
    return String(phase);
  }

  function guaranteeStatusLabel(status) {
    if (GuaranteeWorkflowModel && typeof GuaranteeWorkflowModel.stepForStatus === "function") {
      const step = GuaranteeWorkflowModel.stepForStatus(status);
      const n =
        typeof GuaranteeWorkflowModel.coerceStatus === "function"
          ? GuaranteeWorkflowModel.coerceStatus(status)
          : Number(status);
      return (step.title || "—") + " (" + n + ")";
    }
    return String(status);
  }

  function guaranteePhaseLabel(phase) {
    const n = Number(phase);
    if (GuaranteeWorkflowModel && GuaranteeWorkflowModel.PHASES && GuaranteeWorkflowModel.PHASES[n]) {
      return GuaranteeWorkflowModel.PHASES[n] + " (" + n + ")";
    }
    return String(phase);
  }

  function statusLabel(c) {
    const st = pick(c, "currentStatus", "CurrentStatus");
    return state.module === "guarantee" ? guaranteeStatusLabel(st) : investmentStatusLabel(st);
  }

  function phaseLabel(c) {
    const ph = pick(c, "currentPhase", "CurrentPhase");
    return state.module === "guarantee" ? guaranteePhaseLabel(ph) : investmentPhaseLabel(ph);
  }

  function applicantLabel(c) {
    if (window.UIComponents) return UIComponents.applicantLabelFromCase(c);
    const company = c.company || c.Company;
    const type = Number(pick(c, "applicantType", "ApplicantType"));
    if (company && type === 2) return pick(company, "name", "Name") || "شرکت";
    if (state.module === "guarantee") return "حقیقی";
    const appId = pick(c, "applicantUserId", "ApplicantUserId");
    return appId ? "متقاضی · " + String(appId).slice(0, 8) + "…" : "متقاضی";
  }

  function companyLabel(c) {
    if (window.UIComponents) return UIComponents.companySummary(c);
    const company = c.company || c.Company;
    if (!company) return "—";
    return pick(company, "name", "Name") || "—";
  }

  function updateAccessUi() {
    const allowed = canAccessRegistry();
    qs("#navCaseRegistry")?.classList.toggle("hidden", !allowed);
    qs("#registryAccessDenied")?.classList.toggle("hidden", allowed);
    qs("#registryPanel")?.classList.toggle("hidden", !allowed);
    if (!allowed) {
      state.cases = [];
      state.selectedId = null;
      renderTable();
      renderDetailEmpty();
    }
  }

  function fillFilterSelects() {
    const statusSel = qs("#registryStatus");
    const phaseSel = qs("#registryPhase");
    if (!statusSel || !phaseSel) return;

    statusSel.innerHTML = '<option value="">(همه)</option>';
    phaseSel.innerHTML = '<option value="">(همه)</option>';

    if (state.module === "guarantee" && GuaranteeWorkflowModel) {
      const steps = GuaranteeWorkflowModel.getStepperSteps
        ? GuaranteeWorkflowModel.getStepperSteps()
        : Object.values(GuaranteeWorkflowModel.STEPS || {});
      steps.forEach((step) => {
        const o = document.createElement("option");
        o.value = String(step.id);
        o.textContent = step.title + " (" + step.id + ")";
        statusSel.appendChild(o);
      });
      Object.entries(GuaranteeWorkflowModel.PHASES || {}).forEach(([k, label]) => {
        const o = document.createElement("option");
        o.value = k;
        o.textContent = label + " (" + k + ")";
        phaseSel.appendChild(o);
      });
    } else if (WorkflowModel) {
      (WorkflowModel.STEPS || []).forEach((step) => {
        const o = document.createElement("option");
        o.value = String(step.status);
        o.textContent = step.title + " (" + step.status + ")";
        statusSel.appendChild(o);
      });
      Object.entries(WorkflowModel.PHASES || {}).forEach(([k, label]) => {
        const o = document.createElement("option");
        o.value = k;
        o.textContent = label + " (" + k + ")";
        phaseSel.appendChild(o);
      });
    }
  }

  function setListError(msg) {
    const box = qs("#registryListError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
  }

  async function loadList() {
    if (!canAccessRegistry()) return;
    setListError("");
    state.loadingList = true;
    qs("#registryListLoading")?.classList.remove("hidden");
    qs("#registryListEmpty")?.classList.add("hidden");

    try {
      const page = Number(qs("#registryPage")?.value) || 1;
      const take = Number(qs("#registryPageSize")?.value) || 25;
      const query = buildQuery({
        caseNumber: (qs("#registryCaseNumber")?.value || "").trim(),
        applicantUserId: (qs("#registryApplicantUserId")?.value || "").trim(),
        currentStatus: qs("#registryStatus")?.value,
        currentPhase: qs("#registryPhase")?.value,
        createdAtFrom: (qs("#registryFrom")?.value || "").trim(),
        createdAtTo: (qs("#registryTo")?.value || "").trim(),
        skip: Math.max(0, (page - 1) * take),
        take,
      });
      const res = await state.panel.apiRequest({ method: "GET", path: apiBase() + query });
      const { items, total } = unwrapPaged(res.body);
      state.cases = items;
      qs("#registryListMeta").textContent = state.cases.length + " پرونده در این صفحه";
      renderTable();
      if (!state.cases.length) qs("#registryListEmpty")?.classList.remove("hidden");
    } catch (e) {
      setListError(e.message || String(e));
      state.cases = [];
      renderTable();
    } finally {
      state.loadingList = false;
      qs("#registryListLoading")?.classList.add("hidden");
    }
  }

  function renderTable() {
    const body = qs("#registryTableBody");
    if (!body) return;
    body.innerHTML = "";

    state.cases.forEach((c) => {
      const id = pick(c, "id", "Id");
      const tr = document.createElement("tr");
      if (id === state.selectedId) tr.classList.add("is-selected");

      const tdNum = document.createElement("td");
      tdNum.innerHTML = '<span class="mono">' + (pick(c, "caseNumber", "CaseNumber") || "—") + "</span>";

      const tdSt = document.createElement("td");
      tdSt.textContent = statusLabel(c);

      const tdCompany = document.createElement("td");
      tdCompany.textContent = companyLabel(c);

      const tdApp = document.createElement("td");
      tdApp.textContent = applicantLabel(c);

      const tdDt = document.createElement("td");
      tdDt.textContent = formatDate(pick(c, "updatedAt", "UpdatedAt") || pick(c, "createdAt", "CreatedAt"));

      const tdBtn = document.createElement("td");
      const btn = el("button", "btn btn--small", "جزئیات");
      btn.type = "button";
      btn.addEventListener("click", (ev) => {
        ev.stopPropagation();
        void selectCase(id);
      });
      tdBtn.appendChild(btn);

      tr.appendChild(tdNum);
      tr.appendChild(tdSt);
      tr.appendChild(tdCompany);
      tr.appendChild(tdApp);
      tr.appendChild(tdDt);
      tr.appendChild(tdBtn);

      tr.addEventListener("click", () => void selectCase(id));
      body.appendChild(tr);
    });
  }

  function renderDetailEmpty() {
    qs("#registryDetailEmpty")?.classList.remove("hidden");
    qs("#registryDetailHost")?.classList.add("hidden");
    qs("#registryDetailHost").innerHTML = "";
  }

  async function downloadDocument(caseId, documentId) {
    const session = state.panel.getActiveSession();
    const headers = {};
    if (session?.accessToken) headers.Authorization = "Bearer " + session.accessToken;
    const url = state.panel.makeUrl(
      apiBase() + "/" + caseId + "/documents/" + encodeURIComponent(documentId) + "/download"
    );
    const res = await fetch(url, { method: "GET", headers });
    if (!res.ok) throw new Error("دانلود با کد " + res.status + " ناموفق بود.");
    const blob = await res.blob();
    const disposition = res.headers.get("Content-Disposition") || "";
    const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
    const fileName = match ? decodeURIComponent(match[1].replace(/"/g, "")) : "document";
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  }

  async function fetchDetailBundle(caseId) {
    const base = apiBase() + "/" + caseId;
    const includeInternal = "true";
    const [caseRes, historyRes, docsRes, commentsRes] = await Promise.all([
      state.panel.apiRequest({ method: "GET", path: base }),
      state.panel.apiRequest({ method: "GET", path: base + "/history" }),
      state.panel.apiRequest({ method: "GET", path: base + "/documents" }),
      state.panel.apiRequest({ method: "GET", path: base + "/comments?includeInternal=" + includeInternal }),
    ]);

    const bundle = {
      case: unwrap(caseRes.body),
      history: unwrap(historyRes.body) || [],
      documents: unwrap(docsRes.body) || [],
      comments: unwrap(commentsRes.body) || [],
    };

    if (state.module === "investment") {
      try {
        const evalRes = await state.panel.apiRequest({ method: "GET", path: base + "/evaluations" });
        bundle.evaluations = unwrap(evalRes.body) || [];
      } catch {
        bundle.evaluations = [];
      }
      try {
        const payRes = await state.panel.apiRequest({ method: "GET", path: base + "/payments" });
        bundle.payments = unwrap(payRes.body) || [];
      } catch {
        bundle.payments = [];
      }
    }

    return bundle;
  }

  function appendKv(dl, label, value) {
    const dt = document.createElement("dt");
    dt.textContent = label;
    const dd = document.createElement("dd");
    dd.textContent = value == null || value === "" ? "—" : String(value);
    dl.appendChild(dt);
    dl.appendChild(dd);
  }

  function accordion(title, renderBody) {
    const details = el("details", "registry-accordion");
    details.open = title === "خلاصه پرونده" || title === "داده کامل (JSON)";
    const summary = document.createElement("summary");
    summary.textContent = title;
    const body = el("div", "registry-accordion__body");
    renderBody(body);
    details.appendChild(summary);
    details.appendChild(body);
    return details;
  }

  function renderJsonSection(parent, data) {
    const pre = el("pre", "registry-json", pretty(data));
    parent.appendChild(pre);
  }

  function renderDetail(bundle) {
    const host = qs("#registryDetailHost");
    if (!host) return;
    host.innerHTML = "";
    qs("#registryDetailEmpty")?.classList.add("hidden");
    host.classList.remove("hidden");

    const c = bundle.case || {};
    const caseId = pick(c, "id", "Id");
    const caseNumber = pick(c, "caseNumber", "CaseNumber");

    const header = el("div", "registry-detail-header");
    const title = el("div", "card__title", (state.module === "guarantee" ? "ضمانت‌نامه · " : "سرمایه‌گذاری · ") + (caseNumber || "—"));
    header.appendChild(title);

    const actions = el("div", "registry-detail-actions");
    const btnPortal = el("button", "btn btn--primary", "باز کردن در پورتال");
    btnPortal.type = "button";
    btnPortal.addEventListener("click", () => {
      if (state.module === "guarantee") {
        state.panel.setGuaranteeCaseId(caseId);
        state.panel.setCaseModule("guarantee");
        document.querySelector('[data-tab="tabGuarantee"]')?.click();
      } else {
        state.panel.setCurrentCaseId(caseId);
        state.panel.setCaseModule("investment");
        document.querySelector('[data-tab="tabPortal"]')?.click();
      }
    });
    const btnRefresh = el("button", "btn", "بروزرسانی جزئیات");
    btnRefresh.type = "button";
    btnRefresh.addEventListener("click", () => void selectCase(caseId));
    actions.appendChild(btnPortal);
    actions.appendChild(btnRefresh);
    header.appendChild(actions);
    host.appendChild(header);

    host.appendChild(
      accordion("خلاصه پرونده", (body) => {
        const dl = el("dl", "registry-kv");
        appendKv(dl, "شناسه", caseId);
        appendKv(dl, "شماره پرونده", caseNumber);
        appendKv(dl, "وضعیت", statusLabel(c));
        appendKv(dl, "فاز", phaseLabel(c));
        appendKv(dl, "نوع متقاضی", Number(pick(c, "applicantType", "ApplicantType")) === 2 ? "حقوقی" : "حقیقی");
        const company = c.company || c.Company;
        if (company) {
          appendKv(dl, "شرکت", pick(company, "name", "Name"));
          appendKv(dl, "شناسه ملی", pick(company, "nationalId", "NationalId"));
        }
        if (pick(c, "applicantUserId", "ApplicantUserId")) {
          appendKv(dl, "متقاضی (UserId)", pick(c, "applicantUserId", "ApplicantUserId"));
        }
        appendKv(dl, "ایجاد", formatDate(pick(c, "createdAt", "CreatedAt")));
        appendKv(dl, "بروزرسانی", formatDate(pick(c, "updatedAt", "UpdatedAt")));
        appendKv(dl, "تکمیل", formatDate(pick(c, "completedAt", "CompletedAt")));
        if (pick(c, "workflowInstanceId", "WorkflowInstanceId")) {
          appendKv(dl, "Workflow", pick(c, "workflowInstanceId", "WorkflowInstanceId"));
        }
        body.appendChild(dl);
      })
    );

    if (state.module === "guarantee") {
      const app = c.application || c.Application;
      const af = c.approvalForm || c.ApprovalForm;
      const snap = c.applicantCreditSnapshot || c.ApplicantCreditSnapshot;
      if (app) host.appendChild(accordion("درخواست متقاضی", (b) => renderJsonSection(b, app)));
      if (af) host.appendChild(accordion("فرم تصویب", (b) => renderJsonSection(b, af)));
      if (snap) host.appendChild(accordion("جدول ۱ اعتباری صندوق", (b) => renderJsonSection(b, snap)));
    } else {
      const de1 = c.dataEntry1 || c.DataEntry1;
      const de2 = c.dataEntry2 || c.DataEntry2;
      if (de1) host.appendChild(accordion("فرم ورود ۱", (b) => renderJsonSection(b, de1)));
      if (de2) host.appendChild(accordion("فرم ورود ۲", (b) => renderJsonSection(b, de2)));
    }

    const history = Array.isArray(bundle.history) ? bundle.history : [];
    host.appendChild(
      accordion("تاریخچه گردش کار (" + history.length + ")", (body) => {
        if (!history.length) {
          body.appendChild(el("div", "muted", "موردی نیست."));
          return;
        }
        history.forEach((h) => {
          const row = el("div", "registry-history-item");
          row.innerHTML =
            "<strong>" +
            formatDate(pick(h, "createdAt", "CreatedAt")) +
            "</strong> · " +
            (pick(h, "action", "Action") || "—") +
            " · " +
            (pick(h, "actorRole", "ActorRole") || "—") +
            "<br/><span class='muted'>" +
            "از " +
            (pick(h, "fromStatus", "FromStatus") ?? "—") +
            " → " +
            (pick(h, "toStatus", "ToStatus") ?? "—") +
            "</span>" +
            (pick(h, "comment", "Comment") ? "<br/>" + pick(h, "comment", "Comment") : "");
          body.appendChild(row);
        });
      })
    );

    const docs = Array.isArray(bundle.documents) ? bundle.documents : [];
    host.appendChild(
      accordion("مدارک (" + docs.length + ")", (body) => {
        if (!docs.length) {
          body.appendChild(el("div", "muted", "مدرکی ثبت نشده."));
          return;
        }
        docs.forEach((d) => {
          const row = el("div", "registry-doc-item");
          const docId = pick(d, "id", "Id");
          const meta = el("span");
          meta.textContent =
            (pick(d, "fileName", "FileName") || "—") +
            " · نوع " +
            (pick(d, "documentType", "DocumentType") ?? "—") +
            " · v" +
            (pick(d, "version", "Version") ?? "1");
          row.appendChild(meta);
          const btn = el("button", "btn btn--small", "دانلود");
          btn.type = "button";
          btn.addEventListener("click", () => {
            void downloadDocument(caseId, docId).catch((err) => alert(err.message || String(err)));
          });
          row.appendChild(btn);
          body.appendChild(row);
        });
      })
    );

    const comments = Array.isArray(bundle.comments) ? bundle.comments : [];
    host.appendChild(
      accordion("نظرات (" + comments.length + ")", (body) => {
        if (!comments.length) {
          body.appendChild(el("div", "muted", "نظری نیست."));
          return;
        }
        comments.forEach((cm) => {
          const row = el("div", "registry-comment-item");
          const internal = pick(cm, "isInternal", "IsInternal") ? " [داخلی]" : "";
          const rev = pick(cm, "isRevisionRequest", "IsRevisionRequest") ? " [درخواست اصلاح]" : "";
          row.innerHTML =
            "<strong>" +
            formatDate(pick(cm, "createdAt", "CreatedAt")) +
            "</strong>" +
            internal +
            rev +
            " · " +
            (pick(cm, "senderRole", "SenderRole") || "—") +
            "<br/>" +
            (pick(cm, "message", "Message") || "—");
          body.appendChild(row);
        });
      })
    );

    if (state.module === "investment") {
      const evals = bundle.evaluations || [];
      const pays = bundle.payments || [];
      if (evals.length) host.appendChild(accordion("ارزیابی‌ها", (b) => renderJsonSection(b, evals)));
      if (pays.length) host.appendChild(accordion("پرداخت‌ها", (b) => renderJsonSection(b, pays)));
    }

    host.appendChild(accordion("داده کامل (JSON)", (b) => renderJsonSection(b, bundle)));
  }

  async function selectCase(caseId) {
    if (!caseId || !canAccessRegistry()) return;
    state.selectedId = caseId;
    renderTable();

    const host = qs("#registryDetailHost");
    if (host) {
      host.classList.remove("hidden");
      host.innerHTML = '<div class="muted">در حال بارگذاری جزئیات…</div>';
    }
    qs("#registryDetailEmpty")?.classList.add("hidden");

    try {
      state.detail = await fetchDetailBundle(caseId);
      renderDetail(state.detail);
    } catch (e) {
      if (host) host.innerHTML = "";
      const err = el("div", "alert alert--error", e.message || String(e));
      host?.appendChild(err);
    }
  }

  function setModule(module) {
    state.module = module === "guarantee" ? "guarantee" : "investment";
    state.selectedId = null;
    state.cases = [];
    qsa(".registry-module-tab").forEach((btn) => {
      btn.classList.toggle("is-active", btn.dataset.registryModule === state.module);
    });
    fillFilterSelects();
    renderTable();
    renderDetailEmpty();
  }

  function wire() {
    qs("#registrySearch")?.addEventListener("click", () => void loadList());

    qsa(".registry-module-tab").forEach((btn) => {
      btn.addEventListener("click", () => {
        setModule(btn.dataset.registryModule || "investment");
      });
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateAccessUi();
      if (canAccessRegistry() && qs("#tabCaseRegistry")?.classList.contains("is-active")) {
        void loadList();
      }
    });

    document.querySelector('[data-tab="tabCaseRegistry"]')?.addEventListener("click", () => {
      updateAccessUi();
      if (canAccessRegistry() && !state.cases.length) void loadList();
    });
  }

  window.initCasesRegistry = function initCasesRegistry(panel) {
    state.panel = panel;
    wire();
    fillFilterSelects();
    updateAccessUi();
  };

  window.refreshCasesRegistryAccess = updateAccessUi;
})();
