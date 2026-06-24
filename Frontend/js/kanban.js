(function () {
  const state = { panel: null, actionItems: [], watchItems: [] };

  function qs(sel) {
    return document.querySelector(sel);
  }

  function unwrap(body) {
    return state.panel.unwrapEnvelope(body).payload;
  }

  function kanbanPath(suffix) {
    return state.panel.kanbanBasePath() + suffix;
  }

  function moduleLabel(item) {
    const m = Number(pick(item, "module", "Module") || 1);
    if (m === 2) return "ضمانت‌نامه";
    if (m === 3) return "اصلاحیه";
    if (m === 4) return "تسهیلات";
    return "سرمایه‌گذاری";
  }

  function pick(obj, camel, pascal) {
    if (!obj) return "";
    return obj[camel] ?? obj[pascal] ?? "";
  }

  function formatDate(value) {
    if (!value) return "—";
    try {
      return new Date(value).toLocaleString("fa-IR");
    } catch {
      return String(value);
    }
  }

  function setError(message) {
    const box = qs("#kanbanError");
    if (!box) return;
    if (!message) {
      box.classList.add("hidden");
      box.textContent = "";
      return;
    }
    box.textContent = message;
    box.classList.remove("hidden");
  }

  function setLoading(isLoading) {
    const node = qs("#kanbanLoading");
    if (node) node.classList.toggle("hidden", !isLoading);
  }

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  function renderActionCard(item) {
    const card = el("button", "kanban-card kanban-card--action");
    card.type = "button";
    const id = pick(item, "id", "Id");
    card.dataset.caseId = id;

    const title = pick(item, "startupTitle", "StartupTitle") || pick(item, "companyName", "CompanyName") || pick(item, "caseNumber", "CaseNumber");
    card.appendChild(el("div", "kanban-card__title", "[" + moduleLabel(item) + "] " + title));
    card.dataset.apiBase = pick(item, "apiBasePath", "ApiBasePath") || state.panel.casesBasePath();
    card.dataset.module = String(pick(item, "module", "Module") || "1");

    const meta = el("div", "kanban-card__meta");
    meta.innerHTML =
      "شماره: <span class='mono'>" +
      (pick(item, "caseNumber", "CaseNumber") || "—") +
      "</span><br/>وضعیت: " +
      (pick(item, "statusTitle", "StatusTitle") || "—") +
      " · فاز: " +
      (pick(item, "phaseTitle", "PhaseTitle") || "—") +
      "<br/>بروزرسانی: " +
      formatDate(pick(item, "updatedAt", "UpdatedAt") || pick(item, "createdAt", "CreatedAt"));
    card.appendChild(meta);

    const hint = pick(item, "pendingActionLabel", "PendingActionLabel");
    if (hint) card.appendChild(el("div", "kanban-card__hint", hint));

    const actions = item.allowedActions || item.AllowedActions || [];
    if (actions.length) {
      const wrap = el("div", "kanban-card__actions");
      actions.slice(0, 6).forEach((action) => wrap.appendChild(el("span", "kanban-chip", action)));
      card.appendChild(wrap);
    }

    card.addEventListener("click", () => openCase(id, card));
    return card;
  }

  function renderWatchCard(item) {
    const card = el("button", "kanban-card");
    card.type = "button";
    const id = pick(item, "id", "Id");
    card.dataset.caseId = id;

    const title =
      pick(item, "startupTitle", "StartupTitle") ||
      pick(item, "companyName", "CompanyName") ||
      pick(item, "caseNumber", "CaseNumber");
    card.appendChild(el("div", "kanban-card__title", "[" + moduleLabel(item) + "] " + title));
    card.dataset.apiBase = pick(item, "apiBasePath", "ApiBasePath") || state.panel.casesBasePath();
    card.dataset.module = String(pick(item, "module", "Module") || "1");

    const meta = el("div", "kanban-card__meta");
    meta.textContent =
      (pick(item, "statusTitle", "StatusTitle") || "—") +
      " · " +
      (pick(item, "phaseTitle", "PhaseTitle") || "—") +
      " · " +
      (pick(item, "pendingActionLabel", "PendingActionLabel") || "");
    card.appendChild(meta);

    card.addEventListener("click", () => openCase(id, card));
    return card;
  }

  function renderList(host, items, builder, emptyText) {
    if (!host) return;
    host.innerHTML = "";
    if (!items.length) {
      host.appendChild(el("div", "kanban-empty", emptyText));
      return;
    }
    items.forEach((item) => host.appendChild(builder(item)));
  }

  function resolveModule(card) {
    const module = card?.dataset?.module;
    const apiBase = card?.dataset?.apiBase || "";
    if (module === "4" || apiBase.indexOf("loancases") >= 0) return "loan";
    if (module === "2" || apiBase.indexOf("guaranteecases") >= 0) return "guarantee";
    return "investment";
  }

  function openCase(caseId, card) {
    if (!caseId || !state.panel) return;
    const mod = resolveModule(card);
    const casesTab = document.querySelector('[data-tab="tabCases"]');
    if (casesTab) casesTab.click();
    if (window.CasesHub && typeof window.CasesHub.openCase === "function") {
      window.CasesHub.openCase(mod, caseId);
      return;
    }
    if (mod === "loan") state.panel.setLoanCaseId(caseId);
    else if (mod === "guarantee") state.panel.setGuaranteeCaseId(caseId);
    else state.panel.setCurrentCaseId(caseId, "investment");
  }

  function render() {
    qs("#kanbanActionCount").textContent = String(state.actionItems.length);
    qs("#kanbanWatchCount").textContent = String(state.watchItems.length);
    renderList(qs("#kanbanActionList"), state.actionItems, renderActionCard, "پرونده‌ای منتظر اقدام شما نیست.");
    renderList(qs("#kanbanWatchList"), state.watchItems, renderWatchCard, "موردی برای پیگیری نیست.");
  }

  async function load() {
    if (!state.panel || !state.panel.getActiveSession()?.accessToken) {
      setError("برای مشاهده کارتابل ابتدا وارد شوید.");
      state.actionItems = [];
      state.watchItems = [];
      render();
      return;
    }

    setError("");
    setLoading(true);
    try {
      const [actionRes, watchRes] = await Promise.all([
        state.panel.apiRequest({ method: "GET", path: kanbanPath("/action-required") }),
        state.panel.apiRequest({ method: "GET", path: kanbanPath("/watching") }),
      ]);

      state.actionItems = unwrap(actionRes.body) || [];
      state.watchItems = unwrap(watchRes.body) || [];
      if (!Array.isArray(state.actionItems)) state.actionItems = [];
      if (!Array.isArray(state.watchItems)) state.watchItems = [];
      render();
    } catch (error) {
      setError(error && error.message ? error.message : String(error));
    } finally {
      setLoading(false);
    }
  }

  function wireEvents() {
    const refreshBtn = qs("#kanbanRefresh");
    if (refreshBtn) refreshBtn.addEventListener("click", () => load());
    document.addEventListener("testpanel:session-changed", () => load());
    document.addEventListener("testpanel:case-changed", () => load());
  }

  window.initKanban = function initKanban(panel) {
    state.panel = panel;
    wireEvents();
    load();
  };

  window.kanbanRefresh = load;
})();
