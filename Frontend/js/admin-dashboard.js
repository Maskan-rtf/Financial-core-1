(function () {
  const state = { panel: null, charts: [] };

  const CHART_COLORS = [
    "#6366f1", "#22c55e", "#f59e0b", "#ef4444", "#06b6d4",
    "#8b5cf6", "#ec4899", "#14b8a6", "#f97316", "#3b82f6",
  ];

  const ROLE_SECTIONS = [
    { key: "executive", title: "نمای اجرایی", icon: "👔", color: "#6366f1" },
    { key: "ceo", title: "مدیرعامل", icon: "📊", color: "#3b82f6" },
    { key: "board", title: "هیئت مدیره", icon: "🏛", color: "#8b5cf6" },
    { key: "departments", title: "واحدهای سازمانی", icon: "🏢", color: "#06b6d4" },
    { key: "applicant", title: "متقاضیان", icon: "👤", color: "#22c55e" },
  ];

  function qs(sel) {
    return document.querySelector(sel);
  }

  function pick(obj, ...keys) {
    if (!obj) return undefined;
    for (const k of keys) {
      if (obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return undefined;
  }

  function resolveRole() {
    const s = state.panel?.getActiveSession();
    return WorkflowModel.normalizeRole(s?.userRoleText, s?.userRoleNumber);
  }

  function formatMoney(n) {
    const v = Number(n) || 0;
    return v.toLocaleString("fa-IR") + " ریال";
  }

  function formatNum(n) {
    return (Number(n) || 0).toLocaleString("fa-IR");
  }

  function formatDate(iso) {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleString("fa-IR");
    } catch {
      return String(iso);
    }
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

  function chartDefaults() {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: "bottom",
          labels: { color: "#cbd5e1", padding: 14, usePointStyle: true, pointStyle: "circle" },
        },
        tooltip: {
          backgroundColor: "rgba(15,23,42,.92)",
          titleColor: "#f1f5f9",
          bodyColor: "#cbd5e1",
          borderColor: "rgba(99,102,241,.4)",
          borderWidth: 1,
          padding: 12,
          cornerRadius: 8,
        },
      },
    };
  }

  function createChart(canvas, config) {
    if (window.DashboardUi?.createChart) return window.DashboardUi.createChart(canvas, config, state.charts);
    if (!canvas || typeof Chart === "undefined" || !config) return null;
    const chart = new Chart(canvas, config);
    state.charts.push(chart);
    return chart;
  }

  function formatPersianMonth(year, month) {
    return window.DashboardUi?.formatPersianMonth
      ? window.DashboardUi.formatPersianMonth(year, month)
      : String(year) + "/" + String(month);
  }

  function formatPercent(n) {
    return window.DashboardUi?.formatPercent ? window.DashboardUi.formatPercent(n) : String(n) + "%";
  }

  function formatDays(n) {
    return window.DashboardUi?.formatDays ? window.DashboardUi.formatDays(n) : String(n) + " روز";
  }

  function doughnutConfig(labels, data, colors) {
    return {
      type: "doughnut",
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: colors || CHART_COLORS.slice(0, labels.length),
          borderWidth: 2,
          borderColor: "rgba(15,23,42,.8)",
          hoverOffset: 8,
        }],
      },
      options: {
        ...chartDefaults(),
        cutout: "62%",
        plugins: {
          ...chartDefaults().plugins,
          legend: { ...chartDefaults().plugins.legend, position: "right" },
        },
      },
    };
  }

  function pieConfig(labels, data, colors) {
    return {
      type: "pie",
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: colors || CHART_COLORS.slice(0, labels.length),
          borderWidth: 2,
          borderColor: "rgba(15,23,42,.8)",
          hoverOffset: 6,
        }],
      },
      options: chartDefaults(),
    };
  }

  function barConfig(labels, data, label, horizontal, colors) {
    return {
      type: "bar",
      data: {
        labels,
        datasets: [{
          label: label || "",
          data,
          backgroundColor: colors || CHART_COLORS.slice(0, labels.length).map((c) => c + "cc"),
          borderRadius: 6,
          borderSkipped: false,
        }],
      },
      options: {
        ...chartDefaults(),
        indexAxis: horizontal ? "y" : "x",
        plugins: { ...chartDefaults().plugins, legend: { display: !!label, labels: { color: "#cbd5e1" } } },
        scales: {
          x: { ticks: { color: "#94a3b8", font: { size: 11 } }, grid: { color: "rgba(255,255,255,0.05)" } },
          y: { ticks: { color: "#94a3b8", font: { size: 11 } }, grid: { color: horizontal ? "rgba(255,255,255,0.05)" : "rgba(255,255,255,0.05)" } },
        },
      },
    };
  }

  function lineConfig(labels, data, label) {
    return {
      type: "line",
      data: {
        labels,
        datasets: [{
          label: label || "",
          data,
          borderColor: "#6366f1",
          backgroundColor: "rgba(99,102,241,.15)",
          fill: true,
          tension: 0.35,
          pointRadius: 4,
          pointBackgroundColor: "#6366f1",
        }],
      },
      options: {
        ...chartDefaults(),
        plugins: { ...chartDefaults().plugins, legend: { display: false } },
        scales: {
          x: { ticks: { color: "#94a3b8" }, grid: { color: "rgba(255,255,255,0.05)" } },
          y: { ticks: { color: "#94a3b8" }, grid: { color: "rgba(255,255,255,0.05)" } },
        },
      },
    };
  }

  function renderKpiStrip(data) {
    const ex = pick(data, "executive", "Executive") || {};
    const ceo = pick(data, "ceo", "Ceo") || {};
    const board = pick(data, "board", "Board") || {};
    const app = pick(data, "applicantSummary", "ApplicantSummary") || {};

    const health = pick(data, "systemHealth", "SystemHealth") || pick(ex, "systemHealth", "SystemHealth");
    const kpis = [
      { label: "تعداد کل پرونده‌ها", value: formatNum(pick(board, "totalCases", "TotalCases")), accent: true },
      { label: "پرونده‌های جاری", value: formatNum(pick(ex, "totalActiveCases", "TotalActiveCases")), accent: true },
      { label: "مجموع مبلغ پرونده‌های جاری", value: formatMoney(pick(ceo, "totalRiskExposure", "TotalRiskExposure")), accent: true },
      { label: "نرخ تکمیل", value: formatPercent(pick(board, "completionRate", "CompletionRate") || 0), accent: false },
      { label: "مبلغ ضمانت‌نامه‌های جاری", value: formatMoney(pick(ex, "activeGuaranteesVolume", "ActiveGuaranteesVolume")), accent: true },
      { label: "مبلغ سرمایه‌گذاری‌های جاری", value: formatMoney(pick(ex, "activeInvestmentsVolume", "ActiveInvestmentsVolume")), accent: true },
      { label: "مبلغ تسهیلات جاری", value: formatMoney(pick(ex, "activeLoansVolume", "ActiveLoansVolume")), accent: true },
      { label: "کاربران آنلاین", value: formatNum(pick(health, "onlineUsersCount", "OnlineUsersCount") ?? pick(ex, "onlineUsersCount", "OnlineUsersCount")), accent: false },
      { label: "نشست‌های فعال", value: formatNum(pick(health, "activeSessionsCount", "ActiveSessionsCount")), accent: false },
      { label: "در انتظار تأیید مدیرعامل", value: formatNum(pick(ceo, "pendingCeoApprovals", "PendingCeoApprovals")), accent: false },
      { label: "تعداد متقاضیان", value: formatNum(pick(app, "applicantCount", "ApplicantCount")), accent: false },
    ];

    const host = qs("#adminKpiStrip");
    if (!host) return;
    host.innerHTML = kpis
      .map(
        (k) =>
          '<div class="admin-kpi' +
          (k.accent ? " admin-kpi--accent" : "") +
          '"><span class="admin-kpi__label">' +
          k.label +
          '</span><strong class="admin-kpi__value">' +
          k.value +
          "</strong></div>"
      )
      .join("");
  }

  function renderOverviewCharts(data) {
    const ex = pick(data, "executive", "Executive") || {};
    const board = pick(data, "board", "Board") || {};

    const statusDist = pick(ex, "statusDistribution", "StatusDistribution") || [];
    createChart(
      qs("#adminChartStatusDoughnut"),
      doughnutConfig(
        statusDist.map((x) => pick(x, "categoryTitle", "CategoryTitle") || ""),
        statusDist.map((x) => pick(x, "count", "Count") || 0)
      )
    );

    const phases = pick(board, "countsByPhase", "CountsByPhase") || [];
    createChart(
      qs("#adminChartPhasePie"),
      pieConfig(
        phases.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
        phases.map((x) => pick(x, "count", "Count") || 0)
      )
    );

    const monthly = pick(ex, "monthlyFinancialOutput", "MonthlyFinancialOutput") || [];
    createChart(
      qs("#adminChartMonthlyBar"),
      window.DashboardUi?.verticalBarConfig
        ? window.DashboardUi.verticalBarConfig(
            monthly.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
            monthly.map((m) => pick(m, "amount", "Amount") || 0),
            "مبلغ (ریال)",
            null,
            true
          )
        : barConfig(
            monthly.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
            monthly.map((m) => pick(m, "amount", "Amount") || 0),
            "مبلغ (ریال)"
          )
    );

    const trend = pick(board, "monthlyTrend", "MonthlyTrend") || [];
    createChart(
      qs("#adminChartTrendLine"),
      window.DashboardUi?.lineConfig
        ? window.DashboardUi.fixLineConfig(
            window.DashboardUi.lineConfig(
              trend.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
              trend.map((m) => pick(m, "count", "Count") || 0),
              "تعداد پرونده جدید"
            ),
            "#6366f1"
          )
        : lineConfig(
            trend.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
            trend.map((m) => pick(m, "count", "Count") || 0),
            "تعداد پرونده جدید"
          )
    );

    const pipeline = pick(ex, "pipelineByStatus", "PipelineByStatus") || pick(board, "countsByStatus", "CountsByStatus") || [];
    const topPipeline = pipeline.slice().sort((a, b) => (pick(b, "count", "Count") || 0) - (pick(a, "count", "Count") || 0)).slice(0, 10);
    createChart(
      qs("#adminChartPipelineBar"),
      window.DashboardUi?.horizontalBarConfig
        ? window.DashboardUi.horizontalBarConfig(
            topPipeline.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
            topPipeline.map((x) => pick(x, "count", "Count") || 0),
            "تعداد پرونده"
          )
        : barConfig(
            topPipeline.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
            topPipeline.map((x) => pick(x, "count", "Count") || 0),
            "تعداد",
            true
          )
    );

    const volumes = [
      { label: "ضمانت‌نامه", value: Number(pick(ex, "activeGuaranteesVolume", "ActiveGuaranteesVolume")) || 0 },
      { label: "سرمایه‌گذاری", value: Number(pick(ex, "activeInvestmentsVolume", "ActiveInvestmentsVolume")) || 0 },
      { label: "تسهیلات", value: Number(pick(ex, "activeLoansVolume", "ActiveLoansVolume")) || 0 },
    ];
    createChart(
      qs("#adminChartVolumePie"),
      window.DashboardUi?.horizontalBarConfig
        ? window.DashboardUi.horizontalBarConfig(
            volumes.map((v) => v.label),
            volumes.map((v) => v.value),
            "مبلغ (ریال)"
          )
        : barConfig(
            volumes.map((v) => v.label),
            volumes.map((v) => v.value),
            "مبلغ (ریال)",
            true
          )
    );
  }

  function renderDepartmentSection(data) {
    const departments = pick(data, "departments", "Departments") || [];
    const host = qs("#adminDeptGrid");
    const compareCanvas = qs("#adminChartDeptCompare");
    if (!host) return;

    if (!departments.length) {
      host.innerHTML = '<p class="muted">داده واحدها هنوز آماده نشده است.</p>';
      return;
    }

    host.innerHTML = departments
      .map((dept, idx) => {
        const title = pick(dept, "departmentTitle", "DepartmentTitle") || "";
        const queue = pick(dept, "totalQueueCount", "TotalQueueCount") || 0;
        const revision = pick(dept, "revisionRatePercent", "RevisionRatePercent") || 0;
        const key = pick(dept, "departmentKey", "DepartmentKey") || idx;
        const sm = pick(dept, "specificMetrics", "SpecificMetrics");
        const specificHtml =
          window.DashboardUi && sm
            ? window.DashboardUi.renderMetricRows
              ? window.DashboardUi.renderMetricRows(window.DashboardUi.getDepartmentMetricItems(sm, key), "dept-metric-rows dept-metric-rows--specific")
              : '<div class="admin-dept-card__metrics">' + window.DashboardUi.renderDepartmentMetrics(sm, key) + "</div>"
            : "";
        return (
          '<div class="admin-dept-card dept-summary-card">' +
          '<div class="admin-dept-card__head dept-summary-card__head">' +
          "<strong class=\"dept-summary-card__title\">" +
          title +
          "</strong>" +
          '<span class="admin-dept-card__badge">' +
          formatNum(queue) +
          " در صف</span></div>" +
          '<div class="dept-metric-row dept-metric-row--core">' +
          '<span class="dept-metric-row__label">شاخص درخواست اصلاح</span>' +
          '<strong class="dept-metric-row__value">' +
          formatPercent(revision) +
          "</strong>" +
          '<span class="dept-metric-row__hint muted">درخواست‌های اصلاح ÷ پرونده‌های صف (۶ ماه)</span>' +
          "</div>" +
          specificHtml +
          '<div class="admin-dept-card__chart"><canvas id="adminDeptChart_' +
          key +
          '"></canvas></div></div>"
        );
      })
      .join("");

    departments.forEach((dept) => {
      const key = pick(dept, "departmentKey", "DepartmentKey") || "";
      const queue = pick(dept, "queueByModule", "QueueByModule") || [];
      const canvas = qs("#adminDeptChart_" + key);
      if (!canvas || !queue.length) return;
      createChart(
        canvas,
        window.DashboardUi?.horizontalBarConfig
          ? window.DashboardUi.horizontalBarConfig(
              queue.map((q) => pick(q, "moduleTitle", "ModuleTitle") || ""),
              queue.map((q) => pick(q, "count", "Count") || 0),
              "تعداد پرونده"
            )
          : barConfig(
              queue.map((q) => pick(q, "moduleTitle", "ModuleTitle") || ""),
              queue.map((q) => pick(q, "count", "Count") || 0),
              "تعداد",
              true
            )
      );
    });

    if (compareCanvas) {
      createChart(
        compareCanvas,
        barConfig(
          departments.map((d) => pick(d, "departmentTitle", "DepartmentTitle") || ""),
          departments.map((d) => pick(d, "totalQueueCount", "TotalQueueCount") || 0),
          "تعداد پرونده در صف",
          false,
          departments.map((_, i) => CHART_COLORS[i % CHART_COLORS.length])
        )
      );
    }
  }

  function renderCeoSection(data) {
    const ceo = pick(data, "ceo", "Ceo") || {};
    const metrics = qs("#adminCeoMetrics");
    if (metrics) {
      metrics.innerHTML =
        '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">مجموع مبلغ پرونده‌های جاری</span><strong>' +
        formatMoney(pick(ceo, "totalRiskExposure", "TotalRiskExposure")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">مجموع مبلغ درخواستی</span><strong>' +
        formatMoney(pick(ceo, "totalRequestedAmount", "TotalRequestedAmount")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">مجموع پرداخت‌های تأییدشده</span><strong>' +
        formatMoney(pick(ceo, "approvedPaymentsSum", "ApprovedPaymentsSum")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">پرونده‌های ثبت‌شده این ماه</span><strong>' +
        formatNum(pick(ceo, "casesThisMonth", "CasesThisMonth")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">میانگین روز ماندگاری در بررسی</span><strong>' +
        formatDays(pick(ceo, "averageDaysInReview", "AverageDaysInReview") || 0) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">پرونده‌های رد‌شده</span><strong>' +
        formatNum(pick(ceo, "rejectedCount", "RejectedCount")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">در انتظار پرداخت</span><strong>' +
        formatNum(pick(ceo, "waitingPaymentCount", "WaitingPaymentCount")) +
        "</strong></div>";

      const pipeline = pick(ceo, "pipelineByStatus", "PipelineByStatus") || [];
      const topPipeline = pipeline.slice().sort((a, b) => (pick(b, "count", "Count") || 0) - (pick(a, "count", "Count") || 0)).slice(0, 10);
      createChart(
        qs("#adminChartCeoPipeline"),
        window.DashboardUi?.horizontalBarConfig
          ? window.DashboardUi.horizontalBarConfig(
              topPipeline.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
              topPipeline.map((x) => pick(x, "count", "Count") || 0),
              "تعداد پرونده"
            )
          : barConfig(
              topPipeline.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
              topPipeline.map((x) => pick(x, "count", "Count") || 0),
              "تعداد",
              true
            )
      );
    }
  }

  function renderApplicantSection(data) {
    const app = pick(data, "applicantSummary", "ApplicantSummary") || {};
    const metrics = qs("#adminApplicantMetrics");
    if (metrics) {
      metrics.innerHTML =
        '<div class="dashboard-metric dashboard-metric--accent"><span class="muted">تعداد متقاضی</span><strong>' +
        formatNum(pick(app, "applicantCount", "ApplicantCount")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">پرونده فعال</span><strong>' +
        formatNum(pick(app, "activeCasesCount", "ActiveCasesCount")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">بدهی باقی‌مانده</span><strong>' +
        formatMoney(pick(app, "totalRemainingDebt", "TotalRemainingDebt")) +
        "</strong></div>" +
        '<div class="dashboard-metric"><span class="muted">اقساط پرداخت‌نشده</span><strong>' +
        formatNum(pick(app, "unpaidInstallmentsCount", "UnpaidInstallmentsCount")) +
        "</strong></div>";

      const byModule = pick(app, "activeCasesByModule", "ActiveCasesByModule") || [];
      createChart(
        qs("#adminChartApplicantPie"),
        pieConfig(
          byModule.map((x) => pick(x, "moduleTitle", "ModuleTitle") || ""),
          byModule.map((x) => pick(x, "count", "Count") || 0)
        )
      );
    }
  }

  function renderBoardSection(data) {
    const board = pick(data, "board", "Board") || {};
    const statuses = pick(board, "countsByStatus", "CountsByStatus") || [];
    const phases = pick(board, "countsByPhase", "CountsByPhase") || [];
    const trend = pick(board, "monthlyTrend", "MonthlyTrend") || [];

    createChart(
      qs("#adminChartBoardStatus"),
      pieConfig(
        statuses.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
        statuses.map((x) => pick(x, "count", "Count") || 0)
      )
    );

    createChart(
      qs("#adminChartBoardPhase"),
      doughnutConfig(
        phases.map((x) => pick(x, "statusTitle", "StatusTitle") || ""),
        phases.map((x) => pick(x, "count", "Count") || 0)
      )
    );

    createChart(
      qs("#adminChartBoardTrend"),
      window.DashboardUi?.fixLineConfig
        ? window.DashboardUi.fixLineConfig(
            window.DashboardUi.lineConfig(
              trend.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
              trend.map((m) => pick(m, "count", "Count") || 0),
              "تعداد پرونده جدید"
            ),
            "#06b6d4"
          )
        : lineConfig(
            trend.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month"))),
            trend.map((m) => pick(m, "count", "Count") || 0),
            "تعداد پرونده جدید"
          )
    );
  }

  function renderBottlenecks(data) {
    const ex = pick(data, "executive", "Executive") || {};
    const bottlenecks = pick(ex, "departmentBottlenecks", "DepartmentBottlenecks") || [];
    const host = qs("#adminBottlenecks");
    if (!host) return;

    host.innerHTML =
      '<div class="card__title">میانگین زمان ماندگاری در واحدها</div>' +
      '<p class="muted portal-stage__hint">هرچه نوار بلندتر باشد، پرونده‌ها بیشتر در آن واحد معطل می‌مانند.</p>' +
      (window.DashboardUi?.renderBottleneckBars
        ? window.DashboardUi.renderBottleneckBars(bottlenecks)
        : bottlenecks.length
          ? bottlenecks
              .map((b) => {
                const title = pick(b, "departmentTitle", "DepartmentTitle") || "";
                const days = pick(b, "averageDays", "AverageDays") || 0;
                const cnt = pick(b, "activeCaseCount", "ActiveCaseCount") || 0;
                const pct = Math.min(100, Math.round(days * 3));
                return (
                  '<div class="admin-bottleneck">' +
                  '<div class="admin-bottleneck__head"><span>' +
                  title +
                  ' <span class="muted">(' +
                  formatNum(cnt) +
                  ' پرونده جاری)</span></span><span class="mono">' +
                  formatDays(days) +
                  "</span></div>" +
                  '<div class="dashboard-bar__track"><div class="dashboard-bar__fill dashboard-bar__fill--warn" style="width:' +
                  pct +
                  '%"></div></div></div>'
                );
              })
              .join("")
          : '<p class="muted">داده‌ای موجود نیست.</p>');
  }

  function renderRoleNav() {
    const nav = qs("#adminRoleNav");
    if (!nav) return;
    nav.innerHTML = ROLE_SECTIONS.map(
      (s, i) =>
        '<button type="button" class="admin-role-nav__btn' +
        (i === 0 ? " is-active" : "") +
        '" data-admin-section="' +
        s.key +
        '" style="--role-color:' +
        s.color +
        '"><span class="admin-role-nav__icon">' +
        s.icon +
        "</span><span>" +
        s.title +
        "</span></button>"
    ).join("");

    nav.querySelectorAll(".admin-role-nav__btn").forEach((btn) => {
      btn.addEventListener("click", () => {
        nav.querySelectorAll(".admin-role-nav__btn").forEach((b) => b.classList.remove("is-active"));
        btn.classList.add("is-active");
        const key = btn.getAttribute("data-admin-section");
        document.querySelectorAll(".admin-role-section").forEach((sec) => {
          sec.classList.toggle("hidden", sec.getAttribute("data-section") !== key);
        });
      });
    });
  }

  async function loadAdminDashboard(refresh) {
    const errEl = qs("#adminDashboardError");
    const metaEl = qs("#adminDashboardMeta");
    const loadingEl = qs("#adminDashboardLoading");
    errEl?.classList.add("hidden");
    loadingEl?.classList.remove("hidden");
    destroyCharts();

    try {
      if (refresh) {
        await state.panel.apiRequest({ method: "POST", path: "/api/v1/dashboard/refresh" });
      }

      const res = await state.panel.apiRequest({ method: "GET", path: "/api/v1/dashboard/admin-overview" });
      const d = unwrap(res.body);
      if (!d) throw new Error("پاسخ داشبورد مدیریت خالی است.");

      const computedAt = pick(d, "computedAtUtc", "ComputedAtUtc");
      const isStale = pick(d, "isStale", "IsStale");
      if (metaEl) {
        metaEl.textContent =
          (computedAt ? "آخرین به‌روزرسانی: " + formatDate(computedAt) : "") +
          (isStale ? " · (داده قدیمی — برای تازه‌سازی کلیک کنید)" : "");
      }

      renderKpiStrip(d);

      const modules = pick(d, "modules", "Modules") || pick(pick(d, "executive", "Executive"), "modules", "Modules") || [];
      const health = pick(d, "systemHealth", "SystemHealth");
      const healthHost = qs("#adminSystemHealth");
      if (healthHost) {
        healthHost.innerHTML =
          window.DashboardUi && health ? window.DashboardUi.renderSystemHealth(health) : "";
      }
      if (window.DashboardUi) {
        window.DashboardUi.renderModuleSections(qs("#adminModuleSections"), modules, state.charts, "adminMod");
      }

      renderOverviewCharts(d);
      renderCeoSection(d);
      renderBoardSection(d);
      renderDepartmentSection(d);
      renderApplicantSection(d);
      renderBottlenecks(d);
    } catch (e) {
      if (errEl) {
        errEl.textContent = String(e.message || e);
        errEl.classList.remove("hidden");
      }
    } finally {
      loadingEl?.classList.add("hidden");
    }
  }

  function updateNav() {
    const isAdmin = resolveRole() === "Admin";
    qs("#navAdminDashboard")?.classList.toggle("hidden", !isAdmin);
    qs("#tabAdminDashboard")?.classList.toggle("hidden", !isAdmin);
  }

  function wire() {
    renderRoleNav();
    qs("#btnLoadAdminDashboard")?.addEventListener("click", () => loadAdminDashboard(false));
    qs("#btnRefreshAdminDashboard")?.addEventListener("click", () => loadAdminDashboard(true));

    document.addEventListener("testpanel:session-changed", () => {
      updateNav();
      if (qs("#tabAdminDashboard")?.classList.contains("is-active")) loadAdminDashboard(false);
    });

    qs('[data-tab="tabAdminDashboard"]')?.addEventListener("click", () => {
      setTimeout(() => loadAdminDashboard(false), 50);
    });
  }

  window.initAdminDashboard = function (panel) {
    state.panel = panel;
    wire();
    updateNav();
  };
})();
