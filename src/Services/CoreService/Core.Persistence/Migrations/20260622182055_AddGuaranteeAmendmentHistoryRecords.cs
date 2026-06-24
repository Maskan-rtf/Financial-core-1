using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuaranteeAmendmentHistoryRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guarantee_amendment_history_records",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmendmentType = table.Column<int>(type: "integer", nullable: false),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    NewValues = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    ApprovalUser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_amendment_history_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_amendment_history_records_guarantee_cases_Guarant~",
                        column: x => x.GuaranteeCaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_amendment_history_records_GuaranteeCaseId_Created~",
                schema: "Guarantee",
                table: "guarantee_amendment_history_records",
                columns: new[] { "GuaranteeCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_amendment_history_records_GuaranteeCaseId_Status",
                schema: "Guarantee",
                table: "guarantee_amendment_history_records",
                columns: new[] { "GuaranteeCaseId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guarantee_amendment_history_records",
                schema: "Guarantee");
        }
    }
}
