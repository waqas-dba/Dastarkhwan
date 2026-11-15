using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessMembershipProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCESS_MembershipProjections",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MembershipGeneration = table.Column<long>(type: "bigint", nullable: false),
                    LastAppliedSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_MembershipProjections", x => new { x.TenantId, x.UserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_MembershipProjections_UserId",
                table: "ACCESS_MembershipProjections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ACCESS_MembershipProjections_Tenant_User",
                table: "ACCESS_MembershipProjections",
                columns: new[] { "TenantId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_MembershipProjections");
        }
    }
}
