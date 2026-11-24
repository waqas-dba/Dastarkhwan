using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable RLS on tenant-owned tables. Policies filter by the session-level
            // app.tenant_id parameter, which is set by TenantRowLevelSecurityInterceptor
            // before each command. The policy coalesces empty string to a nil UUID so that
            // queries without a tenant context (e.g., migrations, seeders) return zero rows
            // instead of throwing a cast error.
            migrationBuilder.Sql(@"
ALTER TABLE ""TENANT_Tenants"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Tenants_rls"" ON ""TENANT_Tenants""
    USING (""Id"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE ""TENANT_Members"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Members_rls"" ON ""TENANT_Members""
    USING (""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE ""TENANT_Settings"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Settings_rls"" ON ""TENANT_Settings""
    USING (""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE ""TENANT_AuditEntries"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_AuditEntries_rls"" ON ""TENANT_AuditEntries""
    USING (""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE ""TENANT_OutboxMessages"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_OutboxMessages_rls"" ON ""TENANT_OutboxMessages""
    USING (""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
");

            migrationBuilder.Sql(@"
-- Allow the application to set the tenant context. Revoked after each transaction (SET LOCAL).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_settings WHERE name = 'app.tenant_id') THEN
        EXECUTE 'SET app.tenant_id = ''00000000-0000-0000-0000-000000000000''';
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS ""TENANT_Tenants_rls"" ON ""TENANT_Tenants"";
ALTER TABLE ""TENANT_Tenants"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""TENANT_Members_rls"" ON ""TENANT_Members"";
ALTER TABLE ""TENANT_Members"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""TENANT_Settings_rls"" ON ""TENANT_Settings"";
ALTER TABLE ""TENANT_Settings"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""TENANT_AuditEntries_rls"" ON ""TENANT_AuditEntries"";
ALTER TABLE ""TENANT_AuditEntries"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""TENANT_OutboxMessages_rls"" ON ""TENANT_OutboxMessages"";
ALTER TABLE ""TENANT_OutboxMessages"" DISABLE ROW LEVEL SECURITY;
");
        }
    }
}
