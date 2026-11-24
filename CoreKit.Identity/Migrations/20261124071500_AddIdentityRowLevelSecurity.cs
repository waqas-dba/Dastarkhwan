using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreKit.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable RLS on Identity user-owned tables. Policies filter by the session-level
            // app.user_id parameter, which is set by IdentityRowLevelSecurityInterceptor
            // before each command. The policy coalesces empty string to a nil UUID so that
            // queries without a user context return zero rows instead of throwing a cast error.
            migrationBuilder.Sql(@"
-- Reference data tables (no RLS): Identity_Permissions, Identity_Roles, Identity_RolePermissions
-- User-owned tables with RLS:

ALTER TABLE ""identity"".""Identity_Users"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""Identity_Users_rls"" ON ""identity"".""Identity_Users""
    USING (""Id"" = NULLIF(current_setting('app.user_id', true), '')::uuid);

ALTER TABLE ""identity"".""Identity_RefreshTokens"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""Identity_RefreshTokens_rls"" ON ""identity"".""Identity_RefreshTokens""
    USING (""UserId"" = NULLIF(current_setting('app.user_id', true), '')::uuid);

ALTER TABLE ""identity"".""Idenity_EmailVerificationTokens"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""Idenity_EmailVerificationTokens_rls"" ON ""identity"".""Idenity_EmailVerificationTokens""
    USING (""UserId"" = NULLIF(current_setting('app.user_id', true), '')::uuid);

ALTER TABLE ""identity"".""Identity_UserRoles"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""Identity_UserRoles_rls"" ON ""identity"".""Identity_UserRoles""
    USING (""UserId"" = NULLIF(current_setting('app.user_id', true), '')::uuid);

ALTER TABLE ""identity"".""Identity_OutboxMessages"" ENABLE ROW LEVEL SECURITY;
CREATE POLICY ""Identity_OutboxMessages_rls"" ON ""identity"".""Identity_OutboxMessages""
    USING (""SubjectUserId"" = NULLIF(current_setting('app.user_id', true), '')::uuid OR ""SubjectUserId"" IS NULL);

-- Allow the application to set the user context. Revoked after each transaction (SET LOCAL).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_settings WHERE name = 'app.user_id') THEN
        EXECUTE 'SET app.user_id = ''00000000-0000-0000-0000-000000000000''';
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS ""Identity_Users_rls"" ON ""identity"".""Identity_Users"";
ALTER TABLE ""identity"".""Identity_Users"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""Identity_RefreshTokens_rls"" ON ""identity"".""Identity_RefreshTokens"";
ALTER TABLE ""identity"".""Identity_RefreshTokens"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""Idenity_EmailVerificationTokens_rls"" ON ""identity"".""Idenity_EmailVerificationTokens"";
ALTER TABLE ""identity"".""Idenity_EmailVerificationTokens"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""Identity_UserRoles_rls"" ON ""identity"".""Identity_UserRoles"";
ALTER TABLE ""identity"".""Identity_UserRoles"" DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS ""Identity_OutboxMessages_rls"" ON ""identity"".""Identity_OutboxMessages"";
ALTER TABLE ""identity"".""Identity_OutboxMessages"" DISABLE ROW LEVEL SECURITY;
");
        }
    }
}