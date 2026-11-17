using Core.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    [DbContext(typeof(CoreDbContext))]
    [Migration("20261117120000_AddRevisionCommentAttachments")]
    public class AddRevisionCommentAttachments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentDocumentId",
                schema: "Loan",
                table: "loan_case_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                schema: "Loan",
                table: "loan_case_comments",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentDocumentId",
                schema: "Guarantee",
                table: "guarantee_case_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                schema: "Guarantee",
                table: "guarantee_case_comments",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentDocumentId",
                schema: "Loan",
                table: "loan_case_comments");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                schema: "Loan",
                table: "loan_case_comments");

            migrationBuilder.DropColumn(
                name: "AttachmentDocumentId",
                schema: "Guarantee",
                table: "guarantee_case_comments");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                schema: "Guarantee",
                table: "guarantee_case_comments");
        }
    }
}
