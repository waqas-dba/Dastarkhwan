const { count, scalar } = require('./tests/helpers/db-client');

(async () => {
  try {
    console.log('Identity users:', await count('SELECT COUNT(*) FROM identity."Identity_Users"'));
    console.log('Access roles:', await count('SELECT COUNT(*) FROM access."ACCESS_Roles"'));
    console.log('Tenant tenants:', await count('SELECT COUNT(*) FROM tenant."TENANT_Tenants"'));
    console.log('RLS Identity tables:', await scalar('SELECT COUNT(*) FROM pg_tables WHERE schemaname = "identity" AND rowsecurity = true'));
    console.log('RLS Tenant tables:', await scalar('SELECT COUNT(*) FROM pg_tables WHERE schemaname = "tenant" AND rowsecurity = true'));
    console.log('Policies exist:', await scalar('SELECT COUNT(*) FROM pg_policy'));
  } catch (err) {
    console.error('DB client error:', err.message);
  } finally {
    await new (require('./tests/helpers/db-client').close)();
  }
})();