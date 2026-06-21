(function () {
  const state = {
    panel: null,
    rows: [],
    total: 0,
    skip: 0,
    take: 20,
    busy: false,
    editingId: null,
  };

  const MODULES = [
    { value: 1, label: "ضمانت‌نامه" },
    { value: 2, label: "تسهیلات" },
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

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function unwrapPaged(body) {
    const payload = unwrap(body);
    if (Array.isArray(payload)) {
      return { list: payload, total: payload.length, skip: 0, take: payload.length };
    }
    const list = pick(payload, "items", "Items") || [];
    const total = pick(payload, "totalCount", "TotalCount") ?? list.length;
    const skip = pick(payload, "skip", "Skip") ?? 0;
    const take = pick(payload, "take", "Take") ?? pick(payload, "pageSize", "PageSize") ?? state.take;
    return { list, total, skip, take };
  }

  function readTake() {
    const raw = Number(qs("#fundCreditPageSize")?.value || state.take);
    return Number.isFinite(raw) && raw > 0 ? raw : 20;
  }

  function updatePagerMeta() {
    const meta = qs("#fundCreditLimitsMeta");
    if (!meta) return;
    const currentPage = state.take > 0 ? Math.floor(state.skip / state.take) + 1 : 1;
    const totalPages = state.take > 0 ? Math.max(1, Math.ceil(state.total / state.take)) : 1;
    meta.textContent =
      "صفحه " +
      currentPage +
      " از " +
      totalPages +
      " — نمایش " +
      state.rows.length +
      " از " +
      state.total +
      " سقف";
  }

  function apiPath(suffix) {
    return "/api/v" + (window.TESTPANEL_CONFIG?.casesVersion || "1") + "/fund-credit-limits" + (suffix || "");
  }

  function resolveSessionRole(session) {
    if (!session) return "";
    if (window.WorkflowModel && typeof window.WorkflowModel.normalizeRole === "function") {
      return window.WorkflowModel.normalizeRole(session.userRoleText, session.userRoleNumber);
    }
    return String(session.userRoleText || "").trim();
  }

  function canManageFundCreditLimits() {
    const role = resolveSessionRole(state.panel.getActiveSession());
    return ["CEO", "Admin", "TechnicalExpert"].includes(role);
  }

  function formatRial(value) {
    if (value == null || value === "") return "—";
    const n = Number(value);
    if (!Number.isFinite(n)) return String(value);
    return n.toLocaleString("fa-IR") + " ریال";
  }

  function moduleLabel(moduleType) {
    const n = Number(moduleType);
    if (n === 1) return "ضمانت‌نامه";
    if (n === 2) return "تسهیلات";
    return String(moduleType ?? "—");
  }

  function setError(msg) {
    const box = qs("#fundCreditLimitsError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#fundCreditLimitsInfo")?.classList.add("hidden");
  }

  function setInfo(msg) {
    const box = qs("#fundCreditLimitsInfo");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#fundCreditLimitsError")?.classList.add("hidden");
  }

  function clearForm() {
    const moduleSelect = qs("#fundCreditModuleType");
    if (moduleSelect) {
      moduleSelect.value = moduleSelect.options[0]?.value || "";
      moduleSelect.disabled = false;
    }
    const periodStart = qs("#fundCreditPeriodStart");
    if (periodStart) periodStart.value = "";
    const expiresAt = qs("#fundCreditExpiresAt");
    if (expiresAt) expiresAt.value = "";
    const amount = qs("#fundCreditAmount");
    if (amount) amount.value = "";
    state.editingId = null;
    const saveBtn = qs("#fundCreditSaveNew");
    if (saveBtn) saveBtn.textContent = "ثبت سقف دوره‌ای";
    qs("#fundCreditCancelEdit")?.classList.add("hidden");
  }

  function beginEdit(row) {
    const id = pick(row, "id", "Id");
    if (!id) return;

    state.editingId = id;
    const moduleSelect = qs("#fundCreditModuleType");
    if (moduleSelect) {
      moduleSelect.value = String(pick(row, "moduleType", "ModuleType") || "");
      moduleSelect.disabled = true;
    }
    const periodStart = qs("#fundCreditPeriodStart");
    if (periodStart) periodStart.value = pick(row, "periodStart", "PeriodStart") || "";
    const expiresAt = qs("#fundCreditExpiresAt");
    if (expiresAt) expiresAt.value = pick(row, "expiresAt", "ExpiresAt") || "";
    const amount = qs("#fundCreditAmount");
    if (amount) amount.value = String(pick(row, "creditLimitWithCheck", "CreditLimitWithCheck") || "");

    const saveBtn = qs("#fundCreditSaveNew");
    if (saveBtn) saveBtn.textContent = "ذخیره تغییرات";
    qs("#fundCreditCancelEdit")?.classList.remove("hidden");
    qs("#fundCreditLimitsForm")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    setInfo("در حال ویرایش سقف دوره‌ای — پس از تغییر، «ذخیره تغییرات» را بزنید.");
  }

  function updateAccessUi() {
    const access = qs("#fundCreditLimitsAccess");
    const panel = qs("#fundCreditLimitsPanel");
    const loggedIn = !!state.panel.getActiveSession()?.accessToken;
    const allowed = canManageFundCreditLimits();

    if (!loggedIn) {
      access?.classList.remove("hidden");
      if (access) access.textContent = "برای مدیریت سقف اعتبار دوره‌ای ابتدا وارد شوید.";
      panel?.classList.add("hidden");
      return;
    }

    if (!allowed) {
      access?.classList.remove("hidden");
      if (access) access.textContent = "دسترسی به سقف اعتبار دوره‌ای فقط برای مدیرعامل، کارشناس فنی و مدیر سیستم مجاز است.";
      panel?.classList.add("hidden");
      return;
    }

    access?.classList.add("hidden");
    panel?.classList.remove("hidden");
  }

  function renderGrid() {
    const host = qs("#fundCreditLimitsGrid");
    if (!host) return;
    host.innerHTML = "";

    if (!state.rows.length) {
      host.appendChild(document.createTextNode("هنوز سقف دوره‌ای ثبت نشده است."));
      updatePagerMeta();
      return;
    }

    const table = document.createElement("table");
    table.className = "fund-credit-grid";
    table.innerHTML =
      "<thead><tr>" +
      "<th>ماژول</th><th>شروع</th><th>پایان</th><th>سقف</th><th>مصرف</th><th>مانده</th><th>ثبت‌کننده</th><th>عملیات</th>" +
      "</tr></thead>";
    const tbody = document.createElement("tbody");

    state.rows.forEach((row) => {
      const tr = document.createElement("tr");
      const id = pick(row, "id", "Id");
      const utilized = Number(pick(row, "totalUtilized", "TotalUtilized") || 0);
      const registrar =
        pick(row, "lastSetByFullName", "LastSetByFullName") ||
        pick(row, "lastSetByUserId", "LastSetByUserId") ||
        "—";

      tr.innerHTML =
        "<td>" +
        moduleLabel(pick(row, "moduleType", "ModuleType")) +
        "</td><td>" +
        (pick(row, "periodStart", "PeriodStart") || "—") +
        "</td><td>" +
        (pick(row, "expiresAt", "ExpiresAt") || "—") +
        "</td><td>" +
        formatRial(pick(row, "creditLimitWithCheck", "CreditLimitWithCheck")) +
        "</td><td>" +
        formatRial(pick(row, "totalUtilized", "TotalUtilized")) +
        "</td><td>" +
        formatRial(pick(row, "remainingCapacity", "RemainingCapacity")) +
        "</td><td class=\"mono\">" +
        registrar +
        "</td>";

      const actionsTd = document.createElement("td");
      const actions = document.createElement("div");
      actions.className = "fund-credit-grid__actions";

      const editBtn = document.createElement("button");
      editBtn.type = "button";
      editBtn.className = "btn btn--small";
      editBtn.textContent = "ویرایش";
      editBtn.addEventListener("click", () => beginEdit(row));

      const deleteBtn = document.createElement("button");
      deleteBtn.type = "button";
      deleteBtn.className = "btn btn--small btn--warn";
      deleteBtn.textContent = "حذف";
      deleteBtn.title = utilized > 0 ? "سقف دارای مصرف است و قابل حذف نیست" : "";
      deleteBtn.disabled = utilized > 0;
      deleteBtn.addEventListener("click", () => {
        void deleteLimit(id, row).catch((e) => setError(e.message || String(e)));
      });

      actions.appendChild(editBtn);
      actions.appendChild(deleteBtn);
      actionsTd.appendChild(actions);
      tr.appendChild(actionsTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    host.appendChild(table);
    updatePagerMeta();
  }

  async function loadList(skip) {
    setError("");
    state.take = readTake();
    state.skip = Number.isFinite(Number(skip)) && Number(skip) >= 0 ? Number(skip) : state.skip;
    const res = await state.panel.apiRequest({
      method: "GET",
      path: apiPath("?skip=" + state.skip + "&take=" + state.take),
    });
    const { list, total, skip: resolvedSkip, take: resolvedTake } = unwrapPaged(res.body);
    state.rows = list;
    state.total = total;
    state.skip = resolvedSkip;
    state.take = resolvedTake;
    renderGrid();
    setInfo("فهرست سقف‌های دوره‌ای بارگذاری شد.");
  }

  function readFormValues() {
    const moduleType = Number(qs("#fundCreditModuleType")?.value || 0);
    const amount = Number(qs("#fundCreditAmount")?.value || 0);
    const periodStart = (qs("#fundCreditPeriodStart")?.value || "").trim();
    const expiresAt = (qs("#fundCreditExpiresAt")?.value || "").trim();
    return { moduleType, amount, periodStart, expiresAt };
  }

  function validateFormValues(values, requireModule) {
    if (requireModule && !values.moduleType) throw new Error("نوع ماژول را انتخاب کنید.");
    if (!Number.isFinite(values.amount) || values.amount <= 0) {
      throw new Error("مبلغ سقف باید عددی بزرگ‌تر از صفر باشد.");
    }
    if (!values.periodStart || !values.expiresAt) throw new Error("تاریخ شروع و پایان دوره الزامی است.");
    if (values.expiresAt < values.periodStart) throw new Error("تاریخ پایان باید بعد از تاریخ شروع باشد.");
  }

  async function saveNewLimit() {
    const values = readFormValues();
    validateFormValues(values, true);

    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "POST",
        path: apiPath(""),
        body: {
          moduleType: values.moduleType,
          creditLimitWithCheck: values.amount,
          periodStart: values.periodStart,
          expiresAt: values.expiresAt,
        },
      });
      clearForm();
      await loadList(state.skip);
      setInfo("سقف اعتبار دوره‌ای جدید ثبت شد.");
    } finally {
      state.busy = false;
    }
  }

  async function updateLimit() {
    if (!state.editingId) return;
    const values = readFormValues();
    validateFormValues(values, false);

    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "PUT",
        path: apiPath("/" + state.editingId),
        body: {
          creditLimitWithCheck: values.amount,
          periodStart: values.periodStart,
          expiresAt: values.expiresAt,
        },
      });
      clearForm();
      await loadList(state.skip);
      setInfo("سقف اعتبار دوره‌ای به‌روزرسانی شد.");
    } finally {
      state.busy = false;
    }
  }

  async function deleteLimit(id, row) {
    if (!id) return;
    const moduleName = moduleLabel(pick(row, "moduleType", "ModuleType"));
    const periodStart = pick(row, "periodStart", "PeriodStart") || "—";
    const expiresAt = pick(row, "expiresAt", "ExpiresAt") || "—";
    const message =
      "آیا از حذف سقف دوره‌ای «" +
      moduleName +
      "» (" +
      periodStart +
      " تا " +
      expiresAt +
      ") مطمئن هستید؟";

    if (!window.confirm(message)) return;

    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "DELETE",
        path: apiPath("/" + id),
      });
      if (state.editingId === id) clearForm();
      const maxSkip = Math.max(0, state.total - state.take);
      const nextSkip = state.skip > maxSkip ? Math.max(0, Math.floor(maxSkip / state.take) * state.take) : state.skip;
      await loadList(nextSkip);
      setInfo("سقف اعتبار دوره‌ای حذف شد.");
    } finally {
      state.busy = false;
    }
  }

  function wire() {
    qs("#fundCreditLoadList")?.addEventListener("click", () => {
      state.skip = 0;
      void loadList(0).catch((e) => setError(e.message || String(e)));
    });

    qs("#fundCreditPrevPage")?.addEventListener("click", () => {
      if (state.skip <= 0) return;
      void loadList(Math.max(0, state.skip - state.take)).catch((e) => setError(e.message || String(e)));
    });

    qs("#fundCreditNextPage")?.addEventListener("click", () => {
      if (state.skip + state.take >= state.total) return;
      void loadList(state.skip + state.take).catch((e) => setError(e.message || String(e)));
    });

    qs("#fundCreditPageSize")?.addEventListener("change", () => {
      state.skip = 0;
      void loadList(0).catch((e) => setError(e.message || String(e)));
    });

    qs("#fundCreditSaveNew")?.addEventListener("click", () => {
      const action = state.editingId ? updateLimit() : saveNewLimit();
      void action.catch((e) => setError(e.message || String(e)));
    });

    qs("#fundCreditCancelEdit")?.addEventListener("click", () => {
      clearForm();
      setInfo("ویرایش لغو شد.");
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateAccessUi();
      if (!canManageFundCreditLimits()) {
        state.rows = [];
        state.total = 0;
        state.skip = 0;
        clearForm();
        renderGrid();
      }
    });

    document.querySelector('[data-tab="tabDashboard"]')?.addEventListener("click", () => {
      updateAccessUi();
      if (canManageFundCreditLimits() && !state.rows.length) {
        void loadList(0).catch((e) => setError(e.message || String(e)));
      }
    });
  }

  window.initFundCreditLimits = function initFundCreditLimits(panel) {
    state.panel = panel;
    const select = qs("#fundCreditModuleType");
    if (select && !select.options.length) {
      MODULES.forEach((m) => {
        const opt = document.createElement("option");
        opt.value = String(m.value);
        opt.textContent = m.label;
        select.appendChild(opt);
      });
    }
    wire();
    updateAccessUi();
  };

  window.refreshFundCreditLimitsAccess = updateAccessUi;
})();
