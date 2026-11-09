using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <summary>
    /// A role may only be assigned in its own scope, and the database now enforces it: an assignment must point
    /// at a role with the same (Id, ScopeType, ScopeId).
    ///
    /// Run this read-only check first. Zero rows means the data is consistent and the migration will apply:
    /// <code>
    /// SELECT a."RoleId", a."UserId",
    ///        a."ScopeType" AS "AssignmentScopeType", a."ScopeId" AS "AssignmentScopeId",
    ///        r."ScopeType" AS "RoleScopeType", r."ScopeId" AS "RoleScopeId"
    /// FROM "ACCESS_Assignments" a
    /// JOIN "ACCESS_Roles" r ON r."Id" = a."RoleId"
    /// WHERE a."ScopeType" &lt;&gt; r."ScopeType" OR a."ScopeId" &lt;&gt; r."ScopeId";
    /// </code>
    /// This migration never repairs or deletes data. If such rows exist, adding the composite foreign key fails
    /// and EF rolls the whole migration back; resolve those rows deliberately, then run it again.
    /// </summary>
    public partial class EnforceAssignmentRoleScopeConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId",
                table: "ACCESS_Assignments");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ACCESS_Roles_Id_ScopeType_ScopeId",
                table: "ACCESS_Roles",
                columns: new[] { "Id", "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_Assignments_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" },
                principalTable: "ACCESS_Roles",
                principalColumns: new[] { "Id", "ScopeType", "ScopeId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments");

            migrationBuilder.DropIndex(
                name: "IX_ACCESS_Assignments_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ACCESS_Roles_Id_ScopeType_ScopeId",
                table: "ACCESS_Roles");

            migrationBuilder.AddForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId",
                table: "ACCESS_Assignments",
                column: "RoleId",
                principalTable: "ACCESS_Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
