-- Run once on Liara Postgres when tables exist but EF tries to re-apply Initial.
-- Then run: dotnet ef database update (from Financial-core-1)

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260626135133_Initial', '9.0.13'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260626135133_Initial'
);
