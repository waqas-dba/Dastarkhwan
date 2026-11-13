using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMembershipGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MembershipGeneration",
                table: "TENANT_Members",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "TENANT_Members",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MembershipGeneration",
                table: "TENANT_Members");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "TENANT_Members");
        }
    }
}
