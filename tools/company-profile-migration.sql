START TRANSACTION;
ALTER TABLE "Identity"."Company" ADD "ActivitySubject" character varying(2048);

ALTER TABLE "Identity"."Company" ADD "CompanyEmail" character varying(256);

ALTER TABLE "Identity"."Company" ADD "CurrentCapitalMillions" numeric(18,2);

ALTER TABLE "Identity"."Company" ADD "InitialCapitalMillions" numeric(18,2);

ALTER TABLE "Identity"."Company" ADD "LogoS3Key" character varying(512);

ALTER TABLE "Identity"."Company" ADD "RegistrationDate" date;

ALTER TABLE "Identity"."Company" ADD "RegistrationPlace" character varying(256);

ALTER TABLE "Identity"."Company" ADD "Website" character varying(512);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261101120000_AddExtendedCompanyProfileFields', '9.0.13');

COMMIT;

