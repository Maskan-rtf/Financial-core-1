(function () {
  const state = {
    panel: null,
    rows: [],
    total: 0,
    skip: 0,
    take: 25,
    caseType: "",
    caseId: "",
    busy: false,
  };

  const CASE_TYPES = [
    { value: "", label: "همه انواع" },
    { value: "Investment", label: "سرمایه‌گذاری" },
    { value: "Loan", label: "تسهیلات" },
    { value: "Guarantee", label: "ضمانت‌نامه" },
  ];

  function qs(sel) {
    return document.querySelector(sel);
  }

  function pick(obj, camel, pascal) {
    if (!obj) return undefined;
    if (obj[camel] !== undefined) return obj[camel];
    if (obj[pascal] !== undefined) return obj[pascal];
    return undefined;
  }

  function resolveSessionRole(session) {
    if (!session) return "";
    if (window.WorkflowModel && typeof window.WorkflowModel.normalizeRole === "function") {
      return window.WorkflowModel.normalizeRole(session.userRoleText, session.userRoleNumber);
    }
    return String(session.userRoleText || "").trim();
  }

  function canViewCommentsReport() {
    return !!state.panel.getActiveSession()?.accessToken;
  }

  function isInternalSession() {
    const role = resolveSessionRole(state.panel.getActiveSession());
    return role && role !== "Applicant";
  }

  function apiPath(suffix) {
    return "/api/v1/audit/case-comments" + (suffix || "");
  }

  function unwrapPaged(body) {
    const payload = state.panel.unwrapEnvelope(body).payload;
    if (Array.isArray(payload)) {
      return { list: payload, total: payload.length };
    }
    const list = pick(payload, "items", "Items") || [];
    const total = pick(payload, "totalCount", "TotalCount") ?? list.length;
    return { list, total };
  }

  function setError(msg) {
    const box = qs("#adminCommentsAuditError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
  }

  function setInfo(msg) {
    const box = qs("#adminCommentsAuditInfo");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
  }

  function updateAccessUi() {
    const access = qs("#adminCommentsAuditAccess");
    const panel = qs("#adminCommentsAuditPanel");
    const nav = qs("#navAdminCommentsAudit");
    const tab = qs("#tabAdminCommentsAudit");
    const loggedIn = canViewCommentsReport();
    const internalCheckbox = qs("#adminCommentsAuditIncludeInternal");

    nav?.classList.toggle("hidden", !loggedIn);
    tab?.classList.toggle("hidden", !loggedIn);

    if (internalCheckbox) {
      internalCheckbox.checked = isInternalSession();
      internalCheckbox.disabled = !isInternalSession();
      internalCheckbox.closest("label")?.classList.toggle("hidden", !isInternalSession());
    }

    if (!loggedIn) {
      access?.classList.remove("hidden");
      if (access) access.textContent = "برای مشاهده گزارش نظرات ابتدا وارد شوید.";
      panel?.classList.add("hidden");
      return;
    }

    access?.classList.add("hidden");
    panel?.classList.remove("hidden");
  }

  function caseTypeLabel(value) {
    const found = CASE_TYPES.find((item) => item.value === value);
    return found ? found.label : value || "—";
  }

  function renderGrid() {
    const host = qs("#adminCommentsAuditGrid");
    if (!host) return;
    host.innerHTML = "";

    if (!state.rows.length) {
      host.textContent = "نظری یافت نشد.";
      return;
    }

    const table = document.createElement("table");
    table.className = "admin-mgmt-grid";
    table.innerHTML =
      "<thead><tr>" +
      "<th>نوع</th><th>پرونده</th><th>مرحله گردش کار</th><th>فاز</th><th>فرستنده</th><th>نقش</th><th>پیام</th><th>زمان</th>" +
      "</tr></thead>";
    const tbody = document.createElement("tbody");

    state.rows.forEach((row) => {
      const tr = document.createElement("tr");
      const createdAt = pick(row, "createdAt", "CreatedAt");
      const sender =
        pick(row, "senderFullName", "SenderFullName") ||
        pick(row, "senderUserId", "SenderUserId") ||
        "—";
      const message = pick(row, "message", "Message") || "—";
      const workflowStep =
        pick(row, "workflowStatusLabel", "WorkflowStatusLabel") ||
        pick(row, "phaseLabel", "PhaseLabel") ||
        pick(row, "phase", "Phase") ||
        "—";
      const phaseLabel = pick(row, "phaseLabel", "PhaseLabel") || pick(row, "phase", "Phase") || "—";
      tr.innerHTML =
        "<td>" +
        caseTypeLabel(String(pick(row, "caseType", "CaseType") || "")) +
        "</td><td class=\"mono\">" +
        (pick(row, "caseNumber", "CaseNumber") || pick(row, "caseId", "CaseId") || "—") +
        "</td><td>" +
        workflowStep +
        (pick(row, "isInternal", "IsInternal") ? " · داخلی" : "") +
        (pick(row, "isRevisionRequest", "IsRevisionRequest") ? " · اصلاح" : "") +
        "</td><td>" +
        phaseLabel +
        "</td><td>" +
        sender +
        "</td><td>" +
        (pick(row, "senderRole", "SenderRole") || "—") +
        "</td><td>" +
        message +
        "</td><td class=\"mono\">" +
        (createdAt ? new Date(createdAt).toLocaleString("fa-IR") : "—") +
        "</td>";
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    host.appendChild(table);

    const meta = qs("#adminCommentsAuditMeta");
    if (meta) meta.textContent = "نمایش " + state.rows.length + " از " + state.total + " نظر";
  }

  async function loadList() {
    setError("");
    const includeInternal = isInternalSession() && qs("#adminCommentsAuditIncludeInternal")?.checked !== false;
    const query = new URLSearchParams({
      take: String(state.take),
      skip: String(state.skip),
      includeInternal: includeInternal ? "true" : "false",
    });
    if (state.caseType) query.set("caseType", state.caseType);
    if (state.caseId) {
      const res = await state.panel.apiRequest({
        method: "GET",
        path: apiPath("/" + encodeURIComponent(state.caseType || "Investment") + "/" + encodeURIComponent(state.caseId)) +
          "?includeInternal=" + (includeInternal ? "true" : "false"),
      });
      const payload = state.panel.unwrapEnvelope(res.body).payload;
      const list = pick(payload, "items", "Items") || [];
      state.rows = list;
      state.total = pick(payload, "totalCount", "TotalCount") ?? list.length;
    } else {
      const res = await state.panel.apiRequest({
        method: "GET",
        path: apiPath("?" + query.toString()),
      });
      const { list, total } = unwrapPaged(res.body);
      state.rows = list;
      state.total = total;
    }
    renderGrid();
    setInfo("فهرست نظرات بارگذاری شد.");
  }

  function wire() {
    const typeSelect = qs("#adminCommentsAuditCaseType");
    if (typeSelect) {
      typeSelect.innerHTML = CASE_TYPES.map(
        (item) => '<option value="' + item.value + '">' + item.label + "</option>"
      ).join("");
      typeSelect.addEventListener("change", () => {
        state.caseType = typeSelect.value;
      });
    }

    qs("#adminCommentsAuditLoad")?.addEventListener("click", () => {
      state.caseType = qs("#adminCommentsAuditCaseType")?.value || "";
      state.caseId = (qs("#adminCommentsAuditCaseId")?.value || "").trim();
      state.skip = 0;
      void loadList().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminCommentsAuditPrevPage")?.addEventListener("click", () => {
      if (state.caseId) return;
      state.skip = Math.max(0, state.skip - state.take);
      void loadList().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminCommentsAuditNextPage")?.addEventListener("click", () => {
      if (state.caseId) return;
      if (state.skip + state.take < state.total) {
        state.skip += state.take;
        void loadList().catch((e) => setError(e.message || String(e)));
      }
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateAccessUi();
      if (!canViewCommentsReport()) {
        state.rows = [];
        renderGrid();
      }
    });

    document.querySelector('[data-tab="tabAdminCommentsAudit"]')?.addEventListener("click", () => {
      updateAccessUi();
      if (canViewCommentsReport() && !state.rows.length) {
        void loadList().catch((e) => setError(e.message || String(e)));
      }
    });
  }

  window.initAdminCommentsAudit = function initAdminCommentsAudit(panel) {
    state.panel = panel;
    wire();
    updateAccessUi();
  };
})();
