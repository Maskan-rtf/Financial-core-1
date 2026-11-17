-- Run on database **FinancialCore** (API uses this, not default `postgres`).
-- 1) Published workflow definitions (must be 3 rows for investment/guarantee/loan)
SELECT "DefinitionId", "Name", "Version", "IsPublished"
FROM "WorkflowDefinitions"
ORDER BY "DefinitionId", "Version";

-- 2) Elsa-related migrations recorded (if empty while tables exist → startup migration errors)
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" ILIKE '%Management%' OR "MigrationId" ILIKE '%Runtime%' OR "MigrationId" ILIKE '%Elsa%'
ORDER BY "MigrationId";
