using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Identity.Migrations
{
    /// <inheritdoc />
    public partial class DK_Core_Security_03A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecurityVersion",
                table: "Identity_Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "Identity_RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                table: "Identity_RefreshTokens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                table: "Identity_Users");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "Identity_RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                table: "Identity_RefreshTokens");
        }
    }
}
