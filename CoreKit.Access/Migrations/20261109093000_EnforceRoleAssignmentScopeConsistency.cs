using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <summary>
    /// Enforce Access audit entry scope consistency by adding:
    ///   * Alternate key on AccessRole: (Id, ScopeType, ScopeId)
    ///   * Composite foreign key on AccessAssignment:
    *       (RoleId, ScopeType, ScopeId) references AccessRole(Id, ScopeType, ScopeId)
    ///   * Supporting index on AccessAssignment: (RoleId, ScopeType, ScopeId)
    ///
    /// Preflight: Before applying, ensure no existing assignment references a role
    /// in a different scope. If any such row exists, this migration will fail
    /// when the foreign key constraint is added. Do not repair data manually;
    /// resolve the inconsistency first, then re-run the migration.
    /// </summary>
    public partial class EnforceRoleAssignmentScopeConsistency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "UX_ACCESS_Roles_Id_ScopeType_ScopeId",
                table: "ACCESS_Roles",
                columns: new[] { "Id", "ScopeType", "ScopeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" },
                principalTable: "ACCESS_Roles",
                principalColumns: new[] { "Id", "ScopeType", "ScopeId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_Assignments_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments",
                columns: new[] { "RoleId", "ScopeType", "ScopeId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ACCESS_Assignments_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId",
                table: "ACCESS_Assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "UX_ACCESS_Roles_Id_ScopeType_ScopeId",
                table: "ACCESS_Roles");
        }
    }
}
