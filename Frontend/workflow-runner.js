/* global TESTPANEL_CONFIG */
(function () {
  function initWorkflowRunner(panel) {
    const logRoot = document.getElementById("workflowRunLog");
    const statusEl = document.getElementById("workflowRunStatus");
    const btnRun = document.getElementById("btnRunFullWorkflow");
    const btnStop = document.getElementById("btnStopWorkflow");
    if (!logRoot || !statusEl || !btnRun) return;

    let stopRequested = false;

    function log(message, level) {
      const row = document.createElement("div");
      row.className = "workflow-log__item workflow-log__item--" + (level || "info");
      const time = document.createElement("span");
      time.className = "mono muted";
      time.textContent = new Date().toLocaleTimeString();
      const text = document.createElement("span");
      text.textContent = message;
      row.appendChild(time);
      row.appendChild(text);
      logRoot.appendChild(row);
      logRoot.scrollTop = logRoot.scrollHeight;
    }

    function setStatus(text) {
      statusEl.textContent = text;
    }

    function personaByKey(key) {
      return (TESTPANEL_CONFIG.workflowPersonas || []).find((p) => p.key === key);
    }

    function unwrap(body) {
      return panel.unwrapEnvelope(body);
    }

    function pickId(obj) {
      if (!obj || typeof obj !== "object") return "";
      return obj.id || obj.Id || "";
    }

    function todayIsoDate() {
      const today = new Date();
      const yyyy = today.getFullYear();
      const mm = String(today.getMonth() + 1).padStart(2, "0");
      const dd = String(today.getDate()).padStart(2, "0");
      return yyyy + "-" + mm + "-" + dd;
    }

    async function loginPersona(persona) {
      await panel.apiRequest({
        method: "POST",
        path: "/api/v1/identity/users/send-otp",
        useAuth: false,
        body: { phoneNumber: persona.phone },
      });
      const verify = await panel.apiRequest({
        method: "POST",
        path: "/api/v1/identity/users/verify-otp",
        useAuth: false,
        body: { phoneNumber: persona.phone, otpCode: TESTPANEL_CONFIG.devOtp },
      });
      panel.saveSessionFromLogin(persona.phone, persona.label, verify.body);
      return unwrap(verify.body).payload;
    }

    async function usePersona(key) {
      const persona = personaByKey(key);
      if (!persona) throw new Error("نقش ناشناخته: " + key);
      const existing = panel.findSessionByPhone(persona.phone);
      if (existing) {
        panel.setActiveSessionId(existing.id);
        return existing;
      }
      await loginPersona(persona);
      return panel.getActiveSession();
    }

    async function ensureUsers(adminSession) {
      const personas = TESTPANEL_CONFIG.workflowPersonas || [];
      const created = {};
      panel.setActiveSessionId(adminSession.id);
      for (const persona of personas) {
        if (stopRequested) throw new Error("گردش‌کار توسط کاربر متوقف شد.");
        if (persona.key === "admin") {
          created.admin = { userId: adminSession.userId, persona };
          continue;
        }
        try {
          const createRes = await panel.apiRequest({
            method: "POST",
            path: "/api/v1/identity/users",
            useAuth: false,
            body: {
              phoneNumber: persona.phone,
              email: persona.key + "@workflow.test",
              firstName: persona.firstName,
              lastName: persona.lastName,
              nationalCode: null,
            },
          });
          const user = unwrap(createRes.body).payload;
          created[persona.key] = { userId: pickId(user), persona };
          log("Created user " + persona.label, "ok");
        } catch (error) {
          if (String(error.message || "").toLowerCase().includes("conflict")) {
            log("User already exists for " + persona.label + ", continuing.", "warn");
            await loginPersona(persona);
            created[persona.key] = { userId: panel.getActiveSession().userId, persona };
          } else {
            throw error;
          }
        }
      }

      panel.setActiveSessionId(adminSession.id);
      for (const persona of personas) {
        if (stopRequested) throw new Error("گردش‌کار توسط کاربر متوقف شد.");
        if (persona.key === "admin") continue;
        const entry = created[persona.key];
        if (!entry || !entry.userId) continue;
        await panel.apiRequest({
          method: "PUT",
          path: "/api/v1/identity/users/" + encodeURIComponent(entry.userId),
          body: { role: persona.role, isActive: true },
        });
        log("Assigned role " + persona.role + " to " + persona.label, "ok");
      }

      return created;
    }

    async function uploadDocument(caseId, documentType, fileName, mimeType, content) {
      const blob = new Blob([content], { type: mimeType });
      const presignRes = await panel.apiRequest({
        method: "POST",
        path: panel.casesBasePath() + "/" + caseId + "/documents/presign",
        body: {
          documentType,
          fileName,
          mimeType,
          fileSize: blob.size,
        },
      });
      const presign = unwrap(presignRes.body).payload;
      const uploadUrl = presign.url || presign.Url;
      const s3Key = presign.s3Key || presign.S3Key;
      if (!uploadUrl || !s3Key) throw new Error("پاسخ presign فاقد url یا s3Key است.");

      const putRes = await fetch(uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": mimeType },
        body: blob,
      });
      if (!putRes.ok) throw new Error("بارگذاری در storage با وضعیت " + putRes.status + " ناموفق بود.");

     await panel.apiRequest({
       method: "POST",
        path: panel.casesBasePath() + "/" + caseId + "/documents/confirm?s3Key=" + encodeURIComponent(s3Key) + "&originalFileName=" + encodeURIComponent(fileName),
       body: null,
       json: false,
     });

      return s3Key;
    }

    async function runStep(title, fn) {
      if (stopRequested) throw new Error("گردش‌کار توسط کاربر متوقف شد.");
      log(title, "info");
      await fn();
      log(title + " completed", "ok");
    }

    async function runFullWorkflow() {
      stopRequested = false;
      logRoot.innerHTML = "";
      setStatus("Running");
      btnRun.disabled = true;
      if (btnStop) btnStop.disabled = false;

      try {
        const adminPersona = personaByKey("admin");
        if (!adminPersona) throw new Error("نقش مدیر در پیکربندی تعریف نشده است.");

        let adminSession = panel.findSessionByPhone(adminPersona.phone);
        if (!adminSession) {
          await runStep("Seed and login admin", async () => {
            try {
              await panel.apiRequest({
                method: "POST",
                path: "/api/v1/identity/users",
                useAuth: false,
                body: {
                  phoneNumber: adminPersona.phone,
                  email: "admin@workflow.test",
                  firstName: adminPersona.firstName,
                  lastName: adminPersona.lastName,
                  nationalCode: null,
                },
              });
            } catch (error) {
              if (!String(error.message || "").toLowerCase().includes("conflict")) throw error;
            }
            await loginPersona(adminPersona);
            adminSession = panel.getActiveSession();
          });
        } else {
          panel.setActiveSessionId(adminSession.id);
        }

        await runStep("Provision workflow users and roles", async () => {
          await ensureUsers(adminSession);
        });

        let caseId = "";
        await runStep("Applicant creates and fills the case", async () => {
          await usePersona("applicant");
          const createRes = await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath(),
            body: { applicantType: 1 },
          });
          const created = unwrap(createRes.body).payload;
          caseId = pickId(created);
          if (!caseId) throw new Error("پاسخ ایجاد پرونده شامل شناسه نیست.");
          panel.setCurrentCaseId(caseId);

          await panel.apiRequest({
            method: "PUT",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry1",
            body: {
              businessStage: 2,
              requestedAmount: 100000000,
            },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry1/submit",
            body: { comment: "Auto submit data entry 1" },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry1/submit",
            body: { comment: "Auto submit data entry 1 for review" },
          });
          await panel.apiRequest({
            method: "PUT",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry2",
            body: {
              investmentAttractionBasis: "جذب سرمایه برای توسعه محصول (تست خودکار).",
            },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry1/submit",
            body: { comment: "Auto submit data entry 1 for review" },
          });
        });

        await runStep("Investment expert reviews data entry 1", async () => {
          await usePersona("investmentExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry1/approve",
            body: { comment: "Approved by workflow runner" },
          });
        });

        await runStep("Applicant completes data entry 2", async () => {
          await usePersona("applicant");
          await panel.apiRequest({
            method: "PUT",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry2",
            body: {
              investmentAttractionBasis: "جذب سرمایه برای توسعه محصول (تست خودکار).",
            },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry2/submit",
            body: { comment: "Auto submit data entry 2" },
          });
        });

        await runStep("Investment expert reviews data entry 2", async () => {
          await usePersona("investmentExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/data-entry2/approve",
            body: { comment: "Approved by workflow runner" },
          });
        });

        await runStep("Record valuations and manager approvals", async () => {
          await usePersona("investmentExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/valuations",
            body: { type: 1, amount: 2500000000, notes: "Primary valuation" },
          });
          await usePersona("investmentManager");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/valuations/initial/approve",
            body: { comment: "Initial valuation approved" },
          });
          await usePersona("investmentExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/valuations",
            body: { type: 2, amount: 2600000000, notes: "Secondary valuation" },
          });
          await usePersona("investmentManager");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/valuations/secondary/approve",
            body: { comment: "Secondary valuation approved" },
          });
        });

        let preContractKey = "";
        await runStep("Legal uploads preliminary contract via presigned storage", async () => {
          await usePersona("legalExpert");
          preContractKey = await uploadDocument(
            caseId,
            7,
            "preliminary-contract.pdf",
            "application/pdf",
            "%PDF-1.4\n% Workflow preliminary contract\n"
          );
        });

        await runStep("Applicant approves preliminary contract", async () => {
          await usePersona("applicant");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/contracts/preliminary/approve",
            body: { comment: "Applicant approved preliminary contract" },
          });
        });

        let signedContractKey = "";
        await runStep("Legal finalizes and uploads signed contract", async () => {
          await usePersona("legalExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/contracts/finalize-draft",
            body: { comment: "Draft finalized" },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/contracts/confirm-signature",
            body: { comment: "Signature confirmed" },
          });
          signedContractKey = await uploadDocument(
            caseId,
            9,
            "signed-contract.pdf",
            "application/pdf",
            "%PDF-1.4\n% Workflow signed contract\n"
          );
        });

        await runStep("Financial worksheet and payment completion", async () => {
          await usePersona("investmentExpert");
          await panel.apiRequest({
            method: "PUT",
            path: panel.casesBasePath() + "/" + caseId + "/financial-worksheet",
            body: {
              bankName: "Demo Bank",
              iban: "IR000000000000000000000000",
              approvedAmount: 100000000,
              paymentSchedule: "Single tranche",
              notes: "Generated by workflow runner",
            },
          });
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/financial-worksheet/submit",
            body: { comment: "Worksheet submitted" },
          });
          await usePersona("financialExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/financial-worksheet/approve",
            body: { comment: "Worksheet approved" },
          });
        });

        await runStep("CEO approves before payment phase", async () => {
          await usePersona("ceo");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/ceo-approval/approve",
            body: { comment: "CEO approved for payment" },
          });
        });

        await runStep("Financial records payment", async () => {
          await usePersona("financialExpert");
          await panel.apiRequest({
            method: "POST",
            path: panel.casesBasePath() + "/" + caseId + "/payments",
            body: {
              amount: 100000000,
              paymentDate: todayIsoDate(),
              transactionNumber: "TX-" + Date.now(),
              receiptS3Key: null,
              notes: "Workflow payment",
              method: 1,
              status: 2,
            },
          });
        });

        await runStep("Verify final case state", async () => {
          await usePersona("admin");
          const caseRes = await panel.apiRequest({
            method: "GET",
            path: panel.casesBasePath() + "/" + caseId,
          });
          const caseData = unwrap(caseRes.body).payload;
          const status = caseData.currentStatus ?? caseData.CurrentStatus;
          log("Final case status: " + status, status === 16 || status === "Completed" ? "ok" : "warn");
          await panel.apiRequest({
            method: "GET",
            path: panel.casesBasePath() + "/" + caseId + "/history",
          });
          await panel.apiRequest({
            method: "GET",
            path: panel.casesBasePath() + "/" + caseId + "/documents",
          });
        });

        setStatus("Completed");
        log("Full workflow finished for case " + caseId, "ok");
      } catch (error) {
        setStatus("Failed");
        log(String(error && error.message ? error.message : error), "error");
        throw error;
      } finally {
        btnRun.disabled = false;
        if (btnStop) btnStop.disabled = true;
      }
    }

    btnRun.addEventListener("click", () => {
      runFullWorkflow().catch(() => {});
    });

    if (btnStop) {
      btnStop.addEventListener("click", () => {
        stopRequested = true;
        setStatus("Stopping");
        log("Stop requested. The runner will halt after the current step.", "warn");
      });
    }
  }

  window.initWorkflowRunner = initWorkflowRunner;
})();
