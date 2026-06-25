using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentWorkflowStatusAtCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatusAtCreation",
                schema: "Loan",
                table: "loan_case_comments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatusAtCreation",
                schema: "Guarantee",
                table: "guarantee_case_comments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatusAtCreation",
                schema: "Investment",
                table: "case_comments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkflowStatusAtCreation",
                schema: "Loan",
                table: "loan_case_comments");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusAtCreation",
                schema: "Guarantee",
                table: "guarantee_case_comments");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusAtCreation",
                schema: "Investment",
                table: "case_comments");
        }
    }
}
