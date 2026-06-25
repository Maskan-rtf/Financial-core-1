(function () {
  const state = {
    panel: null,
    rows: [],
    total: 0,
    skip: 0,
    take: 25,
    editingId: null,
    busy: false,
  };

  const PRIVILEGED_ROLES = new Set([
    "Admin",
    "CEO",
    "Ceo",
    "TechnicalExpert",
    "TechnicalManager",
    "InvestmentManager",
  ]);

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

  function canManageCompanies() {
    return PRIVILEGED_ROLES.has(resolveSessionRole(state.panel.getActiveSession()));
  }

  function apiCompaniesPath(suffix) {
    return "/api/v1/identity/companies" + (suffix || "");
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
    const box = qs("#adminCompaniesError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#adminCompaniesInfo")?.classList.add("hidden");
  }

  function setInfo(msg) {
    const box = qs("#adminCompaniesInfo");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#adminCompaniesError")?.classList.add("hidden");
  }

  function updateAccessUi() {
    const access = qs("#adminCompaniesAccess");
    const panel = qs("#adminCompaniesPanel");
    const nav = qs("#navAdminCompanies");
    const tab = qs("#tabAdminCompanies");
    const loggedIn = !!state.panel.getActiveSession()?.accessToken;
    const allowed = canManageCompanies();

    nav?.classList.toggle("hidden", !allowed);
    tab?.classList.toggle("hidden", !allowed);

    if (!loggedIn || !allowed) {
      access?.classList.remove("hidden");
      if (access) {
        access.textContent = loggedIn
          ? "دسترسی به مدیریت شرکت‌ها فقط برای مدیر سیستم، مدیرعامل، هیئت مدیره و کارشناسان فنی مجاز است."
          : "برای مشاهده شرکت‌ها ابتدا وارد شوید.";
      }
      panel?.classList.add("hidden");
      return;
    }

    access?.classList.add("hidden");
    panel?.classList.remove("hidden");
  }

  function clearForm() {
    state.editingId = null;
    const fields = [
      "adminCompanyName",
      "adminCompanyEconomicCode",
      "adminCompanyRegistrationNumber",
      "adminCompanyNationalId",
      "adminCompanyPhone",
      "adminCompanyAddress",
      "adminCompanyCity",
      "adminCompanyProvince",
      "adminCompanyPostalCode",
    ];
    fields.forEach((id) => {
      const el = qs("#" + id);
      if (el) el.value = "";
    });
    qs("#adminCompanyCancelEdit")?.classList.add("hidden");
    const saveBtn = qs("#adminCompanySave");
    if (saveBtn) saveBtn.textContent = "ذخیره شرکت";
    const ownerMeta = qs("#adminCompanyOwnerMeta");
    if (ownerMeta) ownerMeta.textContent = "";
  }

  function beginEdit(row) {
    const id = pick(row, "id", "Id");
    if (!id) return;
    state.editingId = id;
    qs("#adminCompanyName").value = pick(row, "name", "Name") || "";
    qs("#adminCompanyEconomicCode").value = pick(row, "economicCode", "EconomicCode") || "";
    qs("#adminCompanyRegistrationNumber").value = pick(row, "registrationNumber", "RegistrationNumber") || "";
    qs("#adminCompanyNationalId").value = pick(row, "nationalId", "NationalId") || "";
    qs("#adminCompanyPhone").value = pick(row, "phoneNumber", "PhoneNumber") || "";
    qs("#adminCompanyAddress").value = pick(row, "address", "Address") || "";
    qs("#adminCompanyCity").value = pick(row, "city", "City") || "";
    qs("#adminCompanyProvince").value = pick(row, "province", "Province") || "";
    qs("#adminCompanyPostalCode").value = pick(row, "postalCode", "PostalCode") || "";
    const ownerMeta = qs("#adminCompanyOwnerMeta");
    if (ownerMeta) {
      const ownerName = pick(row, "ownerFullName", "OwnerFullName") || "—";
      const ownerPhone = pick(row, "ownerPhoneNumber", "OwnerPhoneNumber") || "—";
      ownerMeta.textContent = "مالک: " + ownerName + " — " + ownerPhone;
    }
    qs("#adminCompanyCancelEdit")?.classList.remove("hidden");
    const saveBtn = qs("#adminCompanySave");
    if (saveBtn) saveBtn.textContent = "ذخیره تغییرات";
    qs("#adminCompaniesEditForm")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    setInfo("در حال ویرایش شرکت — پس از تغییر، «ذخیره تغییرات» را بزنید.");
  }

  function renderGrid() {
    const host = qs("#adminCompaniesGrid");
    if (!host) return;
    host.innerHTML = "";

    if (!state.rows.length) {
      host.appendChild(document.createTextNode("شرکتی یافت نشد."));
      return;
    }

    const table = document.createElement("table");
    table.className = "admin-mgmt-grid";
    table.innerHTML =
      "<thead><tr>" +
      "<th>نام</th><th>کد اقتصادی</th><th>شناسه ملی</th><th>شهر</th><th>مالک</th><th>عملیات</th>" +
      "</tr></thead>";
    const tbody = document.createElement("tbody");

    state.rows.forEach((row) => {
      const id = pick(row, "id", "Id");
      const tr = document.createElement("tr");
      if (state.editingId === id) tr.classList.add("is-selected");

      tr.innerHTML =
        "<td>" +
        (pick(row, "name", "Name") || "—") +
        "</td><td class=\"mono\">" +
        (pick(row, "economicCode", "EconomicCode") || "—") +
        "</td><td class=\"mono\">" +
        (pick(row, "nationalId", "NationalId") || "—") +
        "</td><td>" +
        (pick(row, "city", "City") || "—") +
        "</td><td>" +
        (pick(row, "ownerFullName", "OwnerFullName") || pick(row, "ownerPhoneNumber", "OwnerPhoneNumber") || "—") +
        "</td>";

      const actionsTd = document.createElement("td");
      const actions = document.createElement("div");
      actions.className = "admin-mgmt-grid__actions";

      const editBtn = document.createElement("button");
      editBtn.type = "button";
      editBtn.className = "btn btn--small";
      editBtn.textContent = "ویرایش";
      editBtn.addEventListener("click", () => beginEdit(row));

      const deleteBtn = document.createElement("button");
      deleteBtn.type = "button";
      deleteBtn.className = "btn btn--small btn--warn";
      deleteBtn.textContent = "حذف";
      deleteBtn.addEventListener("click", () => {
        void deleteCompany(id, row).catch((e) => setError(e.message || String(e)));
      });

      actions.appendChild(editBtn);
      actions.appendChild(deleteBtn);
      actionsTd.appendChild(actions);
      tr.appendChild(actionsTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    host.appendChild(table);

    const meta = qs("#adminCompaniesMeta");
    if (meta) meta.textContent = "نمایش " + state.rows.length + " از " + state.total + " شرکت";
  }

  function readFormValues() {
    return {
      name: (qs("#adminCompanyName")?.value || "").trim(),
      economicCode: (qs("#adminCompanyEconomicCode")?.value || "").trim(),
      registrationNumber: (qs("#adminCompanyRegistrationNumber")?.value || "").trim() || null,
      nationalId: (qs("#adminCompanyNationalId")?.value || "").trim() || null,
      phoneNumber: (qs("#adminCompanyPhone")?.value || "").trim() || null,
      address: (qs("#adminCompanyAddress")?.value || "").trim() || null,
      city: (qs("#adminCompanyCity")?.value || "").trim() || null,
      province: (qs("#adminCompanyProvince")?.value || "").trim() || null,
      postalCode: (qs("#adminCompanyPostalCode")?.value || "").trim() || null,
    };
  }

  function validateForm(values) {
    if (!values.name) throw new Error("نام شرکت الزامی است.");
    if (!values.economicCode) throw new Error("کد اقتصادی الزامی است.");
  }

  async function loadList() {
    setError("");
    const res = await state.panel.apiRequest({
      method: "GET",
      path: apiCompaniesPath("?take=" + state.take + "&skip=" + state.skip),
    });
    const { list, total } = unwrapPaged(res.body);
    state.rows = list;
    state.total = total;
    renderGrid();
    setInfo("فهرست شرکت‌ها بارگذاری شد.");
  }

  async function saveCompany() {
    if (!state.editingId) {
      setError("ابتدا یک شرکت را برای ویرایش انتخاب کنید.");
      return;
    }

    const values = readFormValues();
    validateForm(values);

    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "PUT",
        path: apiCompaniesPath("/" + state.editingId),
        body: values,
      });
      clearForm();
      await loadList();
      setInfo("اطلاعات شرکت به‌روزرسانی شد.");
    } finally {
      state.busy = false;
    }
  }

  async function deleteCompany(id, row) {
    if (!id) return;
    const name = pick(row, "name", "Name") || id;
    if (!window.confirm("آیا از حذف شرکت «" + name + "» مطمئن هستید؟")) return;
    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({ method: "DELETE", path: apiCompaniesPath("/" + id) });
      if (state.editingId === id) clearForm();
      await loadList();
      setInfo("شرکت حذف شد.");
    } finally {
      state.busy = false;
    }
  }

  function wire() {
    qs("#adminCompaniesLoad")?.addEventListener("click", () => {
      void loadList().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminCompaniesPrevPage")?.addEventListener("click", () => {
      state.skip = Math.max(0, state.skip - state.take);
      void loadList().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminCompaniesNextPage")?.addEventListener("click", () => {
      if (state.skip + state.take < state.total) {
        state.skip += state.take;
        void loadList().catch((e) => setError(e.message || String(e)));
      }
    });

    qs("#adminCompanySave")?.addEventListener("click", () => {
      void saveCompany().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminCompanyCancelEdit")?.addEventListener("click", () => {
      clearForm();
      setInfo("ویرایش لغو شد.");
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateAccessUi();
      if (!canManageCompanies()) {
        state.rows = [];
        clearForm();
        renderGrid();
      }
    });

    document.querySelector('[data-tab="tabAdminCompanies"]')?.addEventListener("click", () => {
      updateAccessUi();
      if (canManageCompanies() && !state.rows.length) {
        void loadList().catch((e) => setError(e.message || String(e)));
      }
    });
  }

  window.initAdminCompanies = function initAdminCompanies(panel) {
    state.panel = panel;
    wire();
    updateAccessUi();
  };

  window.refreshAdminCompaniesAccess = updateAccessUi;
})();
