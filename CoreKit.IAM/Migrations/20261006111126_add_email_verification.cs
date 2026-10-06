using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.IAM.Migrations
{
    /// <inheritdoc />
    public partial class add_email_verification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IAM_EmailVerificationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_EmailVerificationTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_EmailVerificationTokens_IAM_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IAM_EmailVerificationTokens_ExpiresAt",
                table: "IAM_EmailVerificationTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_EmailVerificationTokens_UserId",
                table: "IAM_EmailVerificationTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_IAM_EmailVerificationTokens_TokenHash",
                table: "IAM_EmailVerificationTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IAM_EmailVerificationTokens");
        }
    }
}
