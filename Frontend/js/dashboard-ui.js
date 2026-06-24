(function () {
  const CHART_COLORS = [
    "#6366f1", "#22c55e", "#f59e0b", "#ef4444", "#06b6d4",
    "#8b5cf6", "#ec4899", "#14b8a6", "#f97316", "#3b82f6",
  ];

  const MODULE_ORDER = ["Guarantee", "Investment", "Loan"];
  const MODULE_ICONS = { Guarantee: "🛡", Investment: "📈", Loan: "💳" };

  const PERSIAN_MONTHS = [
    "",
    "فروردین",
    "اردیبهشت",
    "خرداد",
    "تیر",
    "مرداد",
    "شهریور",
    "مهر",
    "آبان",
    "آذر",
    "دی",
    "بهمن",
    "اسفند",
  ];

  const DASHBOARD_LABELS = {
    viewTypes: {
      Executive: "مدیریت و اجرایی",
      Department: "واحد سازمانی",
      Applicant: "متقاضی",
    },
    roleViews: {
      admin: "مدیریت سیستم",
      ceo: "مدیرعامل",
      board: "هیئت مدیره",
      executive: "نمای اجرایی",
      department: "واحد سازمانی",
      applicant: "متقاضی",
      me: "شخصی (نقش من)",
    },
    kpi: {
      activeGuarantees: "مبلغ ضمانت‌نامه‌های جاری",
      activeInvestments: "مبلغ سرمایه‌گذاری‌های جاری",
      activeLoans: "مبلغ تسهیلات جاری",
      activeCases: "تعداد پرونده‌های جاری",
      completedCases: "تعداد پرونده‌های تکمیل‌شده",
      completionRate: "نرخ تکمیل پرونده‌ها",
      onlineUsers: "کاربران آنلاین",
      dailyActiveUsers: "کاربران فعال امروز",
      pendingCeo: "در انتظار تأیید مدیرعامل",
      avgReviewDays: "میانگین روز ماندگاری در بررسی",
      totalCases: "تعداد کل پرونده‌ها",
      totalRisk: "مجموع مبلغ پرونده‌های جاری",
      requestedAmount: "مجموع مبلغ درخواستی",
      approvedPayments: "مجموع پرداخت‌های تأییدشده",
      rejectedCount: "تعداد پرونده‌های رد‌شده",
      applicants: "تعداد متقاضیان",
      department: "واحد",
      queueCount: "پرونده در صف کاری",
      revisionRate: "نرخ درخواست اصلاح",
      remainingDebt: "مانده بدهی",
      unpaidInstallments: "اقساط پرداخت‌نشده",
      pendingActions: "اقدام‌های در انتظار شما",
      activeCaseCount: "پرونده‌های فعال",
      casesThisMonth: "پرونده‌های ثبت‌شده این ماه",
      waitingPayment: "در انتظار پرداخت",
    },
    module: {
      activeVolume: "مبلغ پرونده‌های جاری",
      activeCases: "پرونده جاری",
      completedCases: "تکمیل‌شده",
      completionRate: "نرخ تکمیل",
      pendingCeo: "در انتظار مدیرعامل",
      rejected: "رد‌شده",
      queue: "صف واحد",
      pipelineTitle: "پراکندگی بر اساس وضعیت",
      trendTitle: "روند ثبت پرونده (ماهانه)",
      sectionHint: "خلاصه شاخص‌ها، وضعیت‌ها و روند ثبت",
    },
    panels: {
      bottlenecks: "میانگین زمان ماندگاری در واحدها",
      bottlenecksHint: "هرچه نوار بلندتر باشد، پرونده‌ها بیشتر در آن واحد معطل می‌مانند.",
      activity: "آخرین فعالیت‌های سیستم",
      inbox: "کارتابل — نیاز به اقدام",
      applicantCases: "پرونده‌های در جریان",
      applicantComments: "آخرین نظرات",
      departments: "واحدهای سازمانی",
      fundCredit: "سقف اعتبار دوره‌ای صندوق",
      deptMetrics: "شاخص‌های تخصصی واحد",
    },
    systemHealth: {
      onlineUsers: "کاربران آنلاین",
      dailyActive: "کاربران فعال امروز",
      activeSessions: "نشست‌های فعال",
    },
    charts: {
      statusMix: "ترکیب وضعیت پرونده‌ها",
      monthlyFinancial: "خروجی مالی ماهانه",
      monthlyCases: "روند ثبت پرونده (ماهانه)",
      queueByModule: "صف کاری به تفکیک ماژول",
      moduleVolumes: "مقایسه مبلغ پرونده‌های جاری",
      pipelineTop: "پرونده‌ها بر اساس وضعیت",
      phaseMix: "توزیع بر اساس فاز",
    },
    empty: {
      noData: "داده‌ای برای نمایش موجود نیست.",
      noModules: "داده ماژول‌ها موجود نیست.",
      noInbox: "موردی در صف اقدام شما نیست.",
      noActivity: "فعالیتی ثبت نشده است.",
      noComments: "نظری ثبت نشده است.",
      noActiveCases: "پرونده فعالی ندارید.",
    },
    meta: {
      stale: " · (داده قدیمی — تازه‌سازی کش توصیه می‌شود)",
      updated: " · آخرین به‌روزرسانی: ",
      viewPrefix: "نمای ",
    },
  };

  const DEPARTMENT_HIDDEN_MODULES = {
    Credit: ["Investment"],
    Investment: ["Guarantee", "Loan", "GuaranteeRenewal"],
  };

  function filterModulesForDepartment(modules, departmentKey) {
    const hidden = DEPARTMENT_HIDDEN_MODULES[departmentKey] || [];
    if (!hidden.length) return Array.isArray(modules) ? modules : [];
    return (Array.isArray(modules) ? modules : []).filter(
      (m) => !hidden.includes(pick(m, "module", "Module") || "")
    );
  }

  function filterQueueForDepartment(queue, departmentKey) {
    const hidden = DEPARTMENT_HIDDEN_MODULES[departmentKey] || [];
    if (!hidden.length) return Array.isArray(queue) ? queue : [];
    return (Array.isArray(queue) ? queue : []).filter(
      (q) => !hidden.includes(pick(q, "module", "Module") || "")
    );
  }

  function pick(obj, ...keys) {
    if (!obj) return undefined;
    for (const k of keys) {
      if (obj[k] !== undefined && obj[k] !== null) return obj[k];
    }
    return undefined;
  }

  function formatMoney(n) {
    return (Number(n) || 0).toLocaleString("fa-IR") + " ریال";
  }

  function formatNum(n) {
    return (Number(n) || 0).toLocaleString("fa-IR");
  }

  function formatPercent(n) {
    return (Number(n) || 0).toLocaleString("fa-IR") + "٪";
  }

  function formatDays(n) {
    const v = Number(n) || 0;
    return v.toLocaleString("fa-IR") + " روز";
  }

  function formatDate(iso) {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleString("fa-IR");
    } catch {
      return String(iso);
    }
  }

  function formatPersianMonth(year, month) {
    const m = Number(month) || 0;
    const y = year != null && year !== "" ? String(year) : "";
    if (m >= 1 && m <= 12) {
      return y ? PERSIAN_MONTHS[m] + " " + y : PERSIAN_MONTHS[m];
    }
    return y ? y + "/" + m : String(m || "—");
  }

  function viewTypeLabel(viewType) {
    return DASHBOARD_LABELS.viewTypes[viewType] || viewType || "—";
  }

  function roleViewLabel(roleId) {
    return DASHBOARD_LABELS.roleViews[roleId] || roleId || "—";
  }

  function buildMetaText(options) {
    const parts = [];
    if (options.roleLabel) parts.push(DASHBOARD_LABELS.meta.viewPrefix + options.roleLabel);
    if (options.viewType) parts.push(" (" + viewTypeLabel(options.viewType) + ")");
    if (options.computedAt) parts.push(DASHBOARD_LABELS.meta.updated + formatDate(options.computedAt));
    if (options.isStale) parts.push(DASHBOARD_LABELS.meta.stale);
    return parts.join("");
  }

  function chartDefaults(extra) {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: "bottom",
          labels: { color: "#cbd5e1", padding: 12, usePointStyle: true, pointStyle: "circle", font: { size: 11 } },
        },
        tooltip: {
          backgroundColor: "rgba(15,23,42,.92)",
          titleColor: "#f1f5f9",
          bodyColor: "#cbd5e1",
          borderColor: "rgba(99,102,241,.4)",
          borderWidth: 1,
          padding: 12,
          cornerRadius: 8,
          callbacks: {
            label(context) {
              const label = context.dataset.label || "";
              const value = context.parsed.y ?? context.parsed.x ?? context.parsed ?? context.raw;
              const formatted = typeof value === "number" ? formatNum(value) : value;
              return label ? label + ": " + formatted : formatted;
            },
          },
        },
        ...(extra || {}),
      },
    };
  }

  function scaleDefaults(horizontal) {
    return {
      x: {
        ticks: { color: "#94a3b8", font: { size: 11 } },
        grid: { color: "rgba(255,255,255,0.05)" },
      },
      y: {
        ticks: { color: "#94a3b8", font: { size: 11 } },
        grid: { color: horizontal ? "rgba(255,255,255,0.05)" : "rgba(255,255,255,0.05)" },
      },
    };
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
          borderColor: "rgba(15,23,42,.85)",
          hoverOffset: 6,
        }],
      },
      options: {
        ...chartDefaults(),
        cutout: "58%",
        plugins: {
          ...chartDefaults().plugins,
          legend: { ...chartDefaults().plugins.legend, position: "bottom" },
          tooltip: {
            ...chartDefaults().plugins.tooltip,
            callbacks: {
              label(context) {
                const total = context.dataset.data.reduce((a, b) => a + Number(b || 0), 0);
                const value = Number(context.raw) || 0;
                const pct = total > 0 ? Math.round((value * 1000) / total) / 10 : 0;
                return (context.label || "") + ": " + formatNum(value) + " (" + formatPercent(pct) + ")";
              },
            },
          },
        },
      },
    };
  }

  function horizontalBarConfig(labels, data, label, colors) {
    return {
      type: "bar",
      data: {
        labels,
        datasets: [{
          label: label || "تعداد",
          data,
          backgroundColor: colors || CHART_COLORS.slice(0, labels.length).map((c) => c + "cc"),
          borderRadius: 6,
          borderSkipped: false,
        }],
      },
      options: {
        ...chartDefaults(),
        indexAxis: "y",
        plugins: { ...chartDefaults().plugins, legend: { display: false } },
        scales: scaleDefaults(true),
      },
    };
  }

  function verticalBarConfig(labels, data, label, colors, moneyAxis) {
    const options = {
      ...chartDefaults(),
      plugins: { ...chartDefaults().plugins, legend: { display: !!label, labels: { color: "#cbd5e1" } } },
      scales: scaleDefaults(false),
    };
    if (moneyAxis) {
      options.plugins = {
        ...options.plugins,
        tooltip: {
          ...chartDefaults().plugins.tooltip,
          callbacks: {
            label(context) {
              return (context.dataset.label || "") + ": " + formatMoney(context.parsed.y);
            },
          },
        },
      };
    }
    return {
      type: "bar",
      data: {
        labels,
        datasets: [{
          label: label || "",
          data,
          backgroundColor: colors || CHART_COLORS.slice(0, Math.max(labels.length, 1)).map((c) => c + "cc"),
          borderRadius: 6,
          borderSkipped: false,
        }],
      },
      options,
    };
  }

  function lineConfig(labels, data, label, color) {
    const c = color || "#6366f1";
    return {
      type: "line",
      data: {
        labels,
        datasets: [{
          label: label || "تعداد پرونده",
          data,
          borderColor: c,
          backgroundColor: c + "26",
          fill: true,
          tension: 0.35,
          pointRadius: 4,
          pointBackgroundColor: c,
        }],
      },
      options: {
        ...chartDefaults(),
        plugins: { ...chartDefaults().plugins, legend: { display: !!label, labels: { color: "#cbd5e1" } } },
        scales: scaleDefaults(false),
      },
    };
  }

  function fixLineConfig(config, color) {
    config.data.datasets[0].borderColor = color;
    config.data.datasets[0].pointBackgroundColor = color;
    config.data.datasets[0].backgroundColor = color + "26";
    return config;
  }

  function buildChartConfig(spec) {
    if (!spec) return null;
    const labels = spec.labels || [];
    const data = spec.data || [];
    if (!labels.length && !data.length) return null;

    if (spec.type === "doughnut" || spec.type === "pie") {
      return doughnutConfig(labels, data, spec.colors);
    }
    if (spec.type === "line") {
      return fixLineConfig(lineConfig(labels, data, spec.datasetLabel, spec.color), spec.color || CHART_COLORS[0]);
    }
    if (spec.horizontal || spec.type === "horizontalBar") {
      return horizontalBarConfig(labels, data, spec.datasetLabel, spec.colors);
    }
    return verticalBarConfig(labels, data, spec.datasetLabel, spec.colors, spec.moneyAxis);
  }

  function createChart(canvas, config, registry) {
    if (!canvas || typeof Chart === "undefined" || !config) return null;
    const chart = new Chart(canvas, config);
    if (registry) registry.push(chart);
    return chart;
  }

  function normalizeModules(modules, departmentKey) {
    const list = filterModulesForDepartment(modules, departmentKey);
    return MODULE_ORDER.map((key) => list.find((m) => (pick(m, "module", "Module") || "") === key)).filter(Boolean);
  }

  function renderStatusPipelineChart(parent, pipeline, canvasId, chartRegistry) {
    const rows = (pipeline || []).filter((r) => (pick(r, "count", "Count") || 0) > 0);
    if (!rows.length) {
      parent.appendChild(el("p", "muted", DASHBOARD_LABELS.empty.noData));
      return;
    }
    const sorted = rows.slice().sort((a, b) => (pick(b, "count", "Count") || 0) - (pick(a, "count", "Count") || 0));
    const top = sorted.slice(0, 12);
    const wrap = el("div", "module-section__canvas");
    const canvas = document.createElement("canvas");
    canvas.id = canvasId;
    wrap.appendChild(canvas);
    parent.appendChild(wrap);
    createChart(
      canvas,
      horizontalBarConfig(
        top.map((r) => pick(r, "statusTitle", "StatusTitle") || "—"),
        top.map((r) => pick(r, "count", "Count") || 0),
        "تعداد پرونده"
      ),
      chartRegistry
    );
  }

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  function renderStatusTable(pipeline) {
    const rows = pipeline || [];
    if (!rows.length) return '<p class="muted">' + DASHBOARD_LABELS.empty.noData + "</p>";
    return (
      '<table class="dashboard-status-table"><thead><tr><th>وضعیت</th><th>تعداد</th></tr></thead><tbody>' +
      rows
        .map((r) => {
          const title = pick(r, "statusTitle", "StatusTitle") || "";
          const count = pick(r, "count", "Count") || 0;
          return "<tr><td>" + title + '</td><td class="mono">' + formatNum(count) + "</td></tr>";
        })
        .join("") +
      "</tbody></table>"
    );
  }

  function getDepartmentMetricItems(metrics, departmentKey) {
    if (!metrics) return [];
    const key = (departmentKey || "").toLowerCase();
    const items = [];

    if (key === "financial") {
      items.push(
        { label: "مجموع کارمزد", value: formatMoney(pick(metrics, "totalCommissions", "TotalCommissions")) },
        { label: "مجموع بازپرداخت", value: formatMoney(pick(metrics, "totalRepayments", "TotalRepayments")) },
        { label: "اقساط معوق", value: formatNum(pick(metrics, "overdueInstallmentsCount", "OverdueInstallmentsCount")) + " قسط" },
        { label: "مبلغ معوق", value: formatMoney(pick(metrics, "overdueAmount", "OverdueAmount")) },
        { label: "بررسی مالی معلق", value: formatNum(pick(metrics, "pendingFinancialReviews", "PendingFinancialReviews")) + " پرونده" }
      );
    } else if (key === "legal") {
      items.push(
        { label: "قرارداد در انتظار بررسی", value: formatNum(pick(metrics, "contractsPendingReview", "ContractsPendingReview")) + " مورد" },
        { label: "پرونده در فاز حقوقی", value: formatNum(pick(metrics, "casesInLegalPhase", "CasesInLegalPhase")) + " پرونده" },
        { label: "آپلود قرارداد امضاشده", value: formatNum(pick(metrics, "pendingSignedContractUploads", "PendingSignedContractUploads")) + " مورد" }
      );
    } else if (key === "credit") {
      items.push(
        { label: "بررسی اعتبارات معلق", value: formatNum(pick(metrics, "pendingCreditReviews", "PendingCreditReviews")) + " پرونده" },
        { label: "درخواست اصلاح (۶ ماه اخیر)", value: formatNum(pick(metrics, "revisionCountLast6Months", "RevisionCountLast6Months")) + " مورد" }
      );
    } else if (key === "investment") {
      items.push(
        { label: "ارزش‌گذاری معلق", value: formatNum(pick(metrics, "pendingValuations", "PendingValuations")) + " پرونده" },
        { label: "در انتظار پرداخت", value: formatNum(pick(metrics, "waitingPaymentCount", "WaitingPaymentCount")) + " پرونده" }
      );
    } else if (key === "technical") {
      items.push(
        { label: "سقف اعتبار فعال", value: formatNum(pick(metrics, "activeFundCreditPools", "ActiveFundCreditPools")) + " دوره" }
      );
    }

    return items;
  }

  function renderMetricRows(items, className) {
    if (!items.length) return "";
    const wrapClass = className || "dept-metric-rows";
    return (
      '<div class="' +
      wrapClass +
      '">' +
      items
        .map(
          (it) =>
            '<div class="dept-metric-row">' +
            '<span class="dept-metric-row__label">' +
            it.label +
            "</span>" +
            '<strong class="dept-metric-row__value">' +
            it.value +
            "</strong></div>"
        )
        .join("") +
      "</div>"
    );
  }

  function renderDepartmentSummaryCard(dept, maxQueue) {
    const title = pick(dept, "departmentTitle", "DepartmentTitle") || "—";
    const departmentKey = pick(dept, "departmentKey", "DepartmentKey") || "";
    const queue = Number(pick(dept, "totalQueueCount", "TotalQueueCount")) || 0;
    const revision = Number(pick(dept, "revisionRatePercent", "RevisionRatePercent")) || 0;
    const sm = pick(dept, "specificMetrics", "SpecificMetrics");
    const queuePct = maxQueue > 0 ? Math.min(100, Math.round((queue / maxQueue) * 100)) : 0;
    const specificItems = getDepartmentMetricItems(sm, departmentKey);

    const coreRows = [
      {
        label: "پرونده در صف کاری",
        value: formatNum(queue),
        bar: queuePct,
        barClass: queue > 0 ? "dept-queue-bar__fill--active" : "",
      },
      {
        label: "شاخص درخواست اصلاح",
        value: formatPercent(revision),
        hint: "تعداد درخواست‌های اصلاح ÷ پرونده‌های صف (۶ ماه اخیر)",
      },
    ];

    let html =
      '<article class="dept-summary-card">' +
      '<header class="dept-summary-card__head"><h4 class="dept-summary-card__title">' +
      title +
      "</h4></header>" +
      '<div class="dept-summary-card__body">';

    coreRows.forEach((row) => {
      html +=
        '<div class="dept-metric-row dept-metric-row--core">' +
        '<span class="dept-metric-row__label">' +
        row.label +
        "</span>" +
        '<strong class="dept-metric-row__value">' +
        row.value +
        "</strong>";
      if (row.bar != null) {
        html +=
          '<div class="dept-queue-bar" aria-hidden="true"><div class="dept-queue-bar__fill ' +
          (row.barClass || "") +
          '" style="width:' +
          row.bar +
          '%"></div></div>';
      }
      if (row.hint) {
        html += '<span class="dept-metric-row__hint muted">' + row.hint + "</span>";
      }
      html += "</div>";
    });

    if (specificItems.length) {
      html += '<div class="dept-summary-card__divider"></div>' + renderMetricRows(specificItems, "dept-metric-rows dept-metric-rows--specific");
    }

    html += "</div></article>";
    return html;
  }

  function renderDepartmentGrid(departments) {
    const list = Array.isArray(departments) ? departments : [];
    if (!list.length) {
      return '<p class="muted">' + DASHBOARD_LABELS.empty.noData + "</p>";
    }
    const maxQueue = Math.max(1, ...list.map((d) => Number(pick(d, "totalQueueCount", "TotalQueueCount")) || 0));
    return (
      '<div class="dept-summary-grid">' +
      list.map((dept) => renderDepartmentSummaryCard(dept, maxQueue)).join("") +
      "</div>"
    );
  }

  function renderDepartmentMetrics(metrics, departmentKey) {
    const items = getDepartmentMetricItems(metrics, departmentKey);
    if (!items.length) return "";
    return (
      '<div class="dept-metrics-strip">' +
      items
        .map(
          (it) =>
            '<div class="dept-metric"><span class="dept-metric__label">' +
            it.label +
            '</span><strong class="dept-metric__value">' +
            it.value +
            "</strong></div>"
        )
        .join("") +
      "</div>"
    );
  }

  function renderSystemHealth(health) {
    if (!health) return "";
    const L = DASHBOARD_LABELS.systemHealth;
    return (
      '<div class="system-health-strip">' +
      '<div class="system-health-card"><span class="muted">' +
      L.onlineUsers +
      '</span><strong>' +
      formatNum(pick(health, "onlineUsersCount", "OnlineUsersCount")) +
      "</strong></div>" +
      '<div class="system-health-card"><span class="muted">' +
      L.dailyActive +
      '</span><strong>' +
      formatNum(pick(health, "dailyActiveUsers", "DailyActiveUsers")) +
      "</strong></div>" +
      '<div class="system-health-card"><span class="muted">' +
      L.activeSessions +
      '</span><strong>' +
      formatNum(pick(health, "activeSessionsCount", "ActiveSessionsCount")) +
      "</strong></div></div>"
    );
  }

  function renderBottleneckBars(bottlenecks) {
    if (!bottlenecks?.length) {
      return '<p class="muted">' + DASHBOARD_LABELS.empty.noData + "</p>";
    }
    return bottlenecks
      .map((b) => {
        const title = pick(b, "departmentTitle", "DepartmentTitle") || "";
        const days = pick(b, "averageDays", "AverageDays") || 0;
        const cnt = pick(b, "activeCaseCount", "ActiveCaseCount") || 0;
        const pct = Math.min(100, Math.round(days * 3));
        return (
          '<div class="dashboard-bar">' +
          '<div class="dashboard-bar__label"><span>' +
          title +
          ' <span class="muted">(' +
          formatNum(cnt) +
          ' پرونده جاری)</span></span><span class="mono">' +
          formatDays(days) +
          '</span></div><div class="dashboard-bar__track"><div class="dashboard-bar__fill dashboard-bar__fill--warn" style="width:' +
          pct +
          '%"></div></div></div>'
        );
      })
      .join("");
  }

  function renderModuleSections(host, modules, chartRegistry, idPrefix, departmentKey) {
    if (!host) return;
    host.innerHTML = "";
    const ordered = normalizeModules(modules, departmentKey);
    if (!ordered.length) {
      host.innerHTML = '<p class="muted">' + DASHBOARD_LABELS.empty.noModules + "</p>";
      return;
    }

    const prefix = idPrefix || "modChart";
    const L = DASHBOARD_LABELS.module;
    const K = DASHBOARD_LABELS.kpi;

    ordered.forEach((mod, idx) => {
      const moduleKey = (pick(mod, "module", "Module") || "").toLowerCase();
      const title = pick(mod, "moduleTitle", "ModuleTitle") || moduleKey;
      const icon = MODULE_ICONS[pick(mod, "module", "Module")] || "📊";
      const trend = pick(mod, "monthlyTrend", "MonthlyTrend") || [];
      const pipeline = pick(mod, "pipelineByStatus", "PipelineByStatus") || [];

      const section = document.createElement("section");
      section.className = "module-section";
      section.dataset.module = moduleKey;
      section.innerHTML =
        '<div class="module-section__head"><span class="module-section__icon">' +
        icon +
        '</span><div><h3 class="module-section__title">' +
        title +
        '</h3><p class="muted module-section__sub">' +
        L.sectionHint +
        "</p></div></div>" +
        '<div class="module-section__kpis">' +
        '<div class="module-kpi"><span>' +
        L.activeVolume +
        '</span><strong>' +
        formatMoney(pick(mod, "activeVolume", "ActiveVolume")) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        L.activeCases +
        '</span><strong>' +
        formatNum(pick(mod, "activeCases", "ActiveCases")) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        L.completedCases +
        '</span><strong>' +
        formatNum(pick(mod, "completedCases", "CompletedCases")) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        L.completionRate +
        '</span><strong>' +
        formatPercent(pick(mod, "completionRate", "CompletionRate") || 0) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        K.pendingCeo +
        '</span><strong>' +
        formatNum(pick(mod, "pendingCeoApprovals", "PendingCeoApprovals")) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        L.rejected +
        '</span><strong>' +
        formatNum(pick(mod, "rejectedCount", "RejectedCount")) +
        "</strong></div>" +
        '<div class="module-kpi"><span>' +
        L.queue +
        '</span><strong>' +
        formatNum(pick(mod, "queueCount", "QueueCount")) +
        "</strong></div></div>" +
        '<div class="module-section__body">' +
        '<div class="module-section__chart"><div class="module-section__block-title">' +
        L.pipelineTitle +
        '</div><div id="' +
        prefix +
        "_pipe_" +
        idx +
        '"></div></div>' +
        '<div class="module-section__chart"><div class="module-section__block-title">' +
        L.trendTitle +
        '</div><div class="module-section__canvas"><canvas id="' +
        prefix +
        "_" +
        idx +
        '"></canvas></div></div></div>';

      host.appendChild(section);

      const pipeHost = document.getElementById(prefix + "_pipe_" + idx);
      renderStatusPipelineChart(pipeHost, pipeline, prefix + "_pipeChart_" + idx, chartRegistry);

      const labels = trend.map((m) => formatPersianMonth(pick(m, "year", "Year"), pick(m, "month", "Month")));
      const data = trend.map((m) => pick(m, "count", "Count") || 0);
      if (labels.length && typeof Chart !== "undefined") {
        const canvas = document.getElementById(prefix + "_" + idx);
        const color = CHART_COLORS[idx % CHART_COLORS.length];
        createChart(canvas, fixLineConfig(lineConfig(labels, data, "تعداد پرونده جدید"), color), chartRegistry);
      }
    });
  }

  window.DashboardUi = {
    pick,
    formatMoney,
    formatNum,
    formatPercent,
    formatDays,
    formatDate,
    formatPersianMonth,
    viewTypeLabel,
    roleViewLabel,
    buildMetaText,
    normalizeModules,
    filterModulesForDepartment,
    filterQueueForDepartment,
    renderModuleSections,
    renderStatusTable,
    renderDepartmentMetrics,
    renderDepartmentGrid,
    renderDepartmentSummaryCard,
    getDepartmentMetricItems,
    renderMetricRows,
    renderSystemHealth,
    renderBottleneckBars,
    createChart,
    buildChartConfig,
    lineConfig,
    doughnutConfig,
    horizontalBarConfig,
    verticalBarConfig,
    fixLineConfig,
    CHART_COLORS,
    DASHBOARD_LABELS,
  };
})();
