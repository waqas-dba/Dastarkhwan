using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Idenity_EmailVerificationTokens_Identity_Users_UserId",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Idenity_EmailVerificationTokens",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens");

            migrationBuilder.RenameTable(
                name: "Idenity_EmailVerificationTokens",
                schema: "identity",
                newName: "Identity_EmailVerificationTokens",
                newSchema: "identity");

            migrationBuilder.RenameIndex(
                name: "IX_Idenity_EmailVerificationTokens_UserId",
                schema: "identity",
                table: "Identity_EmailVerificationTokens",
                newName: "IX_Identity_EmailVerificationTokens_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Idenity_EmailVerificationTokens_ExpiresAt",
                schema: "identity",
                table: "Identity_EmailVerificationTokens",
                newName: "IX_Identity_EmailVerificationTokens_ExpiresAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Identity_EmailVerificationTokens",
                schema: "identity",
                table: "Identity_EmailVerificationTokens",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Identity_EmailVerificationTokens_Identity_Users_UserId",
                schema: "identity",
                table: "Identity_EmailVerificationTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Identity_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Identity_EmailVerificationTokens_Identity_Users_UserId",
                schema: "identity",
                table: "Identity_EmailVerificationTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Identity_EmailVerificationTokens",
                schema: "identity",
                table: "Identity_EmailVerificationTokens");

            migrationBuilder.RenameTable(
                name: "Identity_EmailVerificationTokens",
                schema: "identity",
                newName: "Idenity_EmailVerificationTokens",
                newSchema: "identity");

            migrationBuilder.RenameIndex(
                name: "IX_Identity_EmailVerificationTokens_UserId",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens",
                newName: "IX_Idenity_EmailVerificationTokens_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Identity_EmailVerificationTokens_ExpiresAt",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens",
                newName: "IX_Idenity_EmailVerificationTokens_ExpiresAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Idenity_EmailVerificationTokens",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Idenity_EmailVerificationTokens_Identity_Users_UserId",
                schema: "identity",
                table: "Idenity_EmailVerificationTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Identity_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
