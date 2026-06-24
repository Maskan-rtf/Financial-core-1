using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGuaranteeAmendmentCeoApprovalStep : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_cases"
            SET "CurrentStatus" = "CurrentStatus" + 1
            WHERE "CurrentStatus" >= 19 AND "CurrentStatus" <= 22;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_case_workflow_history"
            SET "FromStatus" = "FromStatus" + 1
            WHERE "FromStatus" >= 19 AND "FromStatus" <= 22;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_case_workflow_history"
            SET "ToStatus" = "ToStatus" + 1
            WHERE "ToStatus" >= 19 AND "ToStatus" <= 22;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_cases"
            SET "CurrentStatus" = "CurrentStatus" - 1
            WHERE "CurrentStatus" >= 20 AND "CurrentStatus" <= 23;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_case_workflow_history"
            SET "FromStatus" = "FromStatus" - 1
            WHERE "FromStatus" >= 20 AND "FromStatus" <= 23;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "Guarantee"."guarantee_case_workflow_history"
            SET "ToStatus" = "ToStatus" - 1
            WHERE "ToStatus" >= 20 AND "ToStatus" <= 23;
            """);
    }
}
