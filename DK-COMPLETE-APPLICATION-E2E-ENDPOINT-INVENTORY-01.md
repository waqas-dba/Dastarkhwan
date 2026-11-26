# DK-COMPLETE-APPLICATION-E2E — Endpoint Inventory (DK-COMPLETE-APPLICATION-E2E-ENDPOINT-INVENTORY-01)

**Baseline:** `da45a146ab491091739f8cc9c0087561a8a046c1`. Read-only discovery from source (not docs).
**Legend:** Auth = authentication requirement; Perm = permission/scope; Tenant = `.RequireTenant()`; RL = rate-limited.

## A. Identity module — prefix `/identity` (MapIdentity)
Source: `CoreKit.Identity/Endpoints/IdentityEndpointExtensions.cs`. No `.RequireTenant()` anywhere. No DataAnnotations on DTOs (validated in services).

| # | Method | Route | Auth | Perm | Tenant | Request | Success | Rejections |
|---|---|---|---|---|---|---|---|---|
| 1 | GET | `/identity/auth/me` | RequireAuthorization | identity.* (service) | no | — | 200 UserDto | 401 |
| 2 | POST | `/identity/auth/login` | AllowAnonymous, RL | — | no | LoginRequest | 200 LoginResponse | 400/401/429 |
| 3 | POST | `/identity/auth/refresh` | AllowAnonymous, RL | — | no | RefreshTokenRequest | 200 LoginResponse | 400/401/429 |
| 4 | POST | `/identity/auth/logout` | AllowAnonymous, RL | — | no | RefreshTokenRequest | 200 | 400/429 |
| 5 | POST | `/identity/auth/change-password` | RequireAuthorization, RL | self | no | ChangePasswordRequest | 200 | 400/401/429 |
| 6 | POST | `/identity/auth/verify-email` | AllowAnonymous, RL | — | no | VerifyEmailRequest | 200 | 400/429 |
| 7 | POST | `/identity/auth/send-verification` | RequireAuthorization, RL | self | no | — | 200 | 401/429 |
| 8 | POST | `/identity/auth/resend-verification` | AllowAnonymous, RL | — | no | ResendVerificationRequest | 200 | 400/429 |
| 9 | POST | `/identity/auth/forgot-password` | AllowAnonymous, RL | — | no | ForgotPasswordRequest | 200 (always) | 429 |
| 10 | POST | `/identity/auth/reset-password` | AllowAnonymous, RL | — | no | ResetPasswordRequest | 200 | 400/429 |
| 11 | GET | `/identity/users` | RequireAuthorization | identity.users.read | no | UserListQuery(qs) | 200 PagedResult<UserDto> | 401/403 |
| 12 | GET | `/identity/users/{id:guid}` | RequireAuthorization | identity.users.read | no | route id | 200 UserDto | 401/403/404 |
| 13 | POST | `/identity/users` | RequireAuthorization | identity.users.create | no | CreateUserRequest | 201 UserDto | 400/401/403/409 |
| 14 | PUT | `/identity/users/{id:guid}` | RequireAuthorization | identity.users.update | no | UpdateUserRequest | 200 UserDto | 400/401/403/404/409 |
| 15 | DELETE | `/identity/users/{id:guid}` | RequireAuthorization | identity.users.delete | no | route id | 200 | 401/403/404/409(owner-protected) |

## B. Access module — prefix `/access` (MapAccess)
Source: `CoreKit.Access/Endpoints/AccessEndpointExtensions.cs`. Group-level `.RequireAuthorization()`. Scope context required (400 `access.scope_required` if unresolved). Write routes RL (`access.write`).

| # | Method | Route | Auth | Perm/scope | Tenant | Request | Success | Rejections |
|---|---|---|---|---|---|---|---|---|
| 16 | GET | `/access/me` | RequireAuthorization | scope ctx | scope | — | 200 summary | 401/400 |
| 17 | GET | `/access/permissions` | RequireAuthorization | scope ctx | scope | — | 200 list | 401/400 |
| 18 | GET | `/access/roles` | RequireAuthorization | scope ctx | scope | — | 200 list | 401/400 |
| 19 | GET | `/access/roles/{id:guid}` | RequireAuthorization | scope ctx | scope | route id | 200 role | 401/400/404 |
| 20 | POST | `/access/roles` | RequireAuthorization, RL | roles.manage | scope | CreateAccessRoleRequest | 201 role | 400/401/403/409/429 |
| 21 | PUT | `/access/roles/{id:guid}` | RequireAuthorization, RL | roles.manage | scope | UpdateAccessRoleRequest | 200 role | 400/401/403/404/409/429 |
| 22 | PUT | `/access/roles/{id:guid}/permissions` | RequireAuthorization, RL | roles.manage | scope | SetAccessRolePermissionsRequest | 200 role | 400/401/403/404/409/429 |
| 23 | DELETE | `/access/roles/{id:guid}` | RequireAuthorization, RL | roles.manage | scope | route id | 200 | 400/401/403/404/429 |
| 24 | GET | `/access/users/{userId:guid}/roles` | RequireAuthorization | scope ctx | scope | route userId | 200 list | 401/400/404 |
| 25 | PUT | `/access/users/{userId:guid}/roles` | RequireAuthorization, RL | assignments.manage | scope | SetAccessUserRolesRequest | 200 | 400/401/403/404/429 |
| 26 | GET | `/access/audit` | RequireAuthorization | scope ctx | scope | page,pageSize(qs) | 200 page | 401/400 |
| 27 | POST | `/access/platform/members` | RequireAuthorization, RL | platform.members.manage (fixed platform scope) | platform | AddPlatformMemberRequest | 201 | 400/401/403/409/429 |
| 28 | GET | `/access/platform/members/{userId:guid}` | RequireAuthorization | fixed platform scope | platform | route userId | 200/404 `{userId,isMember}` | 401/400 |
| 29 | DELETE | `/access/platform/members/{userId:guid}` | RequireAuthorization, RL | platform.members.manage | platform | route userId | 200 | 400/401/403/404/429 |

## C. Tenant module — prefix `/tenants` (MapTenants, in Dastarkhwan.Integration)
Source: `Dastarkhwan.Integration/Endpoints/TenantEndpointExtensions.cs`. Success: GET/PUT→200, non-generic→204, POST create→201. Write routes RL (`tenant.write` 20/min). Only `/tenants/current` uses `.RequireTenant()`.

| # | Method | Route | Auth | Perm | Tenant | Request | Success | Rejections |
|---|---|---|---|---|---|---|---|---|
| 30 | GET | `/tenants/` | RequireAuthorization | tenants.platform (platform) | no | TenantListQuery(qs) | 200 paged | 401/403 |
| 31 | GET | `/tenants/current` | RequireAuthorization | — | **yes** | — | 200 TenantDto | 400 tenant.required/401 |
| 32 | GET | `/tenants/me` | RequireAuthorization | — | no | — | 200 list TenantMembershipDto | 401 |
| 33 | PUT | `/tenants/me/default/{tenantId:guid}` | RequireAuthorization, RL | — | no | route tenantId | 204 | 401/404/429 |
| 34 | GET | `/tenants/{id:guid}` | RequireAuthorization | tenants.read | no | route id | 200 TenantDto | 401/403/404 |
| 35 | POST | `/tenants/` | RequireAuthorization, RL | tenants.create | no | CreateTenantRequest | 201 TenantDto | 400/401/403/409/429 |
| 36 | PUT | `/tenants/{id:guid}` | RequireAuthorization, RL | tenants.update | no | UpdateTenantRequest | 200 TenantDto | 400/401/403/404/409/429 |
| 37 | POST | `/tenants/{id:guid}/activate` | RequireAuthorization, RL | tenants.manage_status | no | — | 200 TenantDto | 403/404/409/429 |
| 38 | POST | `/tenants/{id:guid}/suspend` | RequireAuthorization, RL | tenants.manage_status | no | ChangeTenantStatusRequest? | 200 TenantDto | 403/404/409/429 |
| 39 | POST | `/tenants/{id:guid}/archive` | RequireAuthorization, RL | tenants.manage_status | no | ChangeTenantStatusRequest? | 200 TenantDto | 404/409/429 |
| 40 | POST | `/tenants/{id:guid}/restore` | RequireAuthorization, RL | tenants.manage_status | no | — | 200 TenantDto | 404/409/429 |
| 41 | DELETE | `/tenants/{id:guid}` | RequireAuthorization, RL | tenants.delete | no | route id | 204 | 403/404/409/429 |
| 42 | GET | `/tenants/{tenantId:guid}/audit` | RequireAuthorization | tenants.audit.read | no | page,pageSize(qs) | 200 paged | 401/403 |
| 43 | GET | `/tenants/{tenantId:guid}/settings/` | RequireAuthorization | tenants.settings.read | no | — | 200 list | 401/403 |
| 44 | GET | `/tenants/{tenantId:guid}/settings/{key}` | RequireAuthorization | tenants.settings.read | no | route key | 200 TenantSettingDto | 401/403/404 |
| 45 | PUT | `/tenants/{tenantId:guid}/settings/{key}` | RequireAuthorization, RL | tenants.settings.update | no | SetTenantSettingRequest | 204 | 401/403/429 |
| 46 | PUT | `/tenants/{tenantId:guid}/settings/` | RequireAuthorization, RL | tenants.settings.update | no | SetTenantSettingsRequest | 204 | 401/403/429 |
| 47 | DELETE | `/tenants/{tenantId:guid}/settings/{key}` | RequireAuthorization, RL | tenants.settings.update | no | route key | 204 | 401/403/404/429 |
| 48 | GET | `/tenants/{tenantId:guid}/members/` | RequireAuthorization | tenants.members.read | no | — | 200 list | 401/403 |
| 49 | POST | `/tenants/{tenantId:guid}/members/` | RequireAuthorization, RL | tenants.members.manage | no | AddTenantMemberRequest | 200/204 TenantMemberDto | 400/401/403/404/429 |
| 50 | DELETE | `/tenants/{tenantId:guid}/members/{userId:guid}` | RequireAuthorization, RL | tenants.members.manage | no | route userId | 204 | 401/403/404/429 |
| 51 | PUT | `/tenants/{tenantId:guid}/members/{userId:guid}/owner` | RequireAuthorization, RL | tenants.members.manage | no | SetTenantOwnerRequest | 204 | 401/403/404/409/429 |

## D. Web host-level endpoints (`Dastarkhwan.Web/Program.cs`)
| # | Method | Route | Auth | Notes |
|---|---|---|---|---|
| 52 | GET | `/health/live` | anon | `{status:live}` |
| 53 | GET | `/health/ready` | anon | 200 ready / 503 if DB unreachable |
| 54 | GET | `/` | anon | dev root message |
| 55 | GET | `/demo/public` | anon | dev |
| 56 | GET | `/demo/needs-login` | RequireAuthorization | dev |
| 57 | GET | `/demo/needs-users-read` | identity.users.read (platform) | dev |
| 58 | GET | `/demo/needs-tenant` | RequireAuthorization + RequireTenant | dev |
| 59 | POST | `/demo/sale` | RequireAuthorization + RequireTenant + sales.create | dev |
| 60 | POST | `/demo/refund` | RequireAuthorization + RequireTenant + sales.refund | dev |
| 61 | GET | `/dev/verification-token` | anon (dev only) | DevEmailInbox |
| 62 | GET | `/dev/password-reset-token` | anon (dev only) | DevEmailInbox |

## E. Admin host-level endpoints (`Dastarkhwan.Admin/Program.cs`) — port 53586/53587
Admin also maps Identity/Tenant/Access groups (same as Web). Admin-specific:

| # | Method | Route | Auth | Perm | Notes |
|---|---|---|---|---|---|
| 63 | POST | `/admin/platform/members` | RequireAuthorization | tenants.platform | Add platform member |
| 64 | GET | `/admin/health/ready` | RequireAuthorization | — | 200 ready (auth-protected) |

## Notes
- Demo (54–60) and dev (61–62) endpoints are Development-only and never registered in Production.
- Admin host shares `/identity`, `/tenants`, `/access` routes with Web; E2E must target the correct host base URL.
- Total: **64 endpoints** (51 module + 13 host-level).