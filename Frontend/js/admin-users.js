(function () {
  const state = {
    panel: null,
    users: [],
    onlineUsers: [],
    sessions: [],
    total: 0,
    skip: 0,
    take: 25,
    selectedUserId: null,
    editingUserId: null,
    busy: false,
    viewMode: "all",
  };

  const ROLE_OPTIONS = [
    { value: "", label: "(بدون تغییر)" },
    { value: "1", label: "متقاضی (1)" },
    { value: "10", label: "کارشناس سرمایه‌گذاری (10)" },
    { value: "11", label: "مدیر سرمایه‌گذاری (11)" },
    { value: "12", label: "مدیرعامل (12)" },
    { value: "20", label: "کارشناس حقوقی (20)" },
    { value: "21", label: "مدیر حقوقی (21)" },
    { value: "30", label: "کارشناس مالی (30)" },
    { value: "31", label: "مدیر مالی (31)" },
    { value: "40", label: "کارشناس فنی (40)" },
    { value: "41", label: "مدیر فنی (41)" },
    { value: "50", label: "کارشناس اعتبارات (50)" },
    { value: "51", label: "مدیر اعتبارات (51)" },
    { value: "100", label: "مدیر سیستم (100)" },
  ];

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

  function canManageUsers() {
    return PRIVILEGED_ROLES.has(resolveSessionRole(state.panel.getActiveSession()));
  }

  function apiUsersPath(suffix) {
    return "/api/v1/identity/users" + (suffix || "");
  }

  function unwrapList(body) {
    const { envelope, payload } = state.panel.unwrapEnvelope(body);
    const list = Array.isArray(payload) ? payload : [];
    const total = pick(envelope, "totalCount", "TotalCount") ?? list.length;
    return { list, total };
  }

  function unwrapPayload(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function formatDate(value) {
    if (!value) return "—";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return String(value);
    return d.toLocaleString("fa-IR");
  }

  function roleLabel(user) {
    const num = pick(user, "roleNumber", "RoleNumber");
    const role = pick(user, "role", "Role");
    if (num != null) {
      const mapped = window.WorkflowModel?.normalizeRole?.("", num);
      if (mapped) return mapped + " (" + num + ")";
    }
    return role != null ? String(role) : "—";
  }

  function userName(user) {
    const first = pick(user, "firstName", "FirstName") || "";
    const last = pick(user, "lastName", "LastName") || "";
    const full = (first + " " + last).trim();
    return full || pick(user, "phoneNumber", "PhoneNumber") || "—";
  }

  function setError(msg) {
    const box = qs("#adminUsersError");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#adminUsersInfo")?.classList.add("hidden");
  }

  function setInfo(msg) {
    const box = qs("#adminUsersInfo");
    if (!box) return;
    box.classList.toggle("hidden", !msg);
    box.textContent = msg || "";
    if (msg) qs("#adminUsersError")?.classList.add("hidden");
  }

  function updateAccessUi() {
    const access = qs("#adminUsersAccess");
    const panel = qs("#adminUsersPanel");
    const nav = qs("#navAdminUsers");
    const tab = qs("#tabAdminUsers");
    const loggedIn = !!state.panel.getActiveSession()?.accessToken;
    const allowed = canManageUsers();

    nav?.classList.toggle("hidden", !allowed);
    tab?.classList.toggle("hidden", !allowed);

    if (!loggedIn || !allowed) {
      access?.classList.remove("hidden");
      if (access) {
        access.textContent = loggedIn
          ? "دسترسی به مدیریت کاربران فقط برای مدیرعامل، کارشناس فنی و مدیر سیستم مجاز است."
          : "برای مدیریت کاربران ابتدا وارد شوید.";
      }
      panel?.classList.add("hidden");
      return;
    }

    access?.classList.add("hidden");
    panel?.classList.remove("hidden");
  }

  function renderUsersTable() {
    const host = qs("#adminUsersGrid");
    if (!host) return;
    host.innerHTML = "";

    const rows = state.viewMode === "online" ? state.onlineUsers : state.users;

    const meta = qs("#adminUsersMeta");
    if (meta) {
      if (state.viewMode === "online") {
        meta.textContent = rows.length + " کاربر آنلاین";
      } else {
        meta.textContent = "نمایش " + rows.length + " از " + state.total + " کاربر";
      }
    }

    if (!rows.length) {
      host.appendChild(document.createTextNode(state.viewMode === "online" ? "کاربر آنلاینی یافت نشد." : "کاربری یافت نشد."));
      return;
    }

    const table = document.createElement("table");
    table.className = "admin-mgmt-grid";
    table.innerHTML =
      "<thead><tr>" +
      "<th>نام</th><th>موبایل</th><th>نقش</th><th>وضعیت</th><th>نشست‌ها</th><th>آخرین فعالیت</th><th>عملیات</th>" +
      "</tr></thead>";
    const tbody = document.createElement("tbody");

    rows.forEach((user) => {
      const userId = pick(user, "userId", "UserId") || pick(user, "id", "Id");
      const tr = document.createElement("tr");
      if (state.selectedUserId === userId) tr.classList.add("is-selected");

      const isActive = pick(user, "isActive", "IsActive");
      const sessionCount =
        state.viewMode === "online"
          ? pick(user, "activeSessionCount", "ActiveSessionCount") || 0
          : "—";

      tr.innerHTML =
        "<td>" +
        userName(user) +
        "</td><td class=\"mono\">" +
        (pick(user, "phoneNumber", "PhoneNumber") || "—") +
        "</td><td>" +
        roleLabel(user) +
        "</td><td>" +
        (isActive === false ? "غیرفعال" : "فعال") +
        "</td><td>" +
        sessionCount +
        "</td><td>" +
        formatDate(pick(user, "lastActivityAt", "LastActivityAt") || pick(user, "lastLoginAt", "LastLoginAt")) +
        "</td>";

      const actionsTd = document.createElement("td");
      const actions = document.createElement("div");
      actions.className = "admin-mgmt-grid__actions";

      const sessionsBtn = document.createElement("button");
      sessionsBtn.type = "button";
      sessionsBtn.className = "btn btn--small";
      sessionsBtn.textContent = "نشست‌ها";
      sessionsBtn.addEventListener("click", () => {
        void loadUserSessions(userId).catch((e) => setError(e.message || String(e)));
      });

      const kickBtn = document.createElement("button");
      kickBtn.type = "button";
      kickBtn.className = "btn btn--small btn--warn";
      kickBtn.textContent = "اخراج";
      kickBtn.title = "لغو تمام نشست‌های فعال";
      kickBtn.addEventListener("click", () => {
        void kickUser(userId, user).catch((e) => setError(e.message || String(e)));
      });

      const editBtn = document.createElement("button");
      editBtn.type = "button";
      editBtn.className = "btn btn--small";
      editBtn.textContent = "ویرایش";
      editBtn.addEventListener("click", () => beginEditUser(user));

      const deleteBtn = document.createElement("button");
      deleteBtn.type = "button";
      deleteBtn.className = "btn btn--small btn--warn";
      deleteBtn.textContent = "حذف";
      deleteBtn.addEventListener("click", () => {
        void deleteUser(userId, user).catch((e) => setError(e.message || String(e)));
      });

      actions.appendChild(sessionsBtn);
      actions.appendChild(kickBtn);
      actions.appendChild(editBtn);
      actions.appendChild(deleteBtn);
      actionsTd.appendChild(actions);
      tr.appendChild(actionsTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    host.appendChild(table);
  }

  function renderSessionsPanel() {
    const host = qs("#adminUsersSessions");
    if (!host) return;
    host.innerHTML = "";

    if (!state.selectedUserId) {
      host.appendChild(document.createTextNode("برای مشاهده نشست‌ها، روی «نشست‌ها» کلیک کنید."));
      return;
    }

    if (!state.sessions.length) {
      host.appendChild(document.createTextNode("نشست فعالی برای این کاربر یافت نشد."));
      return;
    }

    const title = document.createElement("div");
    title.className = "admin-mgmt-sessions__title";
    title.textContent = "نشست‌های کاربر انتخاب‌شده";
    host.appendChild(title);

    const table = document.createElement("table");
    table.className = "admin-mgmt-grid";
    table.innerHTML =
      "<thead><tr>" +
      "<th>شناسه</th><th>IP</th><th>دستگاه</th><th>ایجاد</th><th>آخرین فعالیت</th><th>عملیات</th>" +
      "</tr></thead>";
    const tbody = document.createElement("tbody");

    state.sessions.forEach((session) => {
      const sessionId = pick(session, "sessionId", "SessionId");
      const tr = document.createElement("tr");
      tr.innerHTML =
        "<td class=\"mono\">" +
        String(sessionId || "—").slice(0, 8) +
        "…</td><td class=\"mono\">" +
        (pick(session, "ipAddress", "IpAddress") || "—") +
        "</td><td class=\"mono\" title=\"" +
        (pick(session, "userAgent", "UserAgent") || "") +
        "\">" +
        (pick(session, "deviceId", "DeviceId") || pick(session, "userAgent", "UserAgent") || "—") +
        "</td><td>" +
        formatDate(pick(session, "createdAt", "CreatedAt")) +
        "</td><td>" +
        formatDate(pick(session, "lastActivityAt", "LastActivityAt")) +
        "</td>";

      const actionsTd = document.createElement("td");
      const revokeBtn = document.createElement("button");
      revokeBtn.type = "button";
      revokeBtn.className = "btn btn--small btn--warn";
      revokeBtn.textContent = "لغو نشست";
      revokeBtn.addEventListener("click", () => {
        void revokeSession(sessionId).catch((e) => setError(e.message || String(e)));
      });
      actionsTd.appendChild(revokeBtn);
      tr.appendChild(actionsTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    host.appendChild(table);

    const revokeAllBtn = document.createElement("button");
    revokeAllBtn.type = "button";
    revokeAllBtn.className = "btn btn--warn";
    revokeAllBtn.textContent = "لغو همه نشست‌های این کاربر";
    revokeAllBtn.addEventListener("click", () => {
      void revokeAllSessions(state.selectedUserId).catch((e) => setError(e.message || String(e)));
    });
    host.appendChild(revokeAllBtn);
  }

  function clearEditForm() {
    state.editingUserId = null;
    const fields = ["adminUserFirstName", "adminUserLastName", "adminUserEmail", "adminUserNationalCode", "adminUserPhone"];
    fields.forEach((id) => {
      const el = qs("#" + id);
      if (el) el.value = "";
    });
    const roleSelect = qs("#adminUserRole");
    if (roleSelect) roleSelect.value = "";
    const activeCheck = qs("#adminUserIsActive");
    if (activeCheck) activeCheck.checked = true;
    qs("#adminUserCancelEdit")?.classList.add("hidden");
    const saveBtn = qs("#adminUserSave");
    if (saveBtn) saveBtn.textContent = "ذخیره کاربر";
  }

  function beginEditUser(user) {
    const id = pick(user, "id", "Id") || pick(user, "userId", "UserId");
    if (!id) return;
    state.editingUserId = id;
    qs("#adminUserFirstName").value = pick(user, "firstName", "FirstName") || "";
    qs("#adminUserLastName").value = pick(user, "lastName", "LastName") || "";
    qs("#adminUserEmail").value = pick(user, "email", "Email") || "";
    qs("#adminUserNationalCode").value = pick(user, "nationalCode", "NationalCode") || "";
    qs("#adminUserPhone").value = pick(user, "phoneNumber", "PhoneNumber") || "";
    const roleNum = pick(user, "roleNumber", "RoleNumber");
    const roleSelect = qs("#adminUserRole");
    if (roleSelect && roleNum != null) roleSelect.value = String(roleNum);
    const activeCheck = qs("#adminUserIsActive");
    if (activeCheck) activeCheck.checked = pick(user, "isActive", "IsActive") !== false;
    qs("#adminUserCancelEdit")?.classList.remove("hidden");
    const saveBtn = qs("#adminUserSave");
    if (saveBtn) saveBtn.textContent = "ذخیره تغییرات";
    qs("#adminUsersEditForm")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    setInfo("در حال ویرایش کاربر — پس از تغییر، «ذخیره تغییرات» را بزنید.");
  }

  async function loadUsers() {
    setError("");
    state.viewMode = "all";
    const res = await state.panel.apiRequest({
      method: "GET",
      path: apiUsersPath("?take=" + state.take + "&skip=" + state.skip),
    });
    const { list, total } = unwrapList(res.body);
    state.users = list;
    state.total = total;
    renderUsersTable();
    setInfo("فهرست کاربران بارگذاری شد.");
  }

  async function loadOnlineUsers() {
    setError("");
    state.viewMode = "online";
    const res = await state.panel.apiRequest({ method: "GET", path: apiUsersPath("/online") });
    const { list } = unwrapList(res.body);
    state.onlineUsers = list;
    renderUsersTable();
    setInfo("کاربران آنلاین بارگذاری شدند.");
  }

  async function loadUserSessions(userId) {
    if (!userId) return;
    setError("");
    state.selectedUserId = userId;
    const res = await state.panel.apiRequest({ method: "GET", path: apiUsersPath("/" + userId + "/sessions") });
    const { list } = unwrapList(res.body);
    state.sessions = list;
    renderUsersTable();
    renderSessionsPanel();
    setInfo("نشست‌های کاربر بارگذاری شد.");
  }

  async function kickUser(userId, user) {
    if (!userId) return;
    const name = userName(user);
    if (!window.confirm("آیا از اخراج «" + name + "» (لغو تمام نشست‌ها) مطمئن هستید؟")) return;
    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({ method: "POST", path: apiUsersPath("/" + userId + "/kick"), json: false });
      if (state.selectedUserId === userId) {
        state.sessions = [];
        renderSessionsPanel();
      }
      if (state.viewMode === "online") await loadOnlineUsers();
      setInfo("کاربر اخراج شد — تمام نشست‌های فعال لغو شدند.");
    } finally {
      state.busy = false;
    }
  }

  async function revokeSession(sessionId) {
    if (!sessionId || !state.selectedUserId) return;
    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "POST",
        path: apiUsersPath("/" + state.selectedUserId + "/sessions/revoke"),
        body: { sessionId: sessionId },
      });
      await loadUserSessions(state.selectedUserId);
      setInfo("نشست لغو شد.");
    } finally {
      state.busy = false;
    }
  }

  async function revokeAllSessions(userId) {
    if (!userId) return;
    if (!window.confirm("آیا از لغو تمام نشست‌های این کاربر مطمئن هستید؟")) return;
    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({
        method: "POST",
        path: apiUsersPath("/" + userId + "/sessions/revoke-all"),
        json: false,
      });
      await loadUserSessions(userId);
      setInfo("تمام نشست‌های کاربر لغو شدند.");
    } finally {
      state.busy = false;
    }
  }

  async function saveUser() {
    if (!state.editingUserId) {
      setError("ابتدا یک کاربر را برای ویرایش انتخاب کنید.");
      return;
    }

    const body = {};
    const firstName = (qs("#adminUserFirstName")?.value || "").trim();
    const lastName = (qs("#adminUserLastName")?.value || "").trim();
    const email = (qs("#adminUserEmail")?.value || "").trim();
    const nationalCode = (qs("#adminUserNationalCode")?.value || "").trim();
    const phoneNumber = (qs("#adminUserPhone")?.value || "").trim();
    const roleNumber = qs("#adminUserRole")?.value;
    const isActive = qs("#adminUserIsActive")?.checked;

    if (firstName) body.firstName = firstName;
    if (lastName) body.lastName = lastName;
    if (email) body.email = email;
    if (nationalCode) body.nationalCode = nationalCode;
    if (phoneNumber) body.phoneNumber = phoneNumber;
    if (roleNumber) body.roleNumber = Number(roleNumber);
    if (typeof isActive === "boolean") body.isActive = isActive;

    if (state.busy) return;
    state.busy = true;
    try {
      const res = await state.panel.apiRequest({
        method: "PUT",
        path: apiUsersPath("/" + state.editingUserId),
        body: body,
      });
      const updated = unwrapPayload(res.body);
      clearEditForm();
      if (state.viewMode === "online") await loadOnlineUsers();
      else await loadUsers();
      if (updated && pick(updated, "id", "Id")) {
        setInfo("اطلاعات کاربر به‌روزرسانی شد.");
      }
    } finally {
      state.busy = false;
    }
  }

  async function deleteUser(userId, user) {
    if (!userId) return;
    const name = userName(user);
    if (!window.confirm("آیا از حذف کاربر «" + name + "» مطمئن هستید؟ این عمل غیرقابل بازگشت است.")) return;
    if (state.busy) return;
    state.busy = true;
    try {
      await state.panel.apiRequest({ method: "DELETE", path: apiUsersPath("/" + userId) });
      if (state.selectedUserId === userId) {
        state.selectedUserId = null;
        state.sessions = [];
        renderSessionsPanel();
      }
      if (state.editingUserId === userId) clearEditForm();
      if (state.viewMode === "online") await loadOnlineUsers();
      else await loadUsers();
      setInfo("کاربر حذف شد.");
    } finally {
      state.busy = false;
    }
  }

  function wire() {
    qs("#adminUsersLoad")?.addEventListener("click", () => {
      void loadUsers().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminUsersLoadOnline")?.addEventListener("click", () => {
      void loadOnlineUsers().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminUsersPrevPage")?.addEventListener("click", () => {
      state.skip = Math.max(0, state.skip - state.take);
      void loadUsers().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminUsersNextPage")?.addEventListener("click", () => {
      if (state.skip + state.take < state.total) {
        state.skip += state.take;
        void loadUsers().catch((e) => setError(e.message || String(e)));
      }
    });

    qs("#adminUserSave")?.addEventListener("click", () => {
      void saveUser().catch((e) => setError(e.message || String(e)));
    });

    qs("#adminUserCancelEdit")?.addEventListener("click", () => {
      clearEditForm();
      setInfo("ویرایش لغو شد.");
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateAccessUi();
      if (!canManageUsers()) {
        state.users = [];
        state.onlineUsers = [];
        state.sessions = [];
        state.selectedUserId = null;
        clearEditForm();
        renderUsersTable();
        renderSessionsPanel();
      }
    });

    document.querySelector('[data-tab="tabAdminUsers"]')?.addEventListener("click", () => {
      updateAccessUi();
      if (canManageUsers() && !state.users.length && state.viewMode === "all") {
        void loadUsers().catch((e) => setError(e.message || String(e)));
      }
    });
  }

  window.initAdminUsers = function initAdminUsers(panel) {
    state.panel = panel;
    const roleSelect = qs("#adminUserRole");
    if (roleSelect && !roleSelect.options.length) {
      ROLE_OPTIONS.forEach((opt) => {
        const o = document.createElement("option");
        o.value = opt.value;
        o.textContent = opt.label;
        roleSelect.appendChild(o);
      });
    }
    wire();
    updateAccessUi();
  };

  window.refreshAdminUsersAccess = updateAccessUi;
})();
