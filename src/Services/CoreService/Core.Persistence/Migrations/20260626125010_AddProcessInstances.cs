using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Process");

            migrationBuilder.CreateTable(
                name: "process_instances",
                schema: "Process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WorkflowDefinitionVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentProcessStep = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastCommandCorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_instances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_LastCommandCorrelationId",
                schema: "Process",
                table: "process_instances",
                column: "LastCommandCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_Module_CaseId",
                schema: "Process",
                table: "process_instances",
                columns: new[] { "Module", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_Module_Status",
                schema: "Process",
                table: "process_instances",
                columns: new[] { "Module", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_WorkflowInstanceId",
                schema: "Process",
                table: "process_instances",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_instances",
                schema: "Process");
        }
    }
}
