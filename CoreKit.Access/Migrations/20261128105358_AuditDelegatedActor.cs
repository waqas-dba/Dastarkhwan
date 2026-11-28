using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AuditDelegatedActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries",
                sql: "(\"ActorType\" = 'User' AND \"ActorUserId\" IS NOT NULL AND CAST(\"ActorUserId\" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND \"ActorSource\" IS NULL)\r\n    OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NOT NULL AND length(trim(\"ActorSource\", chr(9)||chr(10)||chr(13)||' ')) > 0)\r\n    OR (\"ActorType\" = 'Unknown' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NULL)\r\n    OR (\"ActorType\" = 'Delegated' AND \"ActorUserId\" IS NOT NULL AND \"ActorSource\" IS NOT NULL AND length(trim(\"ActorSource\", chr(9)||chr(10)||chr(13)||' ')) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ACCESS_AuditEntries_Actor",
                schema: "access",
                table: "ACCESS_AuditEntries",
                sql: "(\"ActorType\" = 'User' AND \"ActorUserId\" IS NOT NULL AND CAST(\"ActorUserId\" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND \"ActorSource\" IS NULL)\r\n    OR (\"ActorType\" = 'System' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NOT NULL AND length(trim(\"ActorSource\", chr(9)||chr(10)||chr(13)||' ')) > 0)\r\n    OR (\"ActorType\" = 'Unknown' AND \"ActorUserId\" IS NULL AND \"ActorSource\" IS NULL)");
        }
    }
}
