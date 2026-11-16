using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessMembershipStreamAndFreshness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCESS_MembershipStreamCursors",
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
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_QuarantinedTenants",
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
                name: "UX_ACCESS_ProcessedEvents_Tenant_Sequence",
                table: "ACCESS_ProcessedEvents",
                columns: new[] { "TenantId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_MembershipStreamCursors");

            migrationBuilder.DropTable(
                name: "ACCESS_MembershipWatermarks");

            migrationBuilder.DropTable(
                name: "ACCESS_ProcessedEvents");

            migrationBuilder.DropTable(
                name: "ACCESS_QuarantinedTenants");

            migrationBuilder.DropTable(
                name: "ACCESS_TenantStates");
        }
    }
}
