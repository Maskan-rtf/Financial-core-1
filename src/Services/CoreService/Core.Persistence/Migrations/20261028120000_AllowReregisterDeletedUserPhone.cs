using Core.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    [DbContext(typeof(CoreDbContext))]
    [Migration("20261028120000_AllowReregisterDeletedUserPhone")]
    public class AllowReregisterDeletedUserPhone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_PhoneNumber",
                schema: "Identity",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_User_PhoneNumber",
                schema: "Identity",
                table: "User",
                column: "PhoneNumber",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                schema: "Identity",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                schema: "Identity",
                table: "User",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_PhoneNumber",
                schema: "Identity",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_User_PhoneNumber",
                schema: "Identity",
                table: "User",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                schema: "Identity",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                schema: "Identity",
                table: "User",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");
        }
    }
}
