(function () {
  const ui = () => window.DashboardUi || {};
  const pick = (...args) => (ui().pick ? ui().pick(...args) : undefined);
  const formatNum = (n) => (ui().formatNum ? ui().formatNum(n) : String(n));
  const formatDays = (n) => (ui().formatDays ? ui().formatDays(n) : String(n) + " روز");

  const KPI_ROLES = ["Admin", "CEO", "BoardMember", "TechnicalExpert"];

  const PERIODS = [
    { id: "Last30Days", label: "۳۰ روز اخیر" },
    { id: "Last90Days", label: "۹۰ روز اخیر" },
    { id: "ThisQuarter", label: "فصل جاری" },
    { id: "AllTime", label: "کل دوره" },
  ];

  const state = {
    panel: null,
    period: "Last30Days",
    data: null,
    charts: [],
    loading: false,
  };

  function qs(sel) {
    return document.querySelector(sel);
  }

  function unwrap(body) {
    return body && (body.data != null ? body.data : body.Data != null ? body.Data : body);
  }

  function resolveRole() {
    const s = state.panel?.getActiveSession();
    if (window.WorkflowModel && typeof WorkflowModel.normalizeRole === "function") {
      return WorkflowModel.normalizeRole(s?.userRoleText, s?.userRoleNumber);
    }
    return String(s?.userRoleText || "");
  }

  function canViewKpi() {
    const role = resolveRole();
    return KPI_ROLES.includes(role);
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
          labels: { color: "#cbd5e1", padding: 10, usePointStyle: true, font: { size: 11 } },
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
    if (!canvas || typeof Chart === "undefined") return null;
    const chart = new Chart(canvas, config);
    state.charts.push(chart);
    return chart;
  }

  function formatRelativeTime(iso) {
    if (!iso) return "—";
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) return "—";
    const diffMs = Date.now() - then;
    const hours = Math.floor(diffMs / 3600000);
    if (hours < 1) return "کمتر از ۱ ساعت پیش";
    if (hours < 24) return formatNum(hours) + " ساعت پیش";
    const days = Math.floor(hours / 24);
    return formatNum(days) + " روز پیش";
  }

  function hoursToDaysLabel(hours) {
    const h = Number(hours) || 0;
    if (h >= 48) return formatNum((h / 24).toFixed(1)) + " روز";
    return formatNum(h.toFixed(1)) + " ساعت";
  }

  function formatAvgDays(value) {
    const n = Number(value) || 0;
    return formatNum(n.toFixed(1)) + " روز";
  }

  function renderDepartmentSummaryChips(employees) {
    if (!employees.length) return "";
    const totalActions = employees.reduce((sum, e) => sum + (Number(pick(e, "totalTasksResolved", "TotalTasksResolved")) || 0), 0);
    const avgAll =
      employees.reduce((sum, e) => sum + (Number(pick(e, "averageResolutionDays", "AverageResolutionDays")) || 0), 0) /
      employees.length;
    return (
      '<div class="kpi-dept-summary">' +
      '<span class="kpi-dept-chip"><strong>' +
      formatNum(employees.length) +
      "</strong> نفر</span>" +
      '<span class="kpi-dept-chip">میانگین واحد: <strong>' +
      formatAvgDays(avgAll) +
      "</strong></span>" +
      '<span class="kpi-dept-chip">کل اقدامات: <strong>' +
      formatNum(totalActions) +
      "</strong></span>" +
      "</div>"
    );
  }

  function filterSlaDepartments(departments) {
    return (departments || []).filter((dept) => {
      const key = String(pick(dept, "departmentKey", "DepartmentKey") || "").toLowerCase();
      return key !== "management";
    });
  }

  function renderDepartmentSection(dept, idx) {
    const key = pick(dept, "departmentKey", "DepartmentKey") || "dept";
    const title = pick(dept, "departmentTitle", "DepartmentTitle") || key;
    const employees = pick(dept, "employees", "Employees") || [];
    if (!employees.length) return "";

    const rows = employees
      .map((emp) => {
        const name = pick(emp, "fullName", "FullName") || pick(emp, "userId", "UserId") || "—";
        const avg = pick(emp, "averageResolutionDays", "AverageResolutionDays") || 0;
        const min = pick(emp, "minResolutionHours", "MinResolutionHours") || 0;
        const max = pick(emp, "maxResolutionHours", "MaxResolutionHours") || 0;
        const total = pick(emp, "totalTasksResolved", "TotalTasksResolved") || 0;
        return (
          "<tr><td>" +
          name +
          "</td><td>" +
          formatAvgDays(avg) +
          "</td><td>" +
          hoursToDaysLabel(min) +
          "</td><td>" +
          hoursToDaysLabel(max) +
          "</td><td>" +
          formatNum(total) +
          "</td></tr>"
        );
      })
      .join("");

    return (
      '<section class="kpi-dept-section" data-dept="' +
      key +
      '">' +
      '<div class="kpi-dept-section__head"><h3>' +
      title +
      '</h3></div>' +
      renderDepartmentSummaryChips(employees) +
      '<div class="kpi-dept-section__charts">' +
      '<div class="kpi-chart-card">' +
      '<div class="kpi-chart-card__title">میانگین زمان رسیدگی هر کارمند</div>' +
      '<p class="kpi-chart-card__hint muted">هر میله = میانگین روزهای رسیدگی به اقدامات در بازه انتخاب‌شده</p>' +
      '<div class="kpi-chart-card__canvas"><canvas id="kpiBar_' +
      idx +
      '"></canvas></div></div>' +
      '<div class="kpi-chart-card">' +
      '<div class="kpi-chart-card__title">محدوده زمان رسیدگی (حداقل / میانگین / حداکثر)</div>' +
      '<p class="kpi-chart-card__hint muted">مقایسه سریع پراکندگی SLA بین کارمندان — همان داده جدول</p>' +
      '<div class="kpi-chart-card__canvas"><canvas id="kpiRange_' +
      idx +
      '"></canvas></div></div>' +
      "</div>" +
      '<div class="kpi-table-wrap"><table class="kpi-table"><thead><tr>' +
      "<th>کارمند</th><th>میانگین رسیدگی</th><th>کوتاه‌ترین</th><th>طولانی‌ترین</th><th>تعداد اقدام</th>" +
      "</tr></thead><tbody>" +
      rows +
      "</tbody></table></div></section>"
    );
  }

  function renderCharts(departments) {
    destroyCharts();
    const colors = ui().CHART_COLORS || ["#6366f1", "#22c55e", "#f59e0b", "#ef4444", "#06b6d4"];

    departments.forEach((dept, idx) => {
      const employees = pick(dept, "employees", "Employees") || [];
      const labels = employees.map(
        (e) => pick(e, "fullName", "FullName") || pick(e, "userId", "UserId") || "—"
      );
      const avgDays = employees.map((e) => Number(pick(e, "averageResolutionDays", "AverageResolutionDays")) || 0);
      const minDays = employees.map((e) => Number(pick(e, "minResolutionHours", "MinResolutionHours") || 0) / 24);
      const maxDays = employees.map((e) => Number(pick(e, "maxResolutionHours", "MaxResolutionHours") || 0) / 24);

      const barCanvas = document.getElementById("kpiBar_" + idx);
      if (barCanvas && labels.length) {
        createChart(barCanvas, {
          type: "bar",
          data: {
            labels,
            datasets: [
              {
                label: "میانگین (روز)",
                data: avgDays,
                backgroundColor: colors[idx % colors.length] + "cc",
                borderRadius: 6,
              },
            ],
          },
          options: {
            ...chartDefaults(),
            indexAxis: "y",
            plugins: {
              ...chartDefaults().plugins,
              legend: { display: false },
              tooltip: {
                ...chartDefaults().plugins.tooltip,
                callbacks: {
                  label(ctx) {
                    return "میانگین: " + formatAvgDays(ctx.parsed.x);
                  },
                },
              },
            },
            scales: {
              x: {
                ticks: { color: "#94a3b8", callback: (v) => formatNum(v) + " روز" },
                grid: { color: "rgba(255,255,255,0.05)" },
                title: { display: true, text: "روز", color: "#94a3b8" },
              },
              y: { ticks: { color: "#94a3b8", font: { size: 11 } }, grid: { display: false } },
            },
          },
        });
      }

      const rangeCanvas = document.getElementById("kpiRange_" + idx);
      if (rangeCanvas && labels.length) {
        createChart(rangeCanvas, {
          type: "bar",
          data: {
            labels,
            datasets: [
              {
                label: "حداقل (روز)",
                data: minDays,
                backgroundColor: "rgba(34,197,94,.75)",
                borderRadius: 4,
              },
              {
                label: "میانگین (روز)",
                data: avgDays,
                backgroundColor: colors[idx % colors.length] + "cc",
                borderRadius: 4,
              },
              {
                label: "حداکثر (روز)",
                data: maxDays,
                backgroundColor: "rgba(239,68,68,.75)",
                borderRadius: 4,
              },
            ],
          },
          options: {
            ...chartDefaults(),
            indexAxis: "y",
            plugins: {
              ...chartDefaults().plugins,
              tooltip: {
                ...chartDefaults().plugins.tooltip,
                callbacks: {
                  label(ctx) {
                    return (ctx.dataset.label || "") + ": " + formatNum(Number(ctx.parsed.x).toFixed(2)) + " روز";
                  },
                },
              },
            },
            scales: {
              x: {
                ticks: { color: "#94a3b8", callback: (v) => formatNum(v) + " روز" },
                grid: { color: "rgba(255,255,255,0.05)" },
                title: { display: true, text: "روز", color: "#94a3b8" },
              },
              y: { ticks: { color: "#94a3b8", font: { size: 11 } }, grid: { display: false } },
            },
          },
        });
      }
    });
  }

  function setLoading(on) {
    state.loading = on;
    qs("#employeeKpiLoading")?.classList.toggle("hidden", !on);
    qs("#employeeKpiContent")?.classList.toggle("employee-kpi__content--loading", on);
  }

  function setError(msg) {
    const el = qs("#employeeKpiError");
    if (!el) return;
    if (msg) {
      el.textContent = msg;
      el.classList.remove("hidden");
    } else {
      el.textContent = "";
      el.classList.add("hidden");
    }
  }

  async function runKpiJob() {
    const meta = qs("#employeeKpiMeta");
    if (meta) meta.textContent = "در حال محاسبه شاخص‌های SLA — لطفاً صبر کنید…";
    await state.panel.apiRequest({
      method: "POST",
      path: "/api/v1/analytics/employee-kpis/run-job",
    });
  }

  async function loadEmployeeKpis(forceRefresh) {
    if (!state.panel?.getActiveSession()?.accessToken) return;
    if (!canViewKpi()) return;

    setLoading(true);
    setError("");

    try {
      if (forceRefresh) await runKpiJob();

      const res = await state.panel.apiRequest({
        method: "GET",
        path: "/api/v1/analytics/employee-kpis?period=" + encodeURIComponent(state.period),
      });
      const raw = unwrap(res.body);
      state.data = raw;

      const computedAt = pick(raw, "computedAtUtc", "ComputedAtUtc");
      const isStale = !!pick(raw, "isStale", "IsStale");
      const meta = qs("#employeeKpiMeta");
      if (meta) {
        meta.textContent =
          "زمان رسیدگی از تاریخچه گردش کار محاسبه شده است · آخرین به‌روزرسانی: " +
          formatRelativeTime(computedAt) +
          (isStale ? " · (داده قدیمی — «اجرای محاسبه» را بزنید)" : "");
      }

      const departments = filterSlaDepartments(pick(raw, "departments", "Departments"));
      const host = qs("#employeeKpiDepartments");
      if (host) {
        if (!departments.length) {
          host.innerHTML =
            '<p class="muted">برای این بازه داده‌ای ثبت نشده است. با «اجرای محاسبه» می‌توانید محاسبه را فوری انجام دهید.</p>';
        } else {
          host.innerHTML = departments.map(renderDepartmentSection).join("");
          renderCharts(departments);
        }
      }
    } catch (err) {
      setError(err?.message || "بارگذاری شاخص‌های SLA ناموفق بود.");
      qs("#employeeKpiDepartments").innerHTML = "";
      destroyCharts();
    } finally {
      setLoading(false);
    }
  }

  function switchSubTab(tab) {
    const overview = qs("#homeOverviewPanel");
    const kpi = qs("#homeEmployeeKpiPanel");
    const tabOverview = qs("#homeTabOverview");
    const tabKpi = qs("#homeTabEmployeeKpi");

    const showKpi = tab === "kpi";
    overview?.classList.toggle("hidden", showKpi);
    kpi?.classList.toggle("hidden", !showKpi);
    tabOverview?.classList.toggle("is-active", !showKpi);
    tabKpi?.classList.toggle("is-active", showKpi);

    if (showKpi && !state.data) void loadEmployeeKpis(false);
  }

  function updateVisibility() {
    const show = canViewKpi();
    qs("#homeTabEmployeeKpi")?.classList.toggle("hidden", !show);
    if (!show && qs("#homeEmployeeKpiPanel") && !qs("#homeEmployeeKpiPanel").classList.contains("hidden")) {
      switchSubTab("overview");
    }
  }

  function wire() {
    qs("#homeTabOverview")?.addEventListener("click", () => switchSubTab("overview"));
    qs("#homeTabEmployeeKpi")?.addEventListener("click", () => switchSubTab("kpi"));

    qs("#employeeKpiPeriod")?.addEventListener("change", (e) => {
      state.period = e.target.value || "Last30Days";
      state.data = null;
      if (!qs("#homeEmployeeKpiPanel")?.classList.contains("hidden")) {
        void loadEmployeeKpis(false);
      }
    });

    qs("#btnEmployeeKpiRefresh")?.addEventListener("click", () => {
      void loadEmployeeKpis(true);
    });

    document.addEventListener("testpanel:session-changed", () => {
      updateVisibility();
      state.data = null;
      if (!qs("#homeEmployeeKpiPanel")?.classList.contains("hidden")) {
        void loadEmployeeKpis(false);
      }
    });

    document.querySelector('[data-tab="tabDashboard"]')?.addEventListener("click", () => {
      updateVisibility();
    });
  }

  window.initEmployeeKpiDashboard = function initEmployeeKpiDashboard(panel) {
    state.panel = panel;
    wire();
    updateVisibility();
  };
})();
