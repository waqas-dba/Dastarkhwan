using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "Identity_Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedMobile",
                table: "Identity_Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Identity_Users_NormalizedMobile",
                table: "Identity_Users",
                column: "NormalizedMobile",
                unique: true,
                filter: "\"NormalizedMobile\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Identity_Users_NormalizedMobile",
                table: "Identity_Users");

            migrationBuilder.DropColumn(
                name: "NormalizedMobile",
                table: "Identity_Users");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "Identity_Users");
        }
    }
}
