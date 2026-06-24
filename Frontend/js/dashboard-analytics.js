(function () {
  const state = { panel: null, charts: [] };
  const ui = () => window.DashboardUi || {};

  function qs(sel) {
    return document.querySelector(sel);
  }

  function pick(obj, ...keys) {
    return ui().pick ? ui().pick(obj, ...keys) : undefined;
  }

  function formatMoney(n) {
    return ui().formatMoney ? ui().formatMoney(n) : String(n);
  }

  function formatNum(n) {
    return ui().formatNum ? ui().formatNum(n) : String(n);
  }

  function formatPercent(n) {
    return ui().formatPercent ? ui().formatPercent(n) : String(n) + "%";
  }

  function formatDays(n) {
    return ui().formatDays ? ui().formatDays(n) : String(n) + " روز";
  }

  function formatDate(iso) {
    return ui().formatDate ? ui().formatDate(iso) : String(iso || "");
  }

  function formatPersianMonth(year, month) {
    return ui().formatPersianMonth ? ui().formatPersianMonth(year, month) : String(year) + "/" + String(month);
  }

  function unwrap(body) {
    return body && (body.data != null ? body.data : body.Data != null ? body.Data : body);
  }

  function destroyCharts() {
    state.charts.forEach((c) => {
      try {
        c.destroy();
      } catch (_) {}
    });
    state.charts = [];
  }

  function createChart(canvas, config) {
    if (ui().createChart) return ui().createChart(canvas, config, state.charts);
    if (!canvas || typeof Chart === "undefined" || !config) return null;
    const chart = new Chart(canvas, config);
    state.charts.push(chart);
    return chart;
  }

  function showSection(viewType) {
    ["Executive", "Department", "Applicant"].forEach((t) => {
      qs("#dashView" + t)?.classList.toggle("hidden", t !== viewType);
    });
  }

  function renderExecutive(ex) {
    const metrics = qs("#dashExecMetrics");
    if (!metrics) return;
    metrics.innerHTML =
      '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">مبلغ ضمانت‌نامه‌های جاری</span><strong>' +
      formatMoney(pick(ex, "activeGuaranteesVolume", "ActiveGuaranteesVolume")) +
      "</strong></div>" +
      '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">مبلغ سرمایه‌گذاری‌های جاری</span><strong>' +
      formatMoney(pick(ex, "activeInvestmentsVolume", "ActiveInvestmentsVolume")) +
      "</strong></div>" +
      '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">مبلغ تسهیلات جاری</span><strong>' +
      formatMoney(pick(ex, "activeLoansVolume", "ActiveLoansVolume")) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">پرونده‌های جاری</span><strong>' +
      formatNum(pick(ex, "totalActiveCases", "TotalActiveCases") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">پرونده‌های تکمیل‌شده</span><strong>' +
      formatNum(pick(ex, "completedCases", "CompletedCases") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">نرخ تکمیل</span><strong>' +
      formatPercent(pick(ex, "completionRate", "CompletionRate") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">کاربران آنلاین</span><strong>' +
      formatNum(pick(ex, "onlineUsersCount", "OnlineUsersCount") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">کاربران فعال امروز</span><strong>' +
      formatNum(pick(ex, "dailyActiveUsers", "DailyActiveUsers") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">در انتظار تأیید مدیرعامل</span><strong>' +
      formatNum(pick(ex, "pendingCeoApprovals", "PendingCeoApprovals") || 0) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">میانگین روز ماندگاری در بررسی</span><strong>' +
      formatDays(pick(ex, "averageDaysInReview", "AverageDaysInReview") || 0) +
      "</strong></div>";

    const statusDist = pick(ex, "statusDistribution", "StatusDistribution") || [];
    createChart(
      qs("#dashExecPie"),
      ui().doughnutConfig
        ? ui().doughnutConfig(
            statusDist.map((x) => pick(x, "categoryTitle", "CategoryTitle") || ""),
            statusDist.map((x) => pick(x, "count", "Count") || 0)
          )
        : null
    );

    const monthly = pick(ex, "monthlyFinancialOutput", "MonthlyFinancialOutput") || [];
    createChart(
      qs("#dashExecBar"),
      ui().verticalBarConfig
        ? ui().verticalBarConfig(
            monthly.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
            monthly.map((m) => pick(m, "amount", "Amount") || 0),
            "مبلغ (ریال)",
            null,
            true
          )
        : null
    );

    const bottlenecks = pick(ex, "departmentBottlenecks", "DepartmentBottlenecks") || [];
    const bnEl = qs("#dashExecBottlenecks");
    if (bnEl) {
      bnEl.innerHTML =
        '<div class="card__title">میانگین زمان ماندگاری در واحدها</div>' +
        '<p class="muted portal-stage__hint">هرچه نوار بلندتر باشد، پرونده‌ها بیشتر در آن واحد معطل می‌مانند.</p>' +
        (ui().renderBottleneckBars
          ? ui().renderBottleneckBars(bottlenecks)
          : '<p class="muted">داده‌ای موجود نیست.</p>');
    }

    const activity = pick(ex, "recentActivity", "RecentActivity") || [];
    const actEl = qs("#dashExecActivity");
    if (actEl) {
      actEl.innerHTML =
        '<div class="card__title">آخرین فعالیت‌های سیستم</div><ul class="dashboard-activity">' +
        activity
          .map((r) => {
            const cn = pick(r, "caseNumber", "CaseNumber") || "";
            const act = pick(r, "action", "Action") || "";
            const at = pick(r, "createdAt", "CreatedAt") || "";
            return '<li><span class="mono">' + cn + "</span> — " + act + ' <span class="muted">' + formatDate(at) + "</span></li>";
          })
          .join("") +
        "</ul>";
    }
  }

  function renderDepartment(dept) {
    const metrics = qs("#dashDeptMetrics");
    if (!metrics) return;
    const title = pick(dept, "departmentTitle", "DepartmentTitle") || "";
    const departmentKey = pick(dept, "departmentKey", "DepartmentKey");
    const queue = ui().filterQueueForDepartment
      ? ui().filterQueueForDepartment(pick(dept, "queueByModule", "QueueByModule") || [], departmentKey)
      : pick(dept, "queueByModule", "QueueByModule") || [];
    const totalQueue = queue.reduce((sum, q) => sum + (pick(q, "count", "Count") || 0), 0);

    metrics.innerHTML =
      '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">واحد</span><strong>' +
      title +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">پرونده در صف کاری</span><strong>' +
      formatNum(totalQueue) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">نرخ درخواست اصلاح</span><strong>' +
      formatPercent(pick(dept, "revisionRatePercent", "RevisionRatePercent") || 0) +
      "</strong></div>";

    createChart(
      qs("#dashDeptBar"),
      ui().horizontalBarConfig
        ? ui().horizontalBarConfig(
            queue.map((q) => pick(q, "moduleTitle", "ModuleTitle") || ""),
            queue.map((q) => pick(q, "count", "Count") || 0),
            "تعداد پرونده"
          )
        : null
    );

    const links = pick(dept, "inboxQuickLinks", "InboxQuickLinks") || [];
    const inboxEl = qs("#dashDeptInbox");
    if (inboxEl) {
      inboxEl.innerHTML =
        '<div class="card__title">کارتابل — نیاز به اقدام</div>' +
        (links.length
          ? '<ul class="dashboard-inbox">' +
            links
              .map((l) => {
                const mod = pick(l, "module", "Module") || "";
                const cn = pick(l, "caseNumber", "CaseNumber") || "";
                const st = pick(l, "statusTitle", "StatusTitle") || "";
                const id = pick(l, "caseId", "CaseId") || "";
                return (
                  '<li><button type="button" class="btn btn--small dash-inbox-link" data-module="' +
                  mod.toLowerCase() +
                  '" data-case-id="' +
                  id +
                  '"><span class="mono">' +
                  cn +
                  "</span> — " +
                  st +
                  "</button></li>"
                );
              })
              .join("") +
            "</ul>"
          : '<p class="muted">موردی در صف اقدام شما نیست.</p>');

      inboxEl.querySelectorAll(".dash-inbox-link").forEach((btn) => {
        btn.addEventListener("click", () => {
          const mod = btn.getAttribute("data-module");
          const caseId = btn.getAttribute("data-case-id");
          if (window.CasesHub && caseId) {
            document.querySelector('[data-tab="tabCases"]')?.click();
            window.CasesHub.openCase(mod, caseId);
          }
        });
      });
    }
  }

  function renderApplicant(app) {
    const metrics = qs("#dashApplicantMetrics");
    if (!metrics) return;
    metrics.innerHTML =
      '<div class="dashboard-metric"><span class="muted">مانده بدهی</span><strong>' +
      formatMoney(pick(app, "totalRemainingDebt", "TotalRemainingDebt")) +
      "</strong></div>" +
      '<div class="dashboard-metric"><span class="muted">اقساط پرداخت‌نشده</span><strong>' +
      formatNum(pick(app, "unpaidInstallmentsCount", "UnpaidInstallmentsCount") || 0) +
      "</strong></div>";

    const cases = pick(app, "activeCases", "ActiveCases") || [];
    const casesEl = qs("#dashApplicantCases");
    if (casesEl) {
      casesEl.innerHTML =
        '<div class="card__title">پرونده‌های در جریان</div>' +
        (cases.length
          ? cases
              .map((c) => {
                const pct = pick(c, "progressPercent", "ProgressPercent") || 0;
                const cn = pick(c, "caseNumber", "CaseNumber") || "";
                const mod = pick(c, "moduleTitle", "ModuleTitle") || "";
                const st = pick(c, "statusTitle", "StatusTitle") || "";
                const ph = pick(c, "phaseTitle", "PhaseTitle") || "";
                const id = pick(c, "caseId", "CaseId") || "";
                const moduleKey = (pick(c, "module", "Module") || "").toLowerCase();
                return (
                  '<div class="dash-progress-card">' +
                  '<div class="dash-progress-card__head"><button type="button" class="btn btn--small dash-case-link" data-module="' +
                  moduleKey +
                  '" data-case-id="' +
                  id +
                  '"><span class="mono">' +
                  cn +
                  "</span></button><span class="muted">" +
                  mod +
                  "</span></div>" +
                  '<div class="muted">' +
                  ph +
                  " — " +
                  st +
                  "</div>" +
                  '<div class="dash-stepper"><div class="dash-stepper__fill" style="width:' +
                  pct +
                  '%"></div></div>" +
                  '<div class="muted dash-stepper__label">' +
                  formatPercent(pct) +
                  " پیشرفت</div></div>"
                );
              })
              .join("")
          : '<p class="muted">پرونده فعالی ندارید.</p>');

      casesEl.querySelectorAll(".dash-case-link").forEach((btn) => {
        btn.addEventListener("click", () => {
          const mod = btn.getAttribute("data-module");
          const caseId = btn.getAttribute("data-case-id");
          if (window.CasesHub && caseId) {
            document.querySelector('[data-tab="tabCases"]')?.click();
            window.CasesHub.openCase(mod, caseId);
          }
        });
      });
    }

    const comments = pick(app, "recentComments", "RecentComments") || [];
    const commEl = qs("#dashApplicantComments");
    if (commEl) {
      commEl.innerHTML =
        '<div class="card__title">آخرین نظرات</div><ul class="dashboard-activity">' +
        (comments.length
          ? comments
              .map((c) => {
                const cn = pick(c, "caseNumber", "CaseNumber") || "";
                const msg = pick(c, "commentText", "CommentText") || "";
                const role = pick(c, "authorRole", "AuthorRole") || "";
                const at = pick(c, "createdAt", "CreatedAt") || "";
                return (
                  "<li><span class=\"mono\">" +
                  cn +
                  "</span> <span class=\"muted\">(" +
                  role +
                  ")</span>: " +
                  msg +
                  ' <span class="muted">' +
                  formatDate(at) +
                  "</span></li>"
                );
              })
              .join("")
          : "<li class=\"muted\">نظری ثبت نشده است.</li>") +
        "</ul>";
    }
  }

  function renderFundCreditLimitsSection(section) {
    const host = qs("#dashFundCreditLimits");
    if (!host) return;

    if (!section) {
      host.classList.add("hidden");
      host.innerHTML = "";
      return;
    }

    const active = pick(section, "activePools", "ActivePools") || [];
    const historical = pick(section, "historicalPools", "HistoricalPools") || pick(section, "historical", "Historical") || [];

    host.classList.remove("hidden");
    host.innerHTML = '<div class="card__title">سقف اعتبار دوره‌ای صندوق</div>';

    const renderPoolCards = (pools, title) => {
      if (!pools.length) return "";
      let html = '<div class="fund-credit-dash__section"><div class="muted">' + title + "</div>";
      pools.forEach((p) => {
        const mod = pick(p, "moduleType", "ModuleType");
        const modLabel = Number(mod) === 2 ? "تسهیلات" : "ضمانت‌نامه";
        const budget = pick(p, "creditLimitWithCheck", "CreditLimitWithCheck");
        const used = pick(p, "totalUtilized", "TotalUtilized");
        const remaining = pick(p, "remainingCapacity", "RemainingCapacity");
        html +=
          '<div class="fund-credit-dash__card">' +
          "<strong>" +
          modLabel +
          "</strong> · " +
          (pick(p, "periodStart", "PeriodStart") || "—") +
          " تا " +
          (pick(p, "expiresAt", "ExpiresAt") || "—") +
          "<br/>سقف: " +
          formatMoney(budget) +
          " · مصرف‌شده: " +
          formatMoney(used) +
          " · باقی‌مانده: " +
          formatMoney(remaining) +
          "</div>";
      });
      return html + "</div>";
    };

    host.innerHTML +=
      renderPoolCards(active, "دوره‌های فعال") +
      renderPoolCards(historical, "تاریخچه تخصیص‌ها");
  }

  async function loadDashboard() {
    const errEl = qs("#dashAnalyticsError");
    const metaEl = qs("#dashAnalyticsMeta");
    errEl?.classList.add("hidden");
    destroyCharts();

    try {
      const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/dashboard/me" });
      const d = unwrap(res.body);
      if (!d) throw new Error("پاسخ داشبورد خالی است.");

      const viewType = pick(d, "viewType", "ViewType") || "Executive";
      const computedAt = pick(d, "computedAtUtc", "ComputedAtUtc");
      const isStale = pick(d, "isStale", "IsStale");

      if (metaEl) {
        metaEl.textContent = ui().buildMetaText
          ? ui().buildMetaText({
              roleLabel: ui().viewTypeLabel ? ui().viewTypeLabel(viewType) : viewType,
              computedAt,
              isStale,
            })
          : "نمای " + viewType;
      }

      showSection(viewType);

      if (viewType === "Executive") {
        renderExecutive(pick(d, "executive", "Executive") || {});
      } else if (viewType === "Department") {
        renderDepartment(pick(d, "department", "Department") || {});
      } else {
        renderApplicant(pick(d, "applicant", "Applicant") || {});
      }

      renderFundCreditLimitsSection(pick(d, "fundCreditLimits", "FundCreditLimits"));
    } catch (e) {
      if (errEl) {
        errEl.textContent = String(e.message || e);
        errEl.classList.remove("hidden");
      }
    }
  }

  function updateNav() {
    const role = resolveRole();
    const executive = ["CEO", "Admin", "InvestmentManager", "FinancialManager"].includes(role);
    const department = [
      "InvestmentExpert",
      "LegalExpert",
      "LegalManager",
      "FinancialExpert",
      "CreditExpert",
      "CreditManager",
      "TechnicalExpert",
      "TechnicalManager",
    ].includes(role);
    const applicant = role === "Applicant";
    const show = executive || department || applicant || role === "Admin";
    qs("#navDashboard")?.classList.toggle("hidden", !show);
  }

  function resolveRole() {
    const s = state.panel?.getActiveSession();
    return WorkflowModel.normalizeRole(s?.userRoleText, s?.userRoleNumber);
  }

  function wire() {
    qs("#btnLoadAnalyticsDashboard")?.addEventListener("click", loadDashboard);
    document.addEventListener("testpanel:session-changed", () => {
      updateNav();
      if (qs("#tabDashboard")?.classList.contains("is-active")) loadDashboard();
    });

    const tabBtn = qs('[data-tab="tabDashboard"]');
    tabBtn?.addEventListener("click", () => {
      setTimeout(loadDashboard, 50);
    });
  }

  window.initDashboardAnalytics = function (panel) {
    state.panel = panel;
    wire();
    updateNav();
  };
})();
