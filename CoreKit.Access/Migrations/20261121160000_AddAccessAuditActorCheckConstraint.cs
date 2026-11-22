using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAuditActorCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""ACCESS_AuditEntries"" ADD CONSTRAINT ""CK_ACCESS_AuditEntries_Actor"" CHECK (
                    (""ActorType"" = 'User' AND ""ActorUserId"" IS NOT NULL AND CAST(""ActorUserId"" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND ""ActorSource"" IS NULL)
                    OR (""ActorType"" = 'System' AND ""ActorUserId"" IS NULL AND ""ActorSource"" IS NOT NULL AND length(trim(""ActorSource"", chr(9)||chr(10)||chr(13)||' ')) > 0)
                    OR (""ActorType"" = 'Unknown' AND ""ActorUserId"" IS NULL AND ""ActorSource"" IS NULL)
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""ACCESS_AuditEntries"" DROP CONSTRAINT ""CK_ACCESS_AuditEntries_Actor"";");
        }
    }
}
