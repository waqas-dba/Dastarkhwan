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
            // before each command. Table owner bypasses RLS unless FORCE is used; we
            // force it so migrations and seeders also respect tenant isolation.
            migrationBuilder.Sql(@"
ALTER TABLE ""TENANT_Tenants"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""TENANT_Tenants"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Tenants_rls"" ON ""TENANT_Tenants""
    USING (""Id"" = current_setting('app.tenant_id', true)::uuid);

ALTER TABLE ""TENANT_Members"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""TENANT_Members"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Members_rls"" ON ""TENANT_Members""
    USING (""TenantId"" = current_setting('app.tenant_id', true)::uuid);

ALTER TABLE ""TENANT_Settings"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""TENANT_Settings"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_Settings_rls"" ON ""TENANT_Settings""
    USING (""TenantId"" = current_setting('app.tenant_id', true)::uuid);

ALTER TABLE ""TENANT_AuditEntries"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""TENANT_AuditEntries"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_AuditEntries_rls"" ON ""TENANT_AuditEntries""
    USING (""TenantId"" = current_setting('app.tenant_id', true)::uuid);

ALTER TABLE ""TENANT_OutboxMessages"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""TENANT_OutboxMessages"" FORCE ROW LEVEL SECURITY;
CREATE POLICY ""TENANT_OutboxMessages_rls"" ON ""TENANT_OutboxMessages""
    USING (""TenantId"" = current_setting('app.tenant_id', true)::uuid);
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
