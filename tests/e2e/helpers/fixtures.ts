/**
 * Deterministic-but-unique test data helpers.
 *
 * Each Playwright worker run gets a short unique suffix so repeated runs against
 * the same development database do not collide on unique constraints (email, slug,
 * role name). Cleanup of created records is the responsibility of the owning spec
 * (see afterAll hooks) — this module only generates identifiers.
 */
import { randomUUID } from 'crypto';

const RUN_SUFFIX = randomUUID().slice(0, 8);

/** Unique per-run token reused across all generated identifiers. */
export const runId = RUN_SUFFIX;

export function uniqueEmail(prefix = 'e2e'): string {
  return `${prefix}-${RUN_SUFFIX}@example.com`;
}

export function uniqueSlug(prefix = 'e2e'): string {
  return `${prefix}-${RUN_SUFFIX}`;
}

export function uniqueName(prefix = 'E2E'): string {
  return `${prefix} ${RUN_SUFFIX}`;
}

/** Default password for generated test users (synthetic, non-secret). */
export const TEST_PASSWORD = 'TestPass123!';
export const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL || 'admin@e2e-audit.local';
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD || 'ResetAdminPass123!';