// Dastarkhwan E2E Test Fixtures
// Synthetic test data generators. No real personal data.

const crypto = require('crypto');

const config = require('./config');

/**
 * Generate a unique test email.
 */
function makeEmail(prefix = 'user') {
  const id = crypto.randomBytes(4).toString('hex');
  return `${prefix}.${id}@example.test`;
}

/**
 * Generate a unique test display name.
 */
function makeDisplayName(prefix = 'Test User') {
  return `${prefix} ${crypto.randomBytes(2).toString('hex')}`;
}

/**
 * Generate a test password that meets policy.
 */
function makePassword() {
  return 'TestPass123!';
}

/**
 * Generate a unique tenant name.
 */
function makeTenantName() {
  return `E2E Tenant ${crypto.randomBytes(4).toString('hex')}`;
}

/**
 * Generate a unique role name.
 */
function makeRoleName(prefix = 'E2E Role') {
  return `${prefix} ${crypto.randomBytes(4).toString('hex')}`;
}

/**
 * Create a standard test user registration payload.
 */
function registerPayload(email, password, displayName) {
  return {
    email: email || makeEmail(),
    password: password || makePassword(),
    displayName: displayName || makeDisplayName(),
  };
}

/**
 * Create a login payload.
 */
function loginPayload(email, password) {
  return { email, password };
}

/**
 * Create a change-password payload.
 */
function changePasswordPayload(currentPassword, newPassword) {
  return {
    currentPassword,
    newPassword,
  };
}

/**
 * Create a tenant creation payload.
 */
function createTenantPayload(name, contactEmail) {
  return {
    name: name || makeTenantName(),
    contactEmail: contactEmail || makeEmail('contact'),
  };
}

/**
 * Create a role creation payload.
 */
function createRolePayload(name, description, permissions) {
  return {
    name: name || makeRoleName(),
    description: description || 'E2E test role',
    permissions: permissions || [],
  };
}

/**
 * Create a member addition payload.
 */
function addMemberPayload(userId, roleIds) {
  return {
    userId,
    roleIds: roleIds || [],
  };
}

module.exports = {
  makeEmail,
  makeDisplayName,
  makePassword,
  makeTenantName,
  makeRoleName,
  registerPayload,
  loginPayload,
  changePasswordPayload,
  createTenantPayload,
  createRolePayload,
  addMemberPayload,
};
