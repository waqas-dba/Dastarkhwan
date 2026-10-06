using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.IAM.Migrations
{
    /// <inheritdoc />
    public partial class init_iam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IAM_Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IAM_RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_IAM_RolePermissions_IAM_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "IAM_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IAM_RolePermissions_IAM_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "IAM_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IAM_RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IAM_RefreshTokens_IAM_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IAM_UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IAM_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_IAM_UserRoles_IAM_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "IAM_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IAM_UserRoles_IAM_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "IAM_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_IAM_Permissions_Name",
                table: "IAM_Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_RefreshTokens_ExpiresAt",
                table: "IAM_RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IAM_RefreshTokens_UserId",
                table: "IAM_RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_IAM_RefreshTokens_TokenHash",
                table: "IAM_RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_RolePermissions_PermissionId",
                table: "IAM_RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_IAM_Roles_NormalizedName",
                table: "IAM_Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IAM_UserRoles_RoleId",
                table: "IAM_UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_IAM_Users_NormalizedEmail",
                table: "IAM_Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IAM_RefreshTokens");

            migrationBuilder.DropTable(
                name: "IAM_RolePermissions");

            migrationBuilder.DropTable(
                name: "IAM_UserRoles");

            migrationBuilder.DropTable(
                name: "IAM_Permissions");

            migrationBuilder.DropTable(
                name: "IAM_Roles");

            migrationBuilder.DropTable(
                name: "IAM_Users");
        }
    }
}
