(function () {
  const PRIVILEGED_ROLES = ["Admin", "TechnicalExpert", "TechnicalManager"];

  function canManage(role) {
    return PRIVILEGED_ROLES.includes(String(role || ""));
  }

  function stagesPath(module, suffix) {
    const version = window.TESTPANEL_CONFIG && window.TESTPANEL_CONFIG.casesVersion ? window.TESTPANEL_CONFIG.casesVersion : "1.0";
    return "/api/v" + version + "/casestages/" + module + suffix;
  }

  function pickList(payload) {
    if (!payload || typeof payload !== "object") return [];
    return payload.options || payload.Options || [];
  }

  function pick(obj, camel, pascal) {
    if (!obj) return undefined;
    if (obj[camel] !== undefined) return obj[camel];
    if (obj[pascal] !== undefined) return obj[pascal];
    return undefined;
  }

  async function loadOptions(panel, module, caseId) {
    const res = await panel.apiRequest({
      method: "GET",
      path: stagesPath(module, "/" + encodeURIComponent(caseId) + "/options"),
    });
    return panel.unwrapEnvelope(res.body).payload;
  }

  async function submitRollback(panel, module, caseId, targetStatus, comment) {
    const res = await panel.apiRequest({
      method: "PUT",
      path: stagesPath(module, "/" + encodeURIComponent(caseId)),
      body: {
        targetStatus: Number(targetStatus),
        comment: String(comment || "").trim(),
      },
    });
    return panel.unwrapEnvelope(res.body).payload;
  }

  function renderPanel(host, payload, handlers) {
    if (!host) return;

    host.innerHTML = "";
    host.classList.add("hidden");

    if (!payload) return;

    const options = pickList(payload);
    if (!options.length) return;

    host.classList.remove("hidden");
    host.className = "card portal-stage-rollback";

    const currentTitle = pick(payload, "currentTitle", "CurrentTitle") || "";
    const currentStatus = pick(payload, "currentStatus", "CurrentStatus") || "";

    const title = document.createElement("div");
    title.className = "portal-stage-rollback__title";
    title.textContent = "بازگردانی مرحله (فقط عقب‌تر)";
    host.appendChild(title);

    const hint = document.createElement("p");
    hint.className = "muted portal-stage__hint";
    hint.textContent =
      "مرحله فعلی: " +
      currentTitle +
      " (" +
      currentStatus +
      "). فقط مراحلی که پرونده قبلاً در آن بوده و عقب‌تر از وضعیت فعلی هستند قابل انتخاب‌اند.";
    host.appendChild(hint);

    const row = document.createElement("div");
    row.className = "row portal-stage-rollback__row";

    const stageWrap = document.createElement("div");
    stageWrap.className = "formrow";
    const stageLabel = document.createElement("label");
    stageLabel.textContent = "مرحله هدف";
    stageLabel.setAttribute("for", host.id + "Target");
    const stageSelect = document.createElement("select");
    stageSelect.id = host.id + "Target";
    options.forEach(function (opt) {
      const status = pick(opt, "status", "Status");
      const optTitle = pick(opt, "title", "Title") || status;
      const option = document.createElement("option");
      option.value = String(status);
      option.textContent = optTitle + " (" + status + ")";
      stageSelect.appendChild(option);
    });
    stageWrap.appendChild(stageLabel);
    stageWrap.appendChild(stageSelect);
    row.appendChild(stageWrap);

    const commentWrap = document.createElement("div");
    commentWrap.className = "formrow";
    const commentLabel = document.createElement("label");
    commentLabel.textContent = "دلیل بازگردانی";
    commentLabel.setAttribute("for", host.id + "Comment");
    const commentInput = document.createElement("textarea");
    commentInput.id = host.id + "Comment";
    commentInput.rows = 2;
    commentInput.maxLength = 1000;
    commentInput.placeholder = "دلیل بازگردانی مرحله را بنویسید…";
    commentWrap.appendChild(commentLabel);
    commentWrap.appendChild(commentInput);
    row.appendChild(commentWrap);

    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "btn btn--warn";
    btn.textContent = "بازگردانی مرحله";
    btn.addEventListener("click", function () {
      if (handlers.onSubmit) {
        handlers.onSubmit(stageSelect.value, commentInput.value.trim(), btn);
      }
    });
    row.appendChild(btn);

    host.appendChild(row);
  }

  async function refresh(config) {
    const host = typeof config.host === "string" ? document.getElementById(config.host) : config.host;
    if (!host || !config.panel) return;

    const role = typeof config.getRole === "function" ? config.getRole() : "";
    if (!canManage(role)) {
      host.innerHTML = "";
      host.classList.add("hidden");
      return;
    }

    const caseId = typeof config.getCaseId === "function" ? config.getCaseId() : "";
    if (!caseId) {
      host.innerHTML = "";
      host.classList.add("hidden");
      return;
    }

    try {
      const payload = await loadOptions(config.panel, config.module, caseId);
      renderPanel(host, payload, {
        onSubmit: async function (targetStatus, comment, button) {
          if (!comment) {
            if (typeof config.onError === "function") config.onError("ثبت دلیل بازگردانی الزامی است.");
            return;
          }
          if (!window.confirm("مرحله پرونده به حالت انتخاب‌شده بازگردانده شود؟")) return;

          button.disabled = true;
          try {
            await submitRollback(config.panel, config.module, caseId, targetStatus, comment);
            if (typeof config.onSuccess === "function") {
              await config.onSuccess("مرحله پرونده با موفقیت بازگردانده شد.");
            }
          } catch (error) {
            if (typeof config.onError === "function") {
              config.onError(error && error.message ? error.message : String(error));
            }
          } finally {
            button.disabled = false;
          }
        },
      });
    } catch (error) {
      host.innerHTML = "";
      host.classList.add("hidden");
      if (typeof config.onError === "function") {
        config.onError(error && error.message ? error.message : String(error));
      }
    }
  }

  window.CaseStageRollback = {
    canManage: canManage,
    refresh: refresh,
    stagesPath: stagesPath,
  };
})();
