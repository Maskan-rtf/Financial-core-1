# Release Readiness Workflow

Pre-release checklist for production deployment.

## Trigger

- Release branch cut
- Deployment to staging/production planned
- User requests release sign-off

## Steps

1. **Build & test**
   - Full solution `dotnet build` (warnings-as-errors)
   - Full `dotnet test` including integration suite
   - Document results

2. **Migrations**
   - All migrations in correct order
   - Rollback strategy documented for high-risk schema changes
   - Staging apply verified

3. **Quality gates** (sample or full per risk)
   - `/persistence` on critical write workflows
   - `/security` on auth/permission changes
   - `/ef` on data-heavy features
   - `/logging` — critical workflows reconstructable post-deploy

4. **Observability**
   - Correlation ids flow in staging
   - No secret leakage in sample log review
   - Dashboards/alerts updated if new failure modes

5. **Frontend** (if released)
   - Smoke test React/Next/Vue against staging API
   - Error boundaries and empty states verified

6. **Sign-off**
   - Run `workflows/code-review.md` on release delta
   - Verdict **Approved** required

## Required Skills

`/persistence` · `/security` · `/logging` · `/ef` — depth proportional to release risk

## Completion Criteria

- [ ] Build and full test suite green
- [ ] Migrations safe and applied on staging
- [ ] Critical workflows verified (persist, auth, logs)
- [ ] No open Critical/High security findings
- [ ] Release notes include migration steps and breaking changes
- [ ] Code review **Approved** on release branch
