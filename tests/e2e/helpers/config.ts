/**
 * Environment-aware base URLs for the E2E suite.
 *
 * Hosts (Development launch profiles):
 *   - Dastarkhwan.Web  -> https://localhost:53584 (http 53585)
 *   - Dastarkhwan.Admin -> https://localhost:53586 (http 53587)
 *
 * Override with WEB_BASE_URL / ADMIN_BASE_URL when the hosts run on other ports.
 * HTTPS is the default because both hosts enforce HTTPS redirection.
 */
export const WEB_BASE_URL = process.env.WEB_BASE_URL || 'https://localhost:53584';
export const ADMIN_BASE_URL = process.env.ADMIN_BASE_URL || 'https://localhost:53586';