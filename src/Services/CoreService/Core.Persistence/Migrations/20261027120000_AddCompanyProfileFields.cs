using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CeoName",
                schema: "Identity",
                table: "Company",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CeoPhone",
                schema: "Identity",
                table: "Company",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalType",
                schema: "Identity",
                table: "Company",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                schema: "Identity",
                table: "Company",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativePhone",
                schema: "Identity",
                table: "Company",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnologyDomain",
                schema: "Identity",
                table: "Company",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnologyType",
                schema: "Identity",
                table: "Company",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CeoName", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "CeoPhone", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "LegalType", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "RepresentativeName", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "RepresentativePhone", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "TechnologyDomain", schema: "Identity", table: "Company");
            migrationBuilder.DropColumn(name: "TechnologyType", schema: "Identity", table: "Company");
        }
    }
}
