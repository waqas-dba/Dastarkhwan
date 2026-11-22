using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemasForSingleDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                table: "ACCESS_AuditEntries");

            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_TenantStates",
                newName: "ACCESS_TenantStates",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_Roles",
                newName: "ACCESS_Roles",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_RolePermissions",
                newName: "ACCESS_RolePermissions",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_QuarantinedTenants",
                newName: "ACCESS_QuarantinedTenants",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_ProcessedEvents",
                newName: "ACCESS_ProcessedEvents",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_PlatformMembers",
                newName: "ACCESS_PlatformMembers",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipWatermarks",
                newName: "ACCESS_MembershipWatermarks",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipStreamCursors",
                newName: "ACCESS_MembershipStreamCursors",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipProjections",
                newName: "ACCESS_MembershipProjections",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_AuditEntries",
                newName: "ACCESS_AuditEntries",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "ACCESS_Assignments",
                newName: "ACCESS_Assignments",
                newSchema: "access");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries",
                sql: "(\"ActorType\" = 'User' AND \"ActorUserId\" IS NOT NULL AND CAST(\"ActorUserId\" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND \"ActorSource\" IS NULL)\r\n    OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NOT NULL AND length(trim(\"ActorSource\", chr(9)||chr(10)||chr(13)||' ')) > 0)\r\n    OR (\"ActorType\" = 'Unknown' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries");

            migrationBuilder.RenameTable(
                name: "ACCESS_TenantStates",
                schema: "access",
                newName: "ACCESS_TenantStates");

            migrationBuilder.RenameTable(
                name: "ACCESS_Roles",
                schema: "access",
                newName: "ACCESS_Roles");

            migrationBuilder.RenameTable(
                name: "ACCESS_RolePermissions",
                schema: "access",
                newName: "ACCESS_RolePermissions");

            migrationBuilder.RenameTable(
                name: "ACCESS_QuarantinedTenants",
                schema: "access",
                newName: "ACCESS_QuarantinedTenants");

            migrationBuilder.RenameTable(
                name: "ACCESS_ProcessedEvents",
                schema: "access",
                newName: "ACCESS_ProcessedEvents");

            migrationBuilder.RenameTable(
                name: "ACCESS_PlatformMembers",
                schema: "access",
                newName: "ACCESS_PlatformMembers");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipWatermarks",
                schema: "access",
                newName: "ACCESS_MembershipWatermarks");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipStreamCursors",
                schema: "access",
                newName: "ACCESS_MembershipStreamCursors");

            migrationBuilder.RenameTable(
                name: "ACCESS_MembershipProjections",
                schema: "access",
                newName: "ACCESS_MembershipProjections");

            migrationBuilder.RenameTable(
                name: "ACCESS_AuditEntries",
                schema: "access",
                newName: "ACCESS_AuditEntries");

            migrationBuilder.RenameTable(
                name: "ACCESS_Assignments",
                schema: "access",
                newName: "ACCESS_Assignments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                table: "ACCESS_AuditEntries",
                sql: "(\"ActorType\" = 'User' AND \"ActorUserId\" IS NOT NULL AND CAST(\"ActorUserId\" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND \"ActorSource\" IS NULL)\r\nOR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NOT NULL AND length(trim(\"ActorSource\", chr(9)||chr(10)||chr(13)||' ')) > 0)\r\nOR (\"ActorType\" = 'Unknown' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NULL)");
        }
    }
}
