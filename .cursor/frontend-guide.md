# Frontend Guide — Test Panel

The `Frontend/` folder is a **non-production RTL Persian test panel** for exercising Financial-Core APIs. It is **vanilla JavaScript** (IIFE modules, no bundler, no React/Vue).

## Quick start

```powershell
cd Frontend
python -m http.server 5500
# http://localhost:5500/index.html
```

Configure API base URL in sidebar **تنظیمات** (`config.js` → `TESTPANEL_CONFIG.baseUrl`, default `http://localhost:5081`).

---

## Entry & load order

`index.html` loads scripts in fixed order:

1. `config.js` — `window.TESTPANEL_CONFIG`
2. `js/ui-components.js`, workflow models, portals, hub, kanban, dashboards, admin
3. `app.js` — core shell last among app logic

**Not loaded by default:** `workflow-runner.js` (E2E automation), `js/cases-registry.js`, `js/dashboard-analytics.js`.

---

## Global API (`window.TestPanel`)

Defined in `app.js` after `DOMContentLoaded`:

| Member | Purpose |
|--------|---------|
| `apiRequest({ method, path, body, useAuth })` | Authenticated `fetch` wrapper |
| `unwrapEnvelope(body)` | Parse `{ success, data, message, validationErrors }` |
| `casesBasePath()` | `/api/v{casesVersion}/investmentcases` |
| `guaranteeCasesBasePath()` | `/api/v{casesVersion}/guaranteecases` |
| `loanCasesBasePath()` | `/api/v{casesVersion}/loancases` |
| `kanbanBasePath()` | `/api/v{casesVersion}/kanban` |
| Session helpers | `getActiveSession`, `saveSessionFromLogin`, `setActiveSessionId` |
| Case ID state | `getInvestmentCaseId`, `setGuaranteeCaseId`, `setCaseModule`, … |

**Rule:** Use `panel.apiRequest` — never raw `fetch` for authenticated endpoints. See `skills/frontend-api/SKILL.md`.

---

## API version paths

| Domain | Path | Version |
|--------|------|---------|
| Identity | `/api/v1/identity/...` | Fixed v1 |
| Dashboard | `/api/v1/dashboard/...` | Fixed v1 |
| Analytics | `/api/v1/analytics/...` | Fixed v1 |
| Cases / kanban / fund-credit | `/api/v{casesVersion}/...` | `TESTPANEL_CONFIG.casesVersion` (default `1`) |

---

## Module map

| JS file | Feature |
|---------|---------|
| `workflow-model.js` | Investment statuses, actions, role gates → `WorkflowModel` |
| `guarantee-workflow-model.js` | Guarantee + amendment/cancellation → `GuaranteeWorkflowModel` |
| `loan-workflow-model.js` | Loan workflow → `LoanWorkflowModel` |
| `cases-hub.js` | Case list + module switcher (investment/guarantee/loan) |
| `portal.js` | Investment case detail/workflow UI |
| `guarantee-portal.js` | Guarantee portal (amendments, cancellation) |
| `loan-portal.js` | Loan portal |
| `kanban.js` | Inbox tab (action-required / watching) |
| `home-dashboard.js` | Role home cockpit |
| `admin-dashboard.js` | Admin multi-role dashboard + Chart.js |
| `employee-kpi-dashboard.js` | KPI sub-tab |
| `fund-credit-limits.js` | CEO periodic limits |
| `fund-credit-capacity-ui.js` | Per-case capacity widget |
| `admin-users.js` | User/session admin |
| `admin-companies.js` | Company CRUD |
| `ui-components.js` | Shared tables, comments, DOM helpers → `UIComponents` |
| `dashboard-ui.js` | `DashboardUi` render helpers |

---

## Navigation (tabs)

Sidebar `data-tab` in `index.html`:

| Tab ID | Label | Module |
|--------|-------|--------|
| `tabCases` | پرونده‌ها | `cases-hub.js` + portals |
| `tabInbox` | کارتابل | `kanban.js` |
| `tabAccount` | حساب کاربری | OTP login (`app.js`) |
| `tabDashboard` | صفحه اصلی | dashboards + fund limits |
| `tabAdminDashboard` | داشبورد مدیریت | `admin-dashboard.js` |
| `tabAdminUsers` | کاربران | `admin-users.js` |
| `tabAdminCompanies` | شرکت‌ها | `admin-companies.js` |

Cases tab: nested `data-module` = `investment` | `guarantee` | `loan`; detail subtabs `workflow` | `attachments` | `history`.

---

## Auth & sessions

- OTP on **حساب کاربری**: `POST /api/v1/identity/users/send-otp` → `verify-otp`
- Multiple saved sessions in `localStorage`; **Use** swaps active Bearer token
- Tokens are **JWE** — decode roles from login/profile response only
- `testpanel:session-changed` event → dashboards/kanban/portals refresh

### localStorage keys

| Key | Purpose |
|-----|---------|
| `workflow_test_panel.config.v2` | Base URL, cases version, dev OTP |
| `workflow_test_panel.sessions.v1` | Saved sessions |
| `workflow_test_panel.active_session_id.v1` | Active session |
| `workflow_test_panel.state.v1` | Active case IDs + module |

---

## Custom events

| Event | When |
|-------|------|
| `testpanel:session-changed` | Login / session switch |
| `testpanel:case-changed` | Case id or module change |
| `testpanel:open-comment-step` | Comment deep-link from UIComponents |

---

## Adding a frontend workflow step

1. **Backend first** — endpoint + DTO stable in `docs/frontend/*_API_GUIDE.md`
2. Update `*-workflow-model.js`:
   - Add status label (Persian)
   - Add allowed action for role/status
   - Map action → API method/path
3. Update `*-portal.js`:
   - Render form/button for new step
   - Call `panel.apiRequest`, handle loading/error/empty
   - Refresh case detail after success
4. Update `kanban.js` rules only if inbox behavior changes (usually backend-driven)
5. Test with correct persona session (role switching)

---

## Styling

- Single `styles.css` — custom CSS, no component library
- RTL layout in `index.html` (`dir="rtl"`)
- Chart.js 4.4.1 CDN for admin dashboard only

---

## Docs cross-reference

| Doc | Content |
|-----|---------|
| `Frontend/README.md` | Human-readable overview |
| `docs/frontend/ARCHITECTURE.md` | Architecture |
| `docs/frontend/API_INTEGRATION.md` | Integration patterns |
| `docs/frontend/DEVELOPER_WORKFLOWS.md` | Dev workflows |
| `docs/frontend/INVESTMENT_CASE_API_GUIDE.md` | Investment endpoints |
| `docs/frontend/GUARANTEE_CASE_API_GUIDE.md` | Guarantee endpoints |
| `docs/frontend/LOAN_CASE_API_GUIDE.md` | Loan endpoints |

**Note:** `GUARANTEE_RENEWAL_API_GUIDE.md` is legacy — renewals merged into amendments.

---

## Agent constraints (frontend)

- Do not introduce a bundler/framework without explicit request.
- Keep IIFE `init*(panel)` pattern — modules receive `TestPanel` facade.
- Persian UI strings inline in JS (match existing style).
- Do not break envelope unwrapping contract.
- Loading/error states required on new async UI (see `skills/frontend-ui/SKILL.md`).
