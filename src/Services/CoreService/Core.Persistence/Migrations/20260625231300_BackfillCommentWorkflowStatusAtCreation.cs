using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations;

/// <inheritdoc />
public partial class BackfillCommentWorkflowStatusAtCreation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        BackfillModule(
            migrationBuilder,
            commentSchema: "Investment",
            commentTable: "case_comments",
            historyTable: "case_workflow_history");

        BackfillModule(
            migrationBuilder,
            commentSchema: "Loan",
            commentTable: "loan_case_comments",
            historyTable: "loan_case_workflow_history");

        BackfillModule(
            migrationBuilder,
            commentSchema: "Guarantee",
            commentTable: "guarantee_case_comments",
            historyTable: "guarantee_case_workflow_history");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""UPDATE "Investment"."case_comments" SET "WorkflowStatusAtCreation" = NULL;""");
        migrationBuilder.Sql("""UPDATE "Loan"."loan_case_comments" SET "WorkflowStatusAtCreation" = NULL;""");
        migrationBuilder.Sql("""UPDATE "Guarantee"."guarantee_case_comments" SET "WorkflowStatusAtCreation" = NULL;""");
    }

    private static void BackfillModule(
        MigrationBuilder migrationBuilder,
        string commentSchema,
        string commentTable,
        string historyTable)
    {
        migrationBuilder.Sql(
            $"""
            UPDATE "{commentSchema}"."{commentTable}" AS c
            SET "WorkflowStatusAtCreation" = resolved.status
            FROM (
                SELECT
                    c2."Id",
                    CASE
                        WHEN c2."IsRevisionRequest" THEN COALESCE(
                            (
                                SELECT h."FromStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                  AND h."Comment" IS NOT NULL
                                  AND POSITION(c2."Message" IN h."Comment") > 0
                                  AND ABS(EXTRACT(EPOCH FROM (h."CreatedAt" - c2."CreatedAt"))) < 10
                                ORDER BY h."CreatedAt"
                                LIMIT 1
                            ),
                            (
                                SELECT h."FromStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                  AND ABS(EXTRACT(EPOCH FROM (h."CreatedAt" - c2."CreatedAt"))) < 2
                                ORDER BY ABS(EXTRACT(EPOCH FROM (h."CreatedAt" - c2."CreatedAt")))
                                LIMIT 1
                            ),
                            (
                                SELECT h."ToStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                  AND h."CreatedAt" <= c2."CreatedAt"
                                ORDER BY h."CreatedAt" DESC
                                LIMIT 1
                            ),
                            (
                                SELECT h."FromStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                ORDER BY h."CreatedAt" ASC
                                LIMIT 1
                            )
                        )
                        ELSE COALESCE(
                            (
                                SELECT h."ToStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                  AND h."CreatedAt" <= c2."CreatedAt"
                                ORDER BY h."CreatedAt" DESC
                                LIMIT 1
                            ),
                            (
                                SELECT h."FromStatus"
                                FROM "{commentSchema}"."{historyTable}" h
                                WHERE h."CaseId" = c2."CaseId"
                                ORDER BY h."CreatedAt" ASC
                                LIMIT 1
                            )
                        )
                    END AS status
                FROM "{commentSchema}"."{commentTable}" c2
                WHERE c2."WorkflowStatusAtCreation" IS NULL
            ) AS resolved
            WHERE c."Id" = resolved."Id"
              AND resolved.status IS NOT NULL;
            """);
    }
}
