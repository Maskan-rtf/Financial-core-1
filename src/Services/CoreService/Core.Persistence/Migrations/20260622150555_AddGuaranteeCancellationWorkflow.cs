using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuaranteeCancellationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmendmentApprovedAmount",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AmendmentApprovedValidityTo",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AmendmentCompletedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AmendmentCreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendmentReason",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmendmentRequestedAmount",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AmendmentRequestedValidityTo",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AmendmentRequiresCreditReview",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentType",
                schema: "Guarantee",
                table: "guarantee_cases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmendmentApprovedAmount",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentApprovedValidityTo",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentCompletedAt",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentCreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentReason",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentRequestedAmount",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentRequestedValidityTo",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentRequiresCreditReview",
                schema: "Guarantee",
                table: "guarantee_cases");

            migrationBuilder.DropColumn(
                name: "AmendmentType",
                schema: "Guarantee",
                table: "guarantee_cases");
        }
    }
}
