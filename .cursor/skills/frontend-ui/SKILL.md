---
name: frontend-ui
description: Frontend UI patterns — loading/error/empty states, ui-components.js helpers, RTL Persian layout. Use with /frontend-ui or when building/changing Frontend UI.
disable-model-invocation: true
---

# /frontend-ui

## Purpose

Ensure every async UI boundary has proper loading, error, and empty states using shared components from `js/ui-components.js`.

## When to Use

- `/frontend-ui` invoked
- New tab, panel, table, or form in `Frontend/`
- Review gate: frontend UX
- Any async data fetch in UI code

## Stack context

| Layer | Choice |
|-------|--------|
| Framework | Vanilla JS (IIFE modules, no bundler) |
| Entry | `index.html` → script tags in fixed order |
| Styling | `styles.css` (custom, RTL Persian) |
| Charts | Chart.js 4.4.1 (dashboard tabs only) |
| State | `localStorage` (sessions, config, active case) |

## Responsibilities

1. **Loading state** — show spinner/skeleton before data arrives; disable submit buttons during requests
2. **Error state** — display user-visible message from `apiRequest` throw or `success: false`; never silent fail
3. **Empty state** — when list/query returns zero items, show helpful empty message (not blank panel)
4. Reuse `js/ui-components.js` helpers: tables, comments, modals, status badges
5. Match existing RTL Persian patterns in `styles.css`
6. No unsafe HTML: avoid `innerHTML` with user-controlled content
7. Tab wiring follows `app.js` pattern (`showTab`, panel references on `state`)

## Checklist

- [ ] Loading indicator on every async fetch
- [ ] Error message visible to user (Persian text, actionable)
- [ ] Empty list shows message, not blank container
- [ ] Reused `ui-components.js` before creating new DOM helpers
- [ ] Submit buttons disabled during in-flight requests
- [ ] No `innerHTML` with unsanitized user data
- [ ] Consistent with existing tab/panel layout

## Success Criteria

User never sees frozen UI, blank panels, or cryptic errors; all async boundaries handled.

## Failure Conditions

**Fail** if: missing loading state · silent error · blank empty list · duplicated DOM helper that exists in ui-components · unsafe HTML injection · English-only error in Persian UI without reason

## Shared helpers

Check `Frontend/js/ui-components.js` for:
- Table rendering
- Comment threads
- Status badges and action buttons
- Modal/dialog patterns

## Reference

- `Frontend/README.md` — project structure and script load order
- `docs/frontend/DEVELOPER_WORKFLOWS.md`
