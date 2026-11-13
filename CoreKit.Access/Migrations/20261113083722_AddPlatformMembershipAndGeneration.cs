using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformMembershipAndGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MembershipGeneration",
                table: "ACCESS_Assignments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ACCESS_PlatformMembers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipGeneration = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_PlatformMembers", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_PlatformMembers_PlatformId_UserId",
                table: "ACCESS_PlatformMembers",
                columns: new[] { "PlatformId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_PlatformMembers");

            migrationBuilder.DropColumn(
                name: "MembershipGeneration",
                table: "ACCESS_Assignments");
        }
    }
}
