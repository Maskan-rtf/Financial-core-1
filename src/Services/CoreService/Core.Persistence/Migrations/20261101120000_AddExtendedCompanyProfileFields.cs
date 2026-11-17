using Core.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    [DbContext(typeof(CoreDbContext))]
    [Migration("20261101120000_AddExtendedCompanyProfileFields")]
    public partial class AddExtendedCompanyProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivitySubject",
                schema: "Identity",
                table: "Company",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyEmail",
                schema: "Identity",
                table: "Company",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentCapitalMillions",
                schema: "Identity",
                table: "Company",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialCapitalMillions",
                schema: "Identity",
                table: "Company",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoS3Key",
                schema: "Identity",
                table: "Company",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RegistrationDate",
                schema: "Identity",
                table: "Company",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationPlace",
                schema: "Identity",
                table: "Company",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "Identity",
                table: "Company",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ActivitySubject", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "CompanyEmail", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "CurrentCapitalMillions", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "InitialCapitalMillions", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "LogoS3Key", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "RegistrationDate", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "RegistrationPlace", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "Website", schema: "Identity", table: "Company");
        }
    }
}
