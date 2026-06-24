/* global UIComponents, WorkflowModel, GuaranteeWorkflowModel, LoanWorkflowModel */
(function () {
  const state = {
    panel: null,
    module: "investment",
    cases: [],
    detailId: null,
    subTab: "workflow",
  };

  const MODULES = {
    investment: {
      apiBase: (p) => p.casesBasePath(),
      setId: (p, id) => p.setCurrentCaseId(id, "investment"),
      root: "#investmentPortalRoot",
    },
    guarantee: {
      apiBase: (p) => p.guaranteeCasesBasePath(),
      setId: (p, id) => p.setGuaranteeCaseId(id),
      root: "#guaranteePortalRoot",
    },
    loan: {
      apiBase: (p) => p.loanCasesBasePath(),
      setId: (p, id) => p.setLoanCaseId(id),
      root: "#loanPortalRoot",
    },
  };

  const qs = (sel) => document.querySelector(sel);
  const qsa = (sel) => Array.from(document.querySelectorAll(sel));

  function pick(obj, camel, pascal) {
    return UIComponents.pick(obj, camel, pascal);
  }

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function apiBase() {
    return MODULES[state.module].apiBase(state.panel);
  }

  function resolveRole() {
    const session = state.panel.getActiveSession();
    if (!session) return "";
    if (window.WorkflowModel) return WorkflowModel.normalizeRole(session.userRoleText, session.userRoleNumber);
    return String(session.userRoleText || "");
  }

  function isApplicant() {
    return resolveRole() === "Applicant";
  }

  function isInternal() {
    return WorkflowModel && WorkflowModel.isInternalRole(resolveRole());
  }

  function caseSubject(c) {
    return UIComponents.caseSubjectFromCase(state.module, c);
  }

  function applicantName(c) {
    return UIComponents.applicantLabelFromCase(c);
  }

  function companyLabel(c) {
    return UIComponents.companySummary(c);
  }

  function normalizeList(body) {
    const payload = unwrap(body);
    if (Array.isArray(payload)) return payload;
    if (payload && Array.isArray(payload.items)) return payload.items;
    if (payload && Array.isArray(payload.Items)) return payload.Items;
    return [];
  }

  function setListError(msg) {
    const box = qs("#casesListError");
    if (!box) return;
    if (!msg) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.textContent = msg;
    box.classList.remove("hidden");
  }

  function showListView() {
    state.detailId = null;
    qs("#casesListView")?.classList.remove("hidden");
    qs("#casesDetailView")?.classList.add("hidden");
  }

  function showDetailView() {
    qs("#casesListView")?.classList.add("hidden");
    qs("#casesDetailView")?.classList.remove("hidden");
  }

  function syncModuleUi() {
    qsa(".cases-module-tab").forEach((btn) => {
      btn.classList.toggle("is-active", btn.dataset.module === state.module);
    });
    Object.keys(MODULES).forEach((key) => {
      const root = qs(MODULES[key].root);
      if (root) root.classList.toggle("hidden", key !== state.module);
    });
    const showCreate = isApplicant();
    qs("#casesCreateInvestment")?.classList.toggle("hidden", !showCreate || state.module !== "investment");
    qs("#casesCreateGuarantee")?.classList.toggle("hidden", !showCreate || state.module !== "guarantee");
    qs("#casesCreateLoan")?.classList.toggle("hidden", !showCreate || state.module !== "loan");
  }

  async function loadCases() {
    if (!state.panel.getActiveSession()?.accessToken) {
      setListError("برای مشاهده پرونده‌ها ابتدا وارد شوید.");
      state.cases = [];
      renderList();
      return;
    }
    setListError("");
    qs("#casesListLoading")?.classList.remove("hidden");
    try {
      const session = state.panel.getActiveSession();
      const query = new URLSearchParams({ skip: "0", take: "50" });
      if (isApplicant() && session.userId) query.set("applicantUserId", session.userId);
      const res = await state.panel.apiRequest({
        method: "GET",
        path: apiBase() + "?" + query.toString(),
      });
      state.cases = normalizeList(res.body);
      renderList();
    } catch (e) {
      setListError(e.message || String(e));
      state.cases = [];
      renderList();
    } finally {
      qs("#casesListLoading")?.classList.add("hidden");
    }
  }

  function renderList() {
    const host = qs("#casesTableHost");
    if (!host) return;
    const rows = state.cases.map((c) => ({
      id: pick(c, "id", "Id"),
      caseNumber: pick(c, "caseNumber", "CaseNumber"),
      subject: caseSubject(c),
      company: companyLabel(c),
      applicant: applicantName(c),
      statusLabel: UIComponents.statusTitle(
        state.module,
        pick(c, "currentStatus", "CurrentStatus"),
        { case: c }
      ),
    }));
    UIComponents.renderCaseTable(host, rows, {
      emptyText: "پرونده‌ای برای این ماژول یافت نشد.",
      onEnter: (row) => openCase(state.module, row.id),
    });
    const meta = qs("#casesListMeta");
    if (meta) meta.textContent = rows.length ? rows.length + " پرونده" : "";
  }

  function setSubTab(tab) {
    state.subTab = tab;
    qsa(".case-subtab").forEach((btn) => btn.classList.toggle("is-active", btn.dataset.subtab === tab));
    qs("#caseSubWorkflow")?.classList.toggle("hidden", tab !== "workflow");
    qs("#caseSubAttachments")?.classList.toggle("hidden", tab !== "attachments");
    qs("#caseSubHistory")?.classList.toggle("hidden", tab !== "history");
    if (tab === "attachments") void renderAttachmentsPanel();
    if (tab === "history") void renderHistoryPanel();
  }

  async function renderAttachmentsPanel() {
    const host = qs("#caseAttachmentsHost");
    if (!host || !state.detailId) return;
    host.innerHTML = '<div class="muted">در حال بارگذاری…</div>';
    try {
      const res = await state.panel.apiRequest({
        method: "GET",
        path: apiBase() + "/" + state.detailId + "/documents",
      });
      UIComponents.renderAttachments(host, unwrap(res.body) || [], {
        module: state.module,
        onDownload: (docId) => downloadDocument(docId),
      });
    } catch (e) {
      host.innerHTML = "";
      host.appendChild(UIComponents.el("div", "alert alert--error", e.message || String(e)));
    }
  }

  async function downloadDocument(docId) {
    if (state.module === "investment") {
      const res = await state.panel.apiRequest({
        method: "GET",
        path: apiBase() + "/" + state.detailId + "/documents/" + encodeURIComponent(docId) + "/download?presign=true",
      });
      const body = unwrap(res.body) || res.body;
      const url = body.url || body.Url;
      if (url) window.open(url, "_blank", "noopener");
      return;
    }

    const session = state.panel.getActiveSession();
    const headers = {};
    if (session?.accessToken) headers.Authorization = "Bearer " + session.accessToken;
    const url = state.panel.makeUrl(
      apiBase() + "/" + state.detailId + "/documents/" + encodeURIComponent(docId) + "/download"
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

  async function renderHistoryPanel() {
    const host = qs("#caseHistoryHost");
    if (!host || !state.detailId) return;
    host.innerHTML = '<div class="muted">در حال بارگذاری…</div>';
    try {
      const includeInternal = isInternal() ? "true" : "false";
      const [histRes, commRes] = await Promise.all([
        state.panel.apiRequest({ method: "GET", path: apiBase() + "/" + state.detailId + "/history" }),
        state.panel.apiRequest({
          method: "GET",
          path: apiBase() + "/" + state.detailId + "/comments?includeInternal=" + includeInternal,
        }),
      ]);
      UIComponents.renderTimeline(host, unwrap(histRes.body) || [], unwrap(commRes.body) || [], {
        module: state.module,
        onAddComment: (message, done) => {
          void state.panel
            .apiRequest({
              method: "POST",
              path: apiBase() + "/" + state.detailId + "/comments",
              body: { phase: 1, message, isInternal: isInternal(), parentId: null },
            })
            .then(() => {
              done();
              return renderHistoryPanel();
            })
            .catch((e) => alert(e.message));
        },
      });
    } catch (e) {
      host.innerHTML = "";
      host.appendChild(UIComponents.el("div", "alert alert--error", e.message || String(e)));
    }
  }

  function openCase(module, caseId) {
    if (!caseId) return;
    state.module = module || state.module;
    state.detailId = caseId;
    const row = state.cases.find((c) => pick(c, "id", "Id") === caseId);
    syncModuleUi();
    MODULES[state.module].setId(state.panel, caseId);
    state.panel.setCaseModule(state.module);
    showDetailView();
    setSubTab("workflow");
    const numEl = qs("#caseDetailNumber");
    if (numEl) {
      const caseNumber = row ? pick(row, "caseNumber", "CaseNumber") || caseId : caseId;
      const title = row ? pick(row, "title", "Title") : "";
      numEl.textContent = title ? title + " · " + caseNumber : caseNumber;
    }
    const companyEl = qs("#caseDetailCompany");
    if (companyEl) {
      const companyText = row ? companyLabel(row) : "—";
      companyEl.textContent = companyText;
      companyEl.classList.toggle("hidden", companyText === "—");
    }
    document.dispatchEvent(
      new CustomEvent("testpanel:case-changed", { detail: { caseId, module: state.module } })
    );
  }

  async function loadCompanies(selectId) {
    const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/identity/companies/mine" });
    const list = unwrap(res.body) || [];
    const sel = qs(selectId);
    if (!sel) return;
    sel.innerHTML = "";
    list.forEach((c) => {
      const opt = document.createElement("option");
      opt.value = pick(c, "id", "Id");
      opt.textContent = pick(c, "name", "Name");
      sel.appendChild(opt);
    });
  }

  function optionalTitle(selector) {
    const value = qs(selector)?.value?.trim();
    return value || null;
  }

  async function createInvestmentCase() {
    const applicantType = Number(qs("#caseApplicantTypeHub")?.value || 1);
    const payload = { applicantType, title: optionalTitle("#caseTitleHub") };
    if (applicantType === 2) {
      const companyId = qs("#caseCompanyIdHub")?.value?.trim();
      if (!companyId) throw new Error("شرکت را انتخاب کنید.");
      payload.companyId = companyId;
    }
    const res = await state.panel.apiRequest({ method: "POST", path: state.panel.casesBasePath(), body: payload });
    openCase("investment", pick(unwrap(res.body), "id", "Id"));
  }

  async function createGuaranteeCase() {
    const applicantType = Number(qs("#gApplicantTypeHub")?.value || 1);
    const payload = { applicantType, title: optionalTitle("#gCaseTitleHub") };
    if (applicantType === 2) {
      const companyId = qs("#gCompanyIdHub")?.value?.trim();
      if (!companyId) throw new Error("شرکت را انتخاب کنید.");
      payload.companyId = companyId;
    }
    const res = await state.panel.apiRequest({ method: "POST", path: state.panel.guaranteeCasesBasePath(), body: payload });
    openCase("guarantee", pick(unwrap(res.body), "id", "Id"));
  }

  async function createLoanCase() {
    const applicantType = Number(qs("#lApplicantTypeHub")?.value || 1);
    const payload = { applicantType, companyId: null, title: optionalTitle("#lCaseTitleHub") };
    if (applicantType === 2) {
      const companyId = qs("#lCompanyIdHub")?.value?.trim();
      if (!companyId) throw new Error("شرکت را انتخاب کنید.");
      payload.companyId = companyId;
    }
    const res = await state.panel.apiRequest({ method: "POST", path: state.panel.loanCasesBasePath(), body: payload });
    openCase("loan", pick(unwrap(res.body), "id", "Id"));
  }

  function wireCreateCase() {
    qs("#casesRefreshList")?.addEventListener("click", () => loadCases());
    qsa(".cases-module-tab").forEach((btn) => {
      btn.addEventListener("click", () => {
        state.module = btn.dataset.module;
        syncModuleUi();
        if (!state.detailId) loadCases();
      });
    });
    qs("#caseDetailBack")?.addEventListener("click", () => {
      showListView();
      loadCases();
    });
    qsa(".case-subtab").forEach((btn) => {
      btn.addEventListener("click", () => setSubTab(btn.dataset.subtab));
    });
    qs("#btnCreateCaseHub")?.addEventListener("click", () => void createInvestmentCase().catch((e) => setListError(e.message)));
    qs("#gCreateCaseHub")?.addEventListener("click", () => void createGuaranteeCase().catch((e) => setListError(e.message)));
    qs("#lCreateCaseHub")?.addEventListener("click", () => void createLoanCase().catch((e) => setListError(e.message)));
    qs("#gLoadCompaniesHub")?.addEventListener("click", () => void loadCompanies("#gCompanyIdHub"));
    qs("#lLoadCompaniesHub")?.addEventListener("click", () => void loadCompanies("#lCompanyIdHub"));
    qs("#btnLoadCompaniesHub")?.addEventListener("click", () => void loadCompanies("#caseCompanyIdHub"));
    qs("#caseApplicantTypeHub")?.addEventListener("change", () => {
      qs("#caseCompanyRowHub")?.classList.toggle("hidden", qs("#caseApplicantTypeHub")?.value !== "2");
    });
    qs("#gApplicantTypeHub")?.addEventListener("change", () => {
      qs("#gCompanyRowHub")?.classList.toggle("hidden", qs("#gApplicantTypeHub")?.value !== "2");
    });
    qs("#lApplicantTypeHub")?.addEventListener("change", () => {
      qs("#lCompanyRowHub")?.classList.toggle("hidden", qs("#lApplicantTypeHub")?.value !== "2");
    });
    document.addEventListener("testpanel:session-changed", () => {
      syncModuleUi();
      if (!state.detailId) loadCases();
    });
  }

  function updateDashboardNav() {
    const role = resolveRole();
    const show =
      ["CEO", "Admin", "InvestmentManager", "FinancialManager", "Applicant"].includes(role) ||
      [
        "InvestmentExpert",
        "LegalExpert",
        "LegalManager",
        "FinancialExpert",
        "CreditExpert",
        "CreditManager",
        "TechnicalExpert",
        "TechnicalManager",
      ].includes(role);
    qs("#navDashboard")?.classList.toggle("hidden", !show);
  }

  window.CasesHub = { openCase, loadCases, getModule: () => state.module, setSubTab };

  window.initCasesHub = function initCasesHub(panel) {
    state.panel = panel;
    syncModuleUi();
    wireCreateCase();
    updateDashboardNav();
    document.addEventListener("testpanel:session-changed", updateDashboardNav);
    loadCases();
  };
})();
