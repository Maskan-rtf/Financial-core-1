using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeGuaranteeCancellationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmendmentOriginalGuaranteeReference",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegalOverrideApproved",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SettlementConfirmationRequired",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmendmentOriginalGuaranteeReference",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "LegalOverrideApproved",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "SettlementConfirmationRequired",
                schema: "Guarantee",
                table: "guarantee_cases");
        }
    }
}
