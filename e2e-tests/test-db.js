// Quick database connection test
const { count, close } = require('./tests/helpers/db-client');

async function main() {
  try {
    const users = await count('SELECT COUNT(*) FROM identity."Identity_Users"');
    console.log('Identity users:', users);
    const roles = await count('SELECT COUNT(*) FROM access."ACCESS_Roles"');
    console.log('Access roles:', roles);
    const tenants = await count('SELECT COUNT(*) FROM tenant."TENANT_Tenants"');
    console.log('Tenant tenants:', tenants);
    console.log('DB client works!');
  } catch (err) {
    console.error('DB client error:', err.message);
  } finally {
    await close();
  }
}

main();
