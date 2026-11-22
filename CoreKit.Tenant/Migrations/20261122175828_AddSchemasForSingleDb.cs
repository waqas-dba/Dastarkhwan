using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemasForSingleDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.RenameTable(
                name: "TENANT_Tenants",
                newName: "TENANT_Tenants",
                newSchema: "tenant");

            migrationBuilder.RenameTable(
                name: "TENANT_Settings",
                newName: "TENANT_Settings",
                newSchema: "tenant");

            migrationBuilder.RenameTable(
                name: "TENANT_OutboxMessages",
                newName: "TENANT_OutboxMessages",
                newSchema: "tenant");

            migrationBuilder.RenameTable(
                name: "TENANT_Members",
                newName: "TENANT_Members",
                newSchema: "tenant");

            migrationBuilder.RenameTable(
                name: "TENANT_AuditEntries",
                newName: "TENANT_AuditEntries",
                newSchema: "tenant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "TENANT_Tenants",
                schema: "tenant",
                newName: "TENANT_Tenants");

            migrationBuilder.RenameTable(
                name: "TENANT_Settings",
                schema: "tenant",
                newName: "TENANT_Settings");

            migrationBuilder.RenameTable(
                name: "TENANT_OutboxMessages",
                schema: "tenant",
                newName: "TENANT_OutboxMessages");

            migrationBuilder.RenameTable(
                name: "TENANT_Members",
                schema: "tenant",
                newName: "TENANT_Members");

            migrationBuilder.RenameTable(
                name: "TENANT_AuditEntries",
                schema: "tenant",
                newName: "TENANT_AuditEntries");
        }
    }
}
