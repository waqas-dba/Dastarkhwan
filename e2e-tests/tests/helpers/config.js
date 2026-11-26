// Dastarkhwan E2E Test Configuration
// All values are non-secret test configuration.

module.exports = {
  // Host URLs (from launchSettings.json development profiles)
  webBaseUrl: 'http://localhost:53585',
  adminBaseUrl: 'http://localhost:53587',

  // API prefixes
  identityPrefix: '/Identity',
  tenantPrefix: '/tenants',
  accessPrefix: '/access',

  // Database identifiers (non-secret)
  // Database: dastarkhwan_runtime_audit
  // Role: dastarkhwan_audit
  // Host: 127.0.0.1:5432

  // Timeouts
  requestTimeout: 30000,
  dbQueryTimeout: 15000,

  // Test data prefix for synthetic records
  testDataPrefix: 'e2e_audit_',

  // Default pagination
  defaultPageSize: 20,
};
