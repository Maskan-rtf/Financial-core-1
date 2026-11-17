-- =============================================================================
-- Liara Postgres: one-shot fix (copy ALL → paste in Liara DB Query → Run)
-- Fixes EF history + adds company profile columns (safe to run more than once)
-- =============================================================================

-- 1) EF thinks Initial was never applied; DB already exists → register Initial
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260626135133_Initial', '9.0.13'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626135133_Initial'
);

-- 2) New company profile columns (skip if already added)
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "ActivitySubject" character varying(2048);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "CompanyEmail" character varying(256);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "CurrentCapitalMillions" numeric(18,2);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "InitialCapitalMillions" numeric(18,2);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "LogoS3Key" character varying(512);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "RegistrationDate" date;
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "RegistrationPlace" character varying(256);
ALTER TABLE "Identity"."Company" ADD COLUMN IF NOT EXISTS "Website" character varying(512);

-- 3) Mark new migration as applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20261101120000_AddExtendedCompanyProfileFields', '9.0.13'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101120000_AddExtendedCompanyProfileFields'
);

-- 4) Verify (should show both migration ids)
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
