using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemasForSingleDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.RenameTable(
                name: "Identity_Users",
                newName: "Identity_Users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Identity_RefreshTokens",
                newName: "Identity_RefreshTokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Identity_OutboxMessages",
                newName: "Identity_OutboxMessages",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Idenity_EmailVerificationTokens",
                newName: "Idenity_EmailVerificationTokens",
                newSchema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Identity_Users",
                schema: "identity",
                newName: "Identity_Users");

            migrationBuilder.RenameTable(
                name: "Identity_RefreshTokens",
                schema: "identity",
                newName: "Identity_RefreshTokens");

            migrationBuilder.RenameTable(
                name: "Identity_OutboxMessages",
                schema: "identity",
                newName: "Identity_OutboxMessages");

            migrationBuilder.RenameTable(
                name: "Idenity_EmailVerificationTokens",
                schema: "identity",
                newName: "Idenity_EmailVerificationTokens");
        }
    }
}
