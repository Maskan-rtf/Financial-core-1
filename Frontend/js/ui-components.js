/* global WorkflowModel, GuaranteeWorkflowModel, LoanWorkflowModel */
(function () {
  function el(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }

  function pick(obj, camel, pascal) {
    if (!obj) return "";
    const v = obj[camel] ?? obj[pascal];
    return v == null ? "" : v;
  }

  function formatDate(value) {
    if (!value) return "—";
    try {
      return new Date(value).toLocaleString("fa-IR");
    } catch {
      return String(value);
    }
  }

  const APPLICANT_TYPE_BY_KEY = { Individual: 1, Company: 2 };

  function coerceApplicantType(value) {
    if (value == null || value === "") return 0;
    const n = Number(value);
    if (!Number.isNaN(n)) return n;
    return APPLICANT_TYPE_BY_KEY[String(value)] ?? 0;
  }

  function statusTitle(module, status, options) {
    options = options || {};
    if (module === "guarantee" && window.GuaranteeWorkflowModel) {
      let amendmentType = options.amendmentType;
      if ((amendmentType == null || amendmentType === "") && options.case) {
        amendmentType = GuaranteeWorkflowModel.amendmentTypeFromCase(options.case);
      }
      if (typeof GuaranteeWorkflowModel.coerceAmendmentType === "function") {
        amendmentType = GuaranteeWorkflowModel.coerceAmendmentType(amendmentType);
      } else if (amendmentType != null && amendmentType !== "") {
        amendmentType = Number(amendmentType);
      } else {
        amendmentType = 0;
      }
      const context = amendmentType > 0 ? { amendmentType } : undefined;
      const step = GuaranteeWorkflowModel.stepForStatus(status, context);
      return step ? step.title : String(status);
    }
    if (module === "loan" && window.LoanWorkflowModel) {
      const value =
        typeof LoanWorkflowModel.coerceStatus === "function"
          ? LoanWorkflowModel.coerceStatus(status)
          : Number(status);
      const step = LoanWorkflowModel.stepForStatus(value);
      return step ? step.title : String(status);
    }
    if (window.WorkflowModel && typeof WorkflowModel.getStep === "function") {
      const value =
        typeof WorkflowModel.coerceStatus === "function"
          ? WorkflowModel.coerceStatus(status)
          : Number(status);
      const step = WorkflowModel.getStep(value);
      if (step) return step.title;
    }
    return String(status);
  }

  function pickCompany(obj) {
    if (!obj) return null;
    return obj.company || obj.Company || null;
  }

  function applicantTypeOf(obj) {
    return coerceApplicantType(pick(obj, "applicantType", "ApplicantType"));
  }

  function companySummary(obj) {
    const company = pickCompany(obj);
    if (!company) return "—";
    const name = pick(company, "name", "Name");
    const nationalId = pick(company, "nationalId", "NationalId");
    if (name && nationalId) return name + " · " + nationalId;
    return name || nationalId || "—";
  }

  function applicantLabelFromCase(obj) {
    const company = pickCompany(obj);
    if (company && applicantTypeOf(obj) === 2) {
      return pick(company, "name", "Name") || "شرکت";
    }
    const applicantFullName = pick(obj, "applicantFullName", "ApplicantFullName");
    if (applicantFullName) return applicantFullName;
    const applicant = obj && (obj.applicant || obj.Applicant);
    if (applicant) return pick(applicant, "fullName", "FullName") || "متقاضی";
    return applicantTypeOf(obj) === 2 ? "حقوقی" : "حقیقی";
  }

  function caseSubjectFromCase(module, obj) {
    const title = pick(obj, "title", "Title");
    if (title && String(title).trim()) return String(title).trim();

    if (module === "investment") {
      const company = pickCompany(obj);
      if (company && applicantTypeOf(obj) === 2) {
        return pick(company, "name", "Name") || "سرمایه‌گذاری";
      }
      return pick(obj, "startupTitle", "StartupTitle") || pick(obj, "subject", "Subject") || "سرمایه‌گذاری";
    }
    if (module === "loan") {
      const app = obj.application || obj.Application;
      return pick(app, "facilitySubject", "FacilitySubject") || "تسهیلات";
    }
    return pick(obj, "guaranteeTypeTitle", "GuaranteeTypeTitle") || "ضمانت‌نامه";
  }

  function renderCaseTable(host, rows, opts) {
    if (!host) return;
    host.innerHTML = "";
    if (!rows.length) {
      host.appendChild(el("div", "portal-empty", opts.emptyText || "پرونده‌ای یافت نشد."));
      return;
    }
    const wrap = el("div", "registry-table-wrap");
    const table = el("table", "registry-table");
    const colLabels = ["شماره پرونده", "موضوع", "شرکت", "متقاضی", "وضعیت", ""];
    const thead = el("thead");
    const hr = el("tr");
    colLabels.forEach((h) => {
      const th = el("th", "", h);
      hr.appendChild(th);
    });
    thead.appendChild(hr);
    table.appendChild(thead);
    const tbody = el("tbody");
    rows.forEach((row) => {
      const tr = el("tr");
      const cells = [
        { cls: "mono", val: row.caseNumber || "—" },
        { cls: "", val: row.subject || "—" },
        { cls: "", val: row.company || "—" },
        { cls: "", val: row.applicant || "—" },
        { cls: "", val: row.statusLabel || "—" },
      ];
      cells.forEach((c, i) => {
        const td = el("td", c.cls, c.val);
        td.setAttribute("data-label", colLabels[i]);
        tr.appendChild(td);
      });
      const tdAct = el("td");
      tdAct.setAttribute("data-label", "");
      const btn = el("button", "btn btn--primary btn--small", "ورود به پرونده");
      btn.type = "button";
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        if (opts.onEnter) opts.onEnter(row);
      });
      tdAct.appendChild(btn);
      tr.appendChild(tdAct);
      tr.addEventListener("click", () => {
        if (opts.onEnter) opts.onEnter(row);
      });
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    wrap.appendChild(table);
    host.appendChild(wrap);
  }

  function resolveDocTypeLabel(module, type, customLabel) {
    if (customLabel) return customLabel(type);
    const t = Number(type);
    if (module === "guarantee" && window.GuaranteeWorkflowModel) {
      return GuaranteeWorkflowModel.documentTypeLabel(t);
    }
    if (module === "loan" && window.LoanWorkflowModel) {
      return LoanWorkflowModel.documentTypeLabel(t);
    }
    if (window.WorkflowModel) {
      return WorkflowModel.documentTypeLabel(t);
    }
    return "نوع " + t;
  }

  function formatAttachmentMeta(doc, opts) {
    const name = pick(doc, "fileName", "FileName") || "فایل";
    const type = pick(doc, "documentType", "DocumentType");
    const ver = Number(pick(doc, "version", "Version")) || 1;
    const typeLabel = resolveDocTypeLabel(opts.module, type, opts.docTypeLabel);
    const uploadedAt = pick(doc, "uploadedAt", "UploadedAt");
    let text = typeLabel + " — نسخه " + ver + " — " + name;
    if (uploadedAt) text += " · " + formatDate(uploadedAt);
    return text;
  }

  function sortAttachments(docs) {
    return docs.slice().sort((a, b) => {
      const ta = Number(pick(a, "documentType", "DocumentType")) || 0;
      const tb = Number(pick(b, "documentType", "DocumentType")) || 0;
      if (ta !== tb) return ta - tb;
      const va = Number(pick(a, "version", "Version")) || 0;
      const vb = Number(pick(b, "version", "Version")) || 0;
      return vb - va;
    });
  }

  function renderAttachments(host, docs, opts) {
    if (!host) return;
    opts = opts || {};
    host.innerHTML = "";
    const card = el("div", "card");
    card.appendChild(el("div", "card__title", "پیوست‌ها"));
    if (!docs.length) {
      card.appendChild(el("div", "muted", "هنوز فایلی بارگذاری نشده است."));
    } else {
      const list = el("div", "portal-doc-list");
      sortAttachments(docs).forEach((doc) => {
        const item = el("div", "portal-doc-list__item portal-doc-list__item--row");
        const meta = el("div", "portal-doc-list__meta");
        meta.textContent = formatAttachmentMeta(doc, opts);
        item.appendChild(meta);
        const id = pick(doc, "id", "Id");
        if (id && opts.onDownload) {
          const btn = el("button", "btn btn--small", "دانلود");
          btn.type = "button";
          btn.addEventListener("click", () => opts.onDownload(id));
          item.appendChild(btn);
        }
        list.appendChild(item);
      });
      card.appendChild(list);
    }
    if (opts.canUpload && opts.uploadSlot) {
      const zone = el("div", "card portal-card portal-card--nested");
      zone.appendChild(el("div", "card__title", "بارگذاری فایل"));
      zone.appendChild(opts.uploadSlot);
      card.appendChild(zone);
    }
    host.appendChild(card);
  }

  function getPhases(module) {
    if (module === "guarantee" && window.GuaranteeWorkflowModel) return GuaranteeWorkflowModel.PHASES || {};
    if (module === "loan" && window.LoanWorkflowModel) return LoanWorkflowModel.PHASES || {};
    if (window.WorkflowModel) return WorkflowModel.PHASES || {};
    return {};
  }

  function stepMetaForStatus(module, status) {
    let value = Number(status);
    if (module === "loan" && window.LoanWorkflowModel) {
      value =
        typeof LoanWorkflowModel.coerceStatus === "function"
          ? LoanWorkflowModel.coerceStatus(status)
          : Number(status);
      return value ? LoanWorkflowModel.stepForStatus(value) : null;
    }
    if (module === "guarantee" && window.GuaranteeWorkflowModel) {
      value =
        typeof GuaranteeWorkflowModel.coerceStatus === "function"
          ? GuaranteeWorkflowModel.coerceStatus(status)
          : Number(status);
      return value ? GuaranteeWorkflowModel.stepForStatus(value) : null;
    }
    if (window.WorkflowModel && WorkflowModel.getStep) {
      value =
        typeof WorkflowModel.coerceStatus === "function"
          ? WorkflowModel.coerceStatus(status)
          : Number(status);
      return value ? WorkflowModel.getStep(value) : null;
    }
    return null;
  }

  function resolveStatusAtTime(history, createdAt) {
    if (!history || !history.length || !createdAt) return null;
    const target = new Date(createdAt).getTime();
    if (!Number.isFinite(target)) return null;
    const sorted = history.slice().sort(
      (a, b) =>
        new Date(pick(a, "createdAt", "CreatedAt") || 0).getTime() -
        new Date(pick(b, "createdAt", "CreatedAt") || 0).getTime()
    );
    let status = Number(pick(sorted[0], "fromStatus", "FromStatus")) || null;
    sorted.forEach((item) => {
      const at = new Date(pick(item, "createdAt", "CreatedAt") || 0).getTime();
      if (at <= target) status = Number(pick(item, "toStatus", "ToStatus"));
    });
    return status || null;
  }

  function resolveCommentStepMeta(module, comment, history) {
    const phase = Number(pick(comment, "phase", "Phase"));
    const phases = getPhases(module);
    const phaseTitle = phases[phase] || "فاز " + phase;
    const createdAt = pick(comment, "createdAt", "CreatedAt");
    const status = resolveStatusAtTime(history, createdAt);
    const step = stepMetaForStatus(module, status);
    const stepTitle = (step && (step.title || step.Title)) || phaseTitle;
    const unit = (step && (step.unit || step.Unit)) || "all";
    return { phase, phaseTitle, status, stepTitle, unit };
  }

  function buildCommentMetaParts(comment, stepMeta) {
    const parts = [];
    if (stepMeta && stepMeta.stepTitle) parts.push(stepMeta.stepTitle);
    const role = pick(comment, "senderRole", "SenderRole");
    if (role) parts.push(role);
    if (pick(comment, "isRevisionRequest", "IsRevisionRequest")) parts.push("درخواست اصلاح");
    else if (pick(comment, "isInternal", "IsInternal")) parts.push("داخلی");
    parts.push(formatDate(pick(comment, "createdAt", "CreatedAt")));
    return parts;
  }

  let commentStepModalEl = null;

  function closeCommentStepModal() {
    if (commentStepModalEl) {
      commentStepModalEl.remove();
      commentStepModalEl = null;
    }
  }

  function showCommentStepModal(detail) {
    closeCommentStepModal();
    const modal = el("div", "modal portal-comment-step-modal");
    const body = el("div", "modal__body");
    body.appendChild(
      el(
        "div",
        "modal__title",
        "گفتگوی مرحله: " + (detail.stepTitle || detail.phaseTitle || "—")
      )
    );
    body.appendChild(
      el(
        "div",
        "muted portal-stage__hint",
        "نظرات ثبت‌شده در این مرحله — برای مقایسه با صفحه فعلی پرونده، این پنجره را باز نگه دارید."
      )
    );

    const phaseComments = (detail.comments || []).filter(
      (c) => Number(pick(c, "phase", "Phase")) === Number(detail.phase)
    );
    if (!phaseComments.length) {
      body.appendChild(el("div", "muted", "نظری برای این مرحله ثبت نشده است."));
    } else {
      const list = renderCommentThreadList(phaseComments, {
        module: detail.module,
        history: detail.history || [],
        allComments: detail.comments,
        showOpenButton: false,
        highlightCommentId: detail.commentId,
      });
      body.appendChild(list);
    }

    const actions = el("div", "row");
    actions.style.marginTop = "12px";
    const closeBtn = el("button", "btn", "بستن");
    closeBtn.type = "button";
    closeBtn.addEventListener("click", closeCommentStepModal);
    actions.appendChild(closeBtn);
    body.appendChild(actions);
    modal.appendChild(body);
    modal.addEventListener("click", (event) => {
      if (event.target === modal) closeCommentStepModal();
    });
    document.body.appendChild(modal);
    commentStepModalEl = modal;
  }

  function openCommentStepView(detail) {
    if (window.CasesHub && typeof window.CasesHub.setSubTab === "function") {
      window.CasesHub.setSubTab("workflow");
    } else {
      document.querySelector('.case-subtab[data-subtab="workflow"]')?.click();
    }
    document.dispatchEvent(new CustomEvent("testpanel:open-comment-step", { detail }));
    showCommentStepModal(detail);
  }

  function renderCommentThreadList(comments, opts) {
    opts = opts || {};
    const list = el("div", "portal-thread__list");
    if (!comments.length) return list;

    const module = opts.module || "investment";
    const history = opts.history || opts.historyContext || [];
    const showOpen = opts.showOpenButton !== false;

    comments.forEach((comment) => {
      const stepMeta = resolveCommentStepMeta(module, comment, history);
      const commentId = pick(comment, "id", "Id");
      const item = el("div", "portal-thread__item portal-thread__item--with-action");
      item.dataset.commentPhase = String(stepMeta.phase || "");
      if (commentId) item.dataset.commentId = String(commentId);
      if (opts.highlightCommentId && String(opts.highlightCommentId) === String(commentId)) {
        item.classList.add("is-highlight");
      }

      const main = el("div", "portal-thread__item-main");
      const meta = el("div", "portal-thread__meta");
      const stepLine = el("span", "portal-thread__step", stepMeta.stepTitle || stepMeta.phaseTitle || "—");
      meta.appendChild(stepLine);
      meta.appendChild(document.createTextNode(" · " + buildCommentMetaParts(comment, stepMeta).slice(1).join(" · ")));
      main.appendChild(meta);
      main.appendChild(el("div", "portal-thread__message", pick(comment, "message", "Message") || "—"));
      item.appendChild(main);

      if (showOpen) {
        const openBtn = el("button", "btn btn--small", "باز کردن");
        openBtn.type = "button";
        openBtn.addEventListener("click", () => {
          openCommentStepView({
            module,
            phase: stepMeta.phase,
            phaseTitle: stepMeta.phaseTitle,
            status: stepMeta.status,
            stepTitle: stepMeta.stepTitle,
            unit: stepMeta.unit,
            commentId,
            comments: opts.allComments || comments,
            history,
          });
        });
        item.appendChild(openBtn);
      }

      list.appendChild(item);
    });
    return list;
  }

  function renderTimeline(host, history, comments, opts) {
    if (!host) return;
    host.innerHTML = "";
    const card = el("div", "card");
    card.appendChild(el("div", "card__title", "تاریخچه گردش کار"));
    if (!history.length) {
      card.appendChild(el("div", "muted", "رویداد گردش کاری ثبت نشده است."));
    } else {
      const list = el("div", "portal-history");
      history.forEach((item) => {
        const row = el("div", "portal-history__item");
        const action = pick(item, "action", "Action");
        const from = pick(item, "fromStatus", "FromStatus");
        const to = pick(item, "toStatus", "ToStatus") || pick(item, "status", "Status");
        const at = formatDate(pick(item, "createdAt", "CreatedAt"));
        const actor = pick(item, "performedByName", "PerformedByName") || pick(item, "performedByUserId", "PerformedByUserId");
        row.innerHTML =
          "<strong>" +
          (action || "انتقال") +
          "</strong>" +
          (from || to ? " · " + [from, to].filter(Boolean).join(" → ") : "") +
          "<br/><span class='muted'>" +
          at +
          (actor ? " · " + actor : "") +
          "</span>";
        list.appendChild(row);
      });
      card.appendChild(list);
    }
    const cCard = el("div", "card portal-card portal-card--nested");
    cCard.appendChild(el("div", "card__title", "نظرات و درخواست‌های اصلاح"));
    if (!comments.length) {
      cCard.appendChild(el("div", "muted", "نظری ثبت نشده است."));
    } else {
      cCard.appendChild(
        renderCommentThreadList(comments, {
          module: opts.module || "investment",
          history,
          allComments: comments,
        })
      );
    }
    if (opts && opts.onAddComment) {
      const row = el("div", "formrow");
      row.appendChild(el("label", "", "افزودن نظر"));
      const ta = document.createElement("textarea");
      ta.className = "portal-textarea";
      ta.rows = 3;
      row.appendChild(ta);
      cCard.appendChild(row);
      const btn = el("button", "btn btn--primary", "ثبت نظر");
      btn.type = "button";
      btn.addEventListener("click", () => {
        const msg = ta.value.trim();
        if (msg) opts.onAddComment(msg, () => {
          ta.value = "";
        });
      });
      cCard.appendChild(btn);
    }
    host.appendChild(card);
    host.appendChild(cCard);
  }

  window.UIComponents = {
    el,
    pick,
    pickCompany,
    applicantTypeOf,
    companySummary,
    applicantLabelFromCase,
    caseSubjectFromCase,
    formatDate,
    statusTitle,
    resolveCommentStepMeta,
    renderCommentThreadList,
    openCommentStepView,
    renderCaseTable,
    renderAttachments,
    renderTimeline,
  };
})();
