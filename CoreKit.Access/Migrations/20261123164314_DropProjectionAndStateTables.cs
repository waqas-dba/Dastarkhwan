using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Access.Migrations
{
    /// <inheritdoc />
    public partial class DropProjectionAndStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_MembershipProjections");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is intentionally not implemented: projection tables are removed permanently.
            // Recreating them would require the full projection machinery which has been deleted.
            throw new NotSupportedException("Projection tables cannot be recreated; the projection machinery has been removed.");
        }
    }
}
