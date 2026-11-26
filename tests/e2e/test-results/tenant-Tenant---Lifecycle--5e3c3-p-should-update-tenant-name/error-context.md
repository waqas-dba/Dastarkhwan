# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: tenant.spec.ts >> Tenant - Lifecycle and Membership >> should update tenant name
- Location: tenant.spec.ts:51:3

# Error details

```
Error: Login failed: 429 
```

# Test source

```ts
  1   | import { APIRequestContext, APIResponse } from '@playwright/test';
  2   | 
  3   | export interface ApiClientOptions {
  4   |   baseURL: string;
  5   | }
  6   | 
  7   | export class ApiClient {
  8   |   private request: APIRequestContext;
  9   |   private accessToken: string | null = null;
  10  |   private refreshToken: string | null = null;
  11  | 
  12  |   constructor(request: APIRequestContext, baseURL: string) {
  13  |     this.request = request;
  14  |     this.options = { baseURL };
  15  |   }
  16  | 
  17  |   private options: ApiClientOptions;
  18  | 
  19  |   setAuth(accessToken: string, refreshToken?: string) {
  20  |     this.accessToken = accessToken;
  21  |     this.refreshToken = refreshToken ?? null;
  22  |   }
  23  | 
  24  |   clearAuth() {
  25  |     this.accessToken = null;
  26  |     this.refreshToken = null;
  27  |   }
  28  | 
  29  |   private async headers(extra?: Record<string, string>): Promise<Record<string, string>> {
  30  |     const h: Record<string, string> = {};
  31  |     if (this.accessToken) {
  32  |       h['Authorization'] = `Bearer ${this.accessToken}`;
  33  |     }
  34  |     return { ...h, ...extra };
  35  |   }
  36  | 
  37  |   async get(path: string, params?: Record<string, string | number>): Promise<APIResponse> {
  38  |     const url = new URL(this.options.baseURL + path);
  39  |     if (params) {
  40  |       for (const [key, value] of Object.entries(params)) {
  41  |         url.searchParams.append(key, String(value));
  42  |       }
  43  |     }
  44  |     return this.request.get(url.toString(), { headers: await this.headers() });
  45  |   }
  46  | 
  47  |   async post(path: string, body?: any): Promise<APIResponse> {
  48  |     return this.request.post(this.options.baseURL + path, {
  49  |       data: body,
  50  |     });
  51  |   }
  52  | 
  53  |   async put(path: string, body?: any): Promise<APIResponse> {
  54  |     return this.request.put(this.options.baseURL + path, {
  55  |       data: body,
  56  |     });
  57  |   }
  58  | 
  59  |   async delete(path: string): Promise<APIResponse> {
  60  |     return this.request.delete(this.options.baseURL + path, {
  61  |       headers: await this.headers(),
  62  |     });
  63  |   }
  64  | 
  65  |   async login(email: string, password: string, retries = 3): Promise<{ accessToken: string; refreshToken: string; user: any }> {
  66  |     let lastError: Error | null = null;
  67  |     for (let i = 0; i < retries; i++) {
  68  |       try {
  69  |         const res = await this.post('/Identity/auth/login', { email, password });
  70  |         if (res.ok()) {
  71  |           const data = await res.json();
  72  |           this.setAuth(data.accessToken, data.refreshToken);
  73  |           return data;
  74  |         }
  75  |         // Rate limit error - wait and retry
  76  |         if (res.status() === 429 && i < retries - 1) {
  77  |           await new Promise(r => setTimeout(r, 2000 * (i + 1)));
  78  |           continue;
  79  |         }
> 80  |         throw new Error(`Login failed: ${res.status()} ${await res.text()}`);
      |               ^ Error: Login failed: 429 
  81  |       } catch (e) {
  82  |         lastError = e as Error;
  83  |         if (i < retries - 1 && /rate limit/i.test(lastError.message)) {
  84  |           await new Promise(r => setTimeout(r, 2000 * (i + 1)));
  85  |           continue;
  86  |         }
  87  |         throw lastError!;
  88  |       }
  89  |     }
  90  |     throw new Error(`Login failed after ${retries} retries`);
  91  |   }
  92  | 
  93  |   async refresh(): Promise<{ accessToken: string; refreshToken: string; user: any }> {
  94  |     if (!this.refreshToken) {
  95  |       throw new Error('No refresh token available');
  96  |     }
  97  |     const res = await this.post('/Identity/auth/refresh', { refreshToken: this.refreshToken });
  98  |     if (!res.ok()) {
  99  |       throw new Error(`Refresh failed: ${res.status()} ${await res.text()}`);
  100 |     }
  101 |     const data = await res.json();
  102 |     this.setAuth(data.accessToken, data.refreshToken);
  103 |     return data;
  104 |   }
  105 | 
  106 |   async logout(): Promise<void> {
  107 |     if (this.refreshToken) {
  108 |       await this.post('/Identity/auth/logout', { refreshToken: this.refreshToken });
  109 |     }
  110 |     this.clearAuth();
  111 |   }
  112 | 
  113 |   async changePassword(currentPassword: string, newPassword: string): Promise<APIResponse> {
  114 |     return this.post('/Identity/auth/change-password', { currentPassword, newPassword });
  115 |   }
  116 | 
  117 |   async forgotPassword(email: string): Promise<APIResponse> {
  118 |     return this.post('/Identity/auth/forgot-password', { email });
  119 |   }
  120 | 
  121 |   async resetPassword(token: string, newPassword: string): Promise<APIResponse> {
  122 |     return this.post('/Identity/auth/reset-password', { token, newPassword });
  123 |   }
  124 | 
  125 |   // User management
  126 |   async getCurrentUser(): Promise<APIResponse> {
  127 |     return this.get('/Identity/auth/me');
  128 |   }
  129 | 
  130 |   async listUsers(search?: string, isActive?: boolean, page?: number, pageSize?: number): Promise<APIResponse> {
  131 |     return this.get('/Identity/users', { search, isActive, page, pageSize });
  132 |   }
  133 | 
  134 |   async getUser(id: string): Promise<APIResponse> {
  135 |     return this.get(`/Identity/users/${id}`);
  136 |   }
  137 | 
  138 |   async createUser(email: string, password: string, mobile: string, displayName: string, isActive = true): Promise<APIResponse> {
  139 |     return this.post('/Identity/users', { email, password, mobile, displayName, isActive });
  140 |   }
  141 | 
  142 |   async updateUser(id: string, body: any): Promise<APIResponse> {
  143 |     return this.put(`/Identity/users/${id}`, body);
  144 |   }
  145 | 
  146 |   async deleteUser(id: string): Promise<APIResponse> {
  147 |     return this.delete(`/Identity/users/${id}`);
  148 |   }
  149 | 
  150 |   // Access
  151 |   async getMyAccess(): Promise<APIResponse> {
  152 |     return this.get('/access/me');
  153 |   }
  154 | 
  155 |   async listPermissions(): Promise<APIResponse> {
  156 |     return this.get('/access/permissions');
  157 |   }
  158 | 
  159 |   async listRoles(): Promise<APIResponse> {
  160 |     return this.get('/access/roles');
  161 |   }
  162 | 
  163 |   async createRole(name: string, description?: string, permissions?: string[]): Promise<APIResponse> {
  164 |     return this.post('/access/roles', { name, description, permissions: permissions || [] });
  165 |   }
  166 | 
  167 |   async assignUserRoles(userId: string, roleIds: string[]): Promise<APIResponse> {
  168 |     return this.put(`/access/users/${userId}/roles`, { roleIds });
  169 |   }
  170 | 
  171 |   async addPlatformMember(userId: string): Promise<APIResponse> {
  172 |     return this.post('/access/platform/members', { userId });
  173 |   }
  174 | 
  175 |   async revokePlatformMember(userId: string): Promise<APIResponse> {
  176 |     return this.delete(`/access/platform/members/${userId}`);
  177 |   }
  178 | 
  179 |   // Tenant
  180 |   async listTenants(search?: string, status?: string, page?: number, pageSize?: number): Promise<APIResponse> {
```