import { APIRequestContext, APIResponse } from '@playwright/test';

export interface ApiClientOptions {
  baseURL: string;
}

export class ApiClient {
  private request: APIRequestContext;
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private tenantId: string | null = null;

  constructor(request: APIRequestContext, baseURL: string) {
    this.request = request;
    this.options = { baseURL };
  }

  private options: ApiClientOptions;

  setAuth(accessToken: string, refreshToken?: string) {
    this.accessToken = accessToken;
    this.refreshToken = refreshToken ?? null;
  }

  /** Sets the X-Tenant-Id header sent on every subsequent request. */
  setTenantHeader(tenantId: string | null) {
    this.tenantId = tenantId;
  }

  clearAuth() {
    this.accessToken = null;
    this.refreshToken = null;
  }

  private headersSync(extra?: Record<string, string>): Record<string, string> {
    const h: Record<string, string> = {};
    if (this.accessToken) {
      h['Authorization'] = `Bearer ${this.accessToken}`;
    }
    if (this.tenantId) {
      h['X-Tenant-Id'] = this.tenantId;
    }
    return { ...h, ...extra };
  }

  private async headers(extra?: Record<string, string>): Promise<Record<string, string>> {
    return this.headersSync(extra);
  }

  async get(path: string, params?: Record<string, string | number | boolean | undefined | null>): Promise<APIResponse> {
    const url = new URL(this.options.baseURL + path);
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value === undefined || value === null) continue;
        url.searchParams.append(key, String(value));
      }
    }
    return this.request.get(url.toString(), { headers: await this.headers() });
  }

  async post(path: string, body?: any): Promise<APIResponse> {
    return this.writeWithRetry(() =>
      this.request.post(this.options.baseURL + path, { data: body, headers: this.headersSync() }),
    );
  }

  async put(path: string, body?: any): Promise<APIResponse> {
    return this.writeWithRetry(() =>
      this.request.put(this.options.baseURL + path, { data: body, headers: this.headersSync() }),
    );
  }

  async delete(path: string): Promise<APIResponse> {
    return this.writeWithRetry(() =>
      this.request.delete(this.options.baseURL + path, { headers: this.headersSync() }),
    );
  }

  // The Access write policy is a hardcoded 20/min fixed window. A single suite run stays under it,
  // but a boundary 429 is retried with backoff so one run never fails another.
  private async writeWithRetry(send: () => Promise<APIResponse>, retries = 4): Promise<APIResponse> {
    const backoffs = [2000, 5000, 10000, 20000];
    let res = await send();
    for (let i = 0; i < retries && res.status() === 429; i++) {
      await new Promise(r => setTimeout(r, backoffs[i] ?? 20000));
      res = await send();
    }
    return res;
  }

  async login(email: string, password: string, retries = 3): Promise<{ accessToken: string; refreshToken: string; user: any }> {
    let lastError: Error | null = null;
    for (let i = 0; i < retries; i++) {
      try {
        const res = await this.post('/Identity/auth/login', { email, password });
        if (res.ok()) {
          const data = await res.json();
          this.setAuth(data.accessToken, data.refreshToken);
          return data;
        }
        // Rate limit error - wait and retry
        if (res.status() === 429 && i < retries - 1) {
          await new Promise(r => setTimeout(r, 2000 * (i + 1)));
          continue;
        }
        throw new Error(`Login failed: ${res.status()} ${await res.text()}`);
      } catch (e) {
        lastError = e as Error;
        if (i < retries - 1 && /rate limit/i.test(lastError.message)) {
          await new Promise(r => setTimeout(r, 2000 * (i + 1)));
          continue;
        }
        throw lastError!;
      }
    }
    throw new Error(`Login failed after ${retries} retries`);
  }

  async refresh(): Promise<{ accessToken: string; refreshToken: string; user: any }> {
    if (!this.refreshToken) {
      throw new Error('No refresh token available');
    }
    const res = await this.post('/Identity/auth/refresh', { refreshToken: this.refreshToken });
    if (!res.ok()) {
      throw new Error(`Refresh failed: ${res.status()} ${await res.text()}`);
    }
    const data = await res.json();
    this.setAuth(data.accessToken, data.refreshToken);
    return data;
  }

  async logout(): Promise<void> {
    if (this.refreshToken) {
      await this.post('/Identity/auth/logout', { refreshToken: this.refreshToken });
    }
    this.clearAuth();
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<APIResponse> {
    return this.post('/Identity/auth/change-password', { currentPassword, newPassword });
  }

  async forgotPassword(email: string): Promise<APIResponse> {
    return this.post('/Identity/auth/forgot-password', { email });
  }

  async resetPassword(token: string, newPassword: string): Promise<APIResponse> {
    return this.post('/Identity/auth/reset-password', { token, newPassword });
  }

  async sendVerification(): Promise<APIResponse> {
    return this.post('/Identity/auth/send-verification');
  }

  async resendVerification(email: string): Promise<APIResponse> {
    return this.post('/Identity/auth/resend-verification', { email });
  }

  async verifyEmail(token: string): Promise<APIResponse> {
    return this.post('/Identity/auth/verify-email', { token });
  }

  // Development-only inbox (Web host) for token retrieval.
  async getVerificationToken(email: string): Promise<APIResponse> {
    return this.get('/dev/verification-token', { email });
  }

  async getPasswordResetToken(email: string): Promise<APIResponse> {
    return this.get('/dev/password-reset-token', { email });
  }

  // User management
  async getCurrentUser(): Promise<APIResponse> {
    return this.get('/Identity/auth/me');
  }

  async listUsers(search?: string, isActive?: boolean, page?: number, pageSize?: number): Promise<APIResponse> {
    return this.get('/Identity/users', { search, isActive, page, pageSize });
  }

  async getUser(id: string): Promise<APIResponse> {
    return this.get(`/Identity/users/${id}`);
  }

  async createUser(email: string, password: string, mobile: string, displayName: string, isActive = true): Promise<APIResponse> {
    return this.post('/Identity/users', { email, password, mobile, displayName, isActive });
  }

  async updateUser(id: string, body: any): Promise<APIResponse> {
    return this.put(`/Identity/users/${id}`, body);
  }

  async deleteUser(id: string): Promise<APIResponse> {
    return this.delete(`/Identity/users/${id}`);
  }

  // Access
  async getMyAccess(): Promise<APIResponse> {
    return this.get('/access/me');
  }

  async listPermissions(): Promise<APIResponse> {
    return this.get('/access/permissions');
  }

  async listRoles(): Promise<APIResponse> {
    return this.get('/access/roles');
  }

  async getRole(id: string): Promise<APIResponse> {
    return this.get(`/access/roles/${id}`);
  }

  async createRole(name: string, description?: string, permissions?: string[]): Promise<APIResponse> {
    return this.post('/access/roles', { name, description, permissions: permissions || [] });
  }

  async updateRole(id: string, body: any): Promise<APIResponse> {
    return this.put(`/access/roles/${id}`, body);
  }

  async setRolePermissions(id: string, permissions: string[], concurrencyStamp: string): Promise<APIResponse> {
    return this.put(`/access/roles/${id}/permissions`, { permissions, concurrencyStamp });
  }

  async deleteRole(id: string): Promise<APIResponse> {
    return this.delete(`/access/roles/${id}`);
  }

  async getUserRoles(userId: string): Promise<APIResponse> {
    return this.get(`/access/users/${userId}/roles`);
  }

  async assignUserRoles(userId: string, roleIds: string[]): Promise<APIResponse> {
    return this.put(`/access/users/${userId}/roles`, { roleIds });
  }

  async getAccessAudit(page = 1, pageSize = 50): Promise<APIResponse> {
    return this.get('/access/audit', { page, pageSize });
  }

  async addPlatformMember(userId: string): Promise<APIResponse> {
    return this.post('/access/platform/members', { userId });
  }

  async getPlatformMember(userId: string): Promise<APIResponse> {
    return this.get(`/access/platform/members/${userId}`);
  }

  async revokePlatformMember(userId: string): Promise<APIResponse> {
    return this.delete(`/access/platform/members/${userId}`);
  }

  // Tenant
  async listTenants(search?: string, status?: string, page?: number, pageSize?: number): Promise<APIResponse> {
    return this.get('/tenants', { search, status, page, pageSize });
  }

  async getCurrentTenant(): Promise<APIResponse> {
    return this.get('/tenants/current');
  }

  async listMyTenants(): Promise<APIResponse> {
    return this.get('/tenants/me');
  }

  async getTenant(id: string): Promise<APIResponse> {
    return this.get(`/tenants/${id}`);
  }

  async createTenant(name: string, slug?: string, activateImmediately = true, ownerUserId?: string): Promise<APIResponse> {
    return this.post('/tenants', { name, slug, activateImmediately, ownerUserId });
  }

  async updateTenant(id: string, body: any): Promise<APIResponse> {
    return this.put(`/tenants/${id}`, body);
  }

  async activateTenant(id: string): Promise<APIResponse> {
    return this.post(`/tenants/${id}/activate`);
  }

  async suspendTenant(id: string, reason?: string): Promise<APIResponse> {
    return this.post(`/tenants/${id}/suspend`, { reason });
  }

  async archiveTenant(id: string, reason?: string): Promise<APIResponse> {
    return this.post(`/tenants/${id}/archive`, { reason });
  }

  async restoreTenant(id: string): Promise<APIResponse> {
    return this.post(`/tenants/${id}/restore`);
  }

  async deleteTenant(id: string): Promise<APIResponse> {
    return this.delete(`/tenants/${id}`);
  }

  async listMembers(tenantId: string): Promise<APIResponse> {
    return this.get(`/tenants/${tenantId}/members`);
  }

  async addMember(tenantId: string, userId: string, isOwner = false): Promise<APIResponse> {
    return this.post(`/tenants/${tenantId}/members`, { userId, isOwner });
  }

  async removeMember(tenantId: string, userId: string): Promise<APIResponse> {
    return this.delete(`/tenants/${tenantId}/members/${userId}`);
  }

  async setOwner(tenantId: string, userId: string, isOwner: boolean): Promise<APIResponse> {
    return this.put(`/tenants/${tenantId}/members/${userId}/owner`, { isOwner });
  }

  async getAuditLog(tenantId: string, page = 1, pageSize = 50): Promise<APIResponse> {
    return this.get(`/tenants/${tenantId}/audit`, { page, pageSize });
  }

  // Admin
  async addPlatformMemberAdmin(userId: string): Promise<APIResponse> {
    return this.post('/admin/platform/members', { userId });
  }

  async adminHealthReady(): Promise<APIResponse> {
    return this.get('/admin/health/ready');
  }
}

export async function createApiClient(request: APIRequestContext, baseURL: string): Promise<ApiClient> {
  return new ApiClient(request, baseURL);
}