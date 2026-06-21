using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "Loan",
                table: "loan_cases",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "Investment",
                table: "investment_cases",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                schema: "Loan",
                table: "loan_cases");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "Investment",
                table: "investment_cases");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "Guarantee",
                table: "guarantee_cases");
        }
    }
}
