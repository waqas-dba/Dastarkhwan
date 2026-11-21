using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessAuditActorSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                table: "ACCESS_AuditEntries",
                type: "character varying(30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorSource",
                table: "ACCESS_AuditEntries",
                type: "character varying(200)",
                nullable: true);

            // Historical classification: rows that already carry an actor user id become User;
            // rows without one become Unknown. No system actor, operator identity, source, or
            // historical origin is invented. Every existing entry and its original ActorUserId,
            // action, details, scope and timestamp are preserved.
            migrationBuilder.Sql(
                @"UPDATE ""ACCESS_AuditEntries"" SET ""ActorType"" = 'User' WHERE ""ActorUserId"" IS NOT NULL;
UPDATE ""ACCESS_AuditEntries"" SET ""ActorType"" = 'Unknown' WHERE ""ActorUserId"" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "ActorType",
                table: "ACCESS_AuditEntries",
                type: "character varying(30)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorSource",
                table: "ACCESS_AuditEntries");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "ACCESS_AuditEntries");
        }
    }
}
