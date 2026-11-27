using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class PendingFixAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                schema: "access",
                table: "ACCESS_Assignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ACCESS_PlatformMembers",
                schema: "access",
                table: "ACCESS_PlatformMembers");

            migrationBuilder.DropIndex(
                name: "IX_ACCESS_PlatformMembers_PlatformId_UserId",
                schema: "access",
                table: "ACCESS_PlatformMembers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACCESS_PlatformMembers",
                schema: "access",
                table: "ACCESS_PlatformMembers",
                columns: new[] { "PlatformId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                schema: "access",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" },
                principalSchema: "access",
                principalTable: "ACCESS_Roles",
                principalColumns: new[] { "Id", "ScopeType", "ScopeId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                schema: "access",
                table: "ACCESS_Assignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ACCESS_PlatformMembers",
                schema: "access",
                table: "ACCESS_PlatformMembers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACCESS_PlatformMembers",
                schema: "access",
                table: "ACCESS_PlatformMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_PlatformMembers_PlatformId_UserId",
                schema: "access",
                table: "ACCESS_PlatformMembers",
                columns: new[] { "PlatformId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                schema: "access",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" },
                principalSchema: "access",
                principalTable: "ACCESS_Roles",
                principalColumns: new[] { "Id", "ScopeType", "ScopeId" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
