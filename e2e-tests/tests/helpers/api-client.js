// Dastarkhwan E2E API Client Helper
// Provides authenticated HTTP requests against Web and Admin hosts.

const config = require('./config');

const { APIRequestContext } = require('@playwright/test');

/**
 * Creates an authenticated API request context.
 * @param {import('@playwright/test').APIRequestContext} request - Playwright APIRequestContext
 * @param {string} baseUrl - Base URL of the host
 */
function createApiClient(request, baseUrl = config.webBaseUrl) {
  let authToken = null;
  let refreshToken = null;

  return {
    /**
     * Set the bearer token for subsequent requests.
     */
    setAuthToken(token) {
      authToken = token;
    },

    /**
     * Set the refresh token for subsequent requests.
     */
    setRefreshToken(token) {
      refreshToken = token;
    },

    /**
     * Clear all authentication tokens.
     */
    clearAuth() {
      authToken = null;
      refreshToken = null;
    },

    /**
     * Get the current auth headers.
     */
    getAuthHeaders() {
      const headers = {};
      if (authToken) {
        headers['Authorization'] = `Bearer ${authToken}`;
      }
      return headers;
    },

    /**
     * Make a GET request.
     */
    async get(path, options = {}) {
      return request.get(`${baseUrl}${path}`, {
        ...options,
        headers: { ...this.getAuthHeaders(), ...options.headers },
        timeout: config.requestTimeout,
      });
    },

    /**
     * Make a POST request.
     */
    async post(path, body, options = {}) {
      return request.post(`${baseUrl}${path}`, {
        ...options,
        headers: { 'Content-Type': 'application/json', ...this.getAuthHeaders(), ...options.headers },
        data: body,
        timeout: config.requestTimeout,
      });
    },

    /**
     * Make a PUT request.
     */
    async put(path, body, options = {}) {
      return request.put(`${baseUrl}${path}`, {
        ...options,
        headers: { 'Content-Type': 'application/json', ...this.getAuthHeaders(), ...options.headers },
        data: body,
        timeout: config.requestTimeout,
      });
    },

    /**
     * Make a DELETE request.
     */
    async delete(path, options = {}) {
      return request.delete(`${baseUrl}${path}`, {
        ...options,
        headers: { ...this.getAuthHeaders(), ...options.headers },
        timeout: config.requestTimeout,
      });
    },

    /**
     * Login and set auth token.
     */
    async login(email, password) {
      const response = await this.post(`${config.identityPrefix}/auth/login`, {
        email,
        password,
      });
      const body = await response.json();
      if (response.ok() && body.accessToken) {
        this.setAuthToken(body.accessToken);
        if (body.refreshToken) this.setRefreshToken(body.refreshToken);
      }
      return { response, body };
    },

    /**
     * Register a new user.
     */
    async register(email, password, displayName) {
      const response = await this.post(`${config.identityPrefix}/auth/register`, {
        email,
        password,
        displayName,
      });
      const body = await response.json().catch(() => ({}));
      return { response, body };
    },

    /**
     * Refresh the access token using the refresh token.
     */
    async refresh() {
      if (!refreshToken) {
        throw new Error('No refresh token available');
      }
      const response = await this.post(`${config.identityPrefix}/auth/refresh`, {
        refreshToken,
      });
      const body = await response.json().catch(() => ({}));
      if (response.ok() && body.accessToken) {
        this.setAuthToken(body.accessToken);
        if (body.refreshToken) this.setRefreshToken(body.refreshToken);
      }
      return { response, body };
    },

    /**
     * Logout (revoke refresh token).
     */
    async logout() {
      const response = await this.post(`${config.identityPrefix}/auth/logout`, {
        refreshToken,
      });
      this.clearAuth();
      return response;
    },

    /**
     * Helper to extract JSON body from response.
     */
    async json(response) {
      return response.json().catch(() => ({}));
    },

    /**
     * Helper to check if response is successful.
     */
    isOk(response) {
      return response.ok();
    },
  };
}

module.exports = { createApiClient };
