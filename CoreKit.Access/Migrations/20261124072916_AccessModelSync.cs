using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AccessModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_MembershipProjections",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ACCESS_MembershipStreamCursors",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ACCESS_MembershipWatermarks",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ACCESS_ProcessedEvents",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ACCESS_QuarantinedTenants",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ACCESS_TenantStates",
                schema: "access");

            migrationBuilder.CreateIndex(
                name: "UX_ACCESS_Assignments_Scope_User_Role",
                schema: "access",
                table: "ACCESS_Assignments",
                columns: new[] { "ScopeType", "ScopeId", "UserId", "RoleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ACCESS_Assignments_Scope_User_Role",
                schema: "access",
                table: "ACCESS_Assignments");

            migrationBuilder.CreateTable(
                name: "ACCESS_MembershipProjections",
                schema: "access",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastAppliedSequence = table.Column<long>(type: "bigint", nullable: false),
                    MembershipGeneration = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_MembershipProjections", x => new { x.TenantId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_MembershipStreamCursors",
                schema: "access",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastAppliedSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_MembershipStreamCursors", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_MembershipWatermarks",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_MembershipWatermarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_ProcessedEvents",
                schema: "access",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_QuarantinedTenants",
                schema: "access",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Since = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_QuarantinedTenants", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_TenantStates",
                schema: "access",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_TenantStates", x => x.TenantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_MembershipProjections_UserId",
                schema: "access",
                table: "ACCESS_MembershipProjections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ACCESS_MembershipProjections_Tenant_User",
                schema: "access",
                table: "ACCESS_MembershipProjections",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ACCESS_ProcessedEvents_Tenant_Sequence",
                schema: "access",
                table: "ACCESS_ProcessedEvents",
                columns: new[] { "TenantId", "Sequence" },
                unique: true);
        }
    }
}
