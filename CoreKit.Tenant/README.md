# CoreKit.Tenant

A reusable multi-tenancy module for .NET SaaS apps. It answers three questions for any application built on top of it:

1. **Who is the tenant of this request?** (resolution)
2. **Can this tenant be served right now?** (lifecycle and status)
3. **Can this code touch this data?** (isolation)

The same module works for a restaurant SaaS, an e-commerce SaaS, a school SaaS or a gym SaaS. It contains no business concepts from any of them.

## What it does and does not do

| Handles | Does not handle (use another module) |
|---|---|
| Tenant entity, create / edit / delete | Stores and branches (`CoreKit.Store`) |
| Lifecycle: pending, active, suspended, archived | Subscriptions and billing (`CoreKit.Subscription`) |
| Current tenant context (`ICurrentTenant`) | Features and limits (`CoreKit.Entitlement`) |
| Finding the tenant of a request | Domains and DNS (`CoreKit.Domain`) |
| Tenant data isolation for EF Core | Anything specific to your product |
| Tenant settings (key/value) | |
| Tenant members and owners | |
| Authorization integration with `CoreKit.IAM` | |
| Audit trail and in-process events | |

## How it fits

```text
   Your application  ──►  CoreKit.Tenant  ──►  CoreKit.IAM
   (any SaaS)              (this module)        (users, roles, JWT)
```

The dependency only points one way. Tenant never knows about your application. Your application normally needs only two abstractions:

```csharp
ICurrentTenant   // which tenant is this request for?
ITenantService   // manage tenants
```

Everything else (resolution strategies, caching, repositories, middleware) is `internal`.

## Quick start

### 1. Reference and configure

```xml
<ProjectReference Include="..\CoreKit.Tenant\CoreKit.Tenant.csproj" />
```

```powershell
dotnet user-secrets set "ConnectionStrings:Tenant" "Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=YOUR_PASSWORD" --project YourHost
```

```json
{
  "Tenant": {
    "InfoCacheSeconds": 30,
    "MaxSettingsPerTenant": 200,
    "AdditionalReservedSlugs": [],
    "Resolution": { "TrustHeader": false, "HeaderName": "X-Tenant-Id" }
  }
}
```

### 2. Register and wire

```csharp
builder.Services.AddIam(builder.Configuration);
builder.Services.AddTenant(builder.Configuration);       // after AddIam

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IamDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
}

await app.Services.SeedIamAsync(TenantPermissionSeeds.All);

app.UseIam();       // authentication -> rate limiter -> authorization
app.UseTenant();    // must come after UseIam: it reads the tenant claim of the signed-in user
app.MapIam();
app.MapTenants();   // optional HTTP API under /tenants
```

### 3. Create the database tables

```powershell
$env:ConnectionStrings__Tenant = "Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=YOUR_PASSWORD"
dotnet ef migrations add Init_tenant --project CoreKit.Tenant --startup-project CoreKit.Tenant --context TenantDbContext --output-dir Migrations
dotnet ef database update --project CoreKit.Tenant --startup-project CoreKit.Tenant --context TenantDbContext
```

In Visual Studio's Package Manager Console the same command is:

```powershell
Add-Migration Init_tenant -Project CoreKit.Tenant -StartupProject CoreKit.Tenant -Context TenantDbContext -OutputDir Migrations
```

## Concepts

### Tenant lifecycle

A tenant is always in one of four states.

| Status | Meaning | Serves requests? | Editable? |
|---|---|---|---|
| `Pending` | Created, waiting to be activated (for example, approval) | No | Yes |
| `Active` | Normal operation | **Yes** | Yes |
| `Suspended` | Blocked by the platform (for example, unpaid) | No | Yes |
| `Archived` | Closed. Read-only | No | No |

Allowed changes, and nothing else:

```text
Pending   ──► Active, Archived
Active    ──► Suspended, Archived
Suspended ──► Active, Archived
Archived  ──► Suspended          (Restore; an administrator then activates it on purpose)
```

A tenant can be **deleted permanently only when it is Archived**. Deleting removes its settings and members but keeps its audit history.

Status is checked on every request. Suspending a tenant blocks its next request even if users still hold valid tokens.

### Slug

Every tenant has a unique, lowercase identifier such as `acme-foods`.

- 3 to 63 characters: lowercase letters, digits and single hyphens
- must start and end with a letter or digit
- cannot be a reserved word: `www api app admin root system platform support help status mail static assets cdn auth login signup dashboard billing docs blog` plus `Tenant:AdditionalReservedSlugs`
- if you do not provide one, it is built from the name (`Café Délice` becomes `cafe-delice`). If taken, the module tries `-2`, `-3` and so on. Names with no Latin letters or digits get `tenant`.

The slug is also a valid DNS label, so it can become a sub-domain later.

### Settings

Per-tenant key/value pairs, for example `general.timezone` = `Asia/Karachi`.

- keys: lowercase letters, digits and `. _ -`, up to 100 characters
- values: up to 4000 characters
- up to 200 settings per tenant (`Tenant:MaxSettingsPerTenant`)
- writing many settings at once is all-or-nothing
- the audit trail records which keys changed, **never their values**

### Members and owners

A member links an IAM user to a tenant. A tenant always keeps at least one owner. A user who belongs to several tenants has one **default** tenant: the first one they join, changeable later.

`TenantMember.UserId` points at an IAM user but has no foreign key, because the tenant and IAM databases are independent. Your code should check that the user exists before adding them.

### Audit trail and events

Every change writes an audit entry **in the same database transaction** as the change itself. History survives permanent deletion of a tenant.

| Audit action | When |
|---|---|
| `tenant.created`, `tenant.updated`, `tenant.activated`, `tenant.suspended`, `tenant.archived`, `tenant.restored`, `tenant.deleted` | Lifecycle |
| `tenant.settings_updated`, `tenant.setting_removed` | Settings |
| `tenant.member_added`, `tenant.member_removed`, `tenant.owner_changed`, `tenant.default_changed` | Members |

After a change is saved, an in-process event is published. Any module can react:

```csharp
public sealed class DeleteCatalogOnTenantDeleted : ITenantEventHandler<TenantDeletedEvent>
{
    private readonly CatalogDbContext _catalog;
    public DeleteCatalogOnTenantDeleted(CatalogDbContext catalog) => _catalog = catalog;

    public async Task HandleAsync(TenantDeletedEvent e, CancellationToken ct = default)
    {
        await _catalog.Products.IgnoreQueryFilters().Where(p => p.TenantId == e.TenantId).ExecuteDeleteAsync(ct);
    }
}

services.AddScoped<ITenantEventHandler<TenantDeletedEvent>, DeleteCatalogOnTenantDeleted>();
```

Events: `TenantCreatedEvent`, `TenantUpdatedEvent`, `TenantStatusChangedEvent`, `TenantDeletedEvent`, `TenantMemberAddedEvent`, `TenantMemberRemovedEvent`, `TenantSettingChangedEvent`.

A handler that throws is logged and does **not** undo the change or stop other handlers. Do not rely on handlers for work that must never be lost.

## Using it in your application

### Read the current tenant

```csharp
app.MapGet("/menu", (ICurrentTenant tenant, MenuService menu) =>
        menu.ForTenant(tenant.RequiredId))   // throws TenantNotResolvedException if there is none
   .RequireAuthorization()
   .RequireTenant();                         // answers 400 "tenant.required" if there is none
```

For controllers, put `[RequireTenant]` on the class or action.

| Member | Meaning |
|---|---|
| `IsResolved` / `Id` | Whether and which tenant this request belongs to |
| `RequiredId` | The id, or an exception when there is none |
| `Change(tenantId)` | Act as another tenant until the returned object is disposed. `Change(null)` acts as no tenant. For background jobs, seeders and tests |

### Make your own data tenant-isolated

Mark the entity and derive your DbContext from `TenantAwareDbContext`:

```csharp
public sealed class Product : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }          // filled in for you on insert
    public string Name { get; set; } = "";
}

public sealed class ShopDbContext : TenantAwareDbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options, ICurrentTenant tenant) : base(options, tenant) { }

    public DbSet<Product> Products => Set<Product>();

    // Override ConfigureModel, not OnModelCreating. The base class applies the tenant filter after it.
    protected override void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopDbContext).Assembly);
}
```

What you get:

| Situation | Result |
|---|---|
| Any query | Only the current tenant's rows |
| Query with no tenant resolved | **No rows** (it fails closed) |
| Insert with an empty `TenantId` | Filled in with the current tenant |
| Insert for another tenant | `TenantIsolationException` |
| Insert with no tenant resolved | `TenantNotResolvedException` |
| Update or delete a row of another tenant (loaded with `IgnoreQueryFilters`) | `TenantIsolationException` |
| Changing a row's `TenantId` | `TenantIsolationException` |

To work across tenants **on purpose**, use `IgnoreQueryFilters()` for reads, or `ICurrentTenant.Change(id)` for writes.

### Manage tenants from code

```csharp
var created = await tenants.CreateAsync(new CreateTenantRequest
{
    Name = "Acme Foods",
    OwnerUserId = ownerId,           // optional
    ActivateImmediately = true       // false creates it as Pending
});

if (created.IsFailure) return created.Error!.ToHttpResult();
var tenant = created.Value;
```

Every method returns a `TenantResult` or `TenantResult<T>`. Expected problems come back as a value with a stable error code, not as an exception.

### Trusted code that is not a signed-in administrator

A sign-up flow or a seeder has no platform-administrator user, but it needs to create tenants. Grant platform access for a short time:

```csharp
using (platformAccess.Grant())          // ITenantPlatformAccess
{
    await tenants.CreateAsync(new CreateTenantRequest { Name = name, OwnerUserId = userId });
}
```

## How the tenant of a request is found

`app.UseTenant()` asks each registered strategy, in order, for a tenant identifier.

| Strategy | Order | Source | Enabled |
|---|---|---|---|
| Claim | 100 | `tenant_id` claim of the signed-in user's token (signed by IAM, so clients cannot change it) | Always |
| Header | 200 | `X-Tenant-Id` header (an id or a slug) | **Only if `Tenant:Resolution:TrustHeader` is true** |

Then, in this order:

1. If **no** strategy finds a tenant, the request continues without one. Use `RequireTenant()` on routes that need one.
2. If the tenant does not exist: **404** `tenant.not_found`.
3. If strategies find **different** tenants: **403** `tenant.mismatch`. A client cannot pick a different tenant than its token's.
4. If the tenant is not `Active`: **403** `tenant.pending`, `tenant.suspended` or `tenant.archived`.
5. Otherwise `ICurrentTenant` is set for the rest of the request.

> **Header security.** A client can send any header. Turn `TrustHeader` on only when your reverse proxy removes the header from incoming requests and sets it itself (for example, from the host name). With IAM's claim present, a wrong header is rejected anyway (step 3).

Tenant id, slug and status are remembered for `Tenant:InfoCacheSeconds` (default 30) and forgotten immediately when the tenant changes on the same server. With several servers, a change can take up to that many seconds to reach the others. Set it to `0` to turn the cache off.

### Adding your own strategy

For example, a domain module that maps `shop.example.com` to a tenant:

```csharp
public sealed class DomainTenantStrategy : ITenantResolutionStrategy
{
    public int Order => 150;

    public Task<TenantIdentifier?> ResolveAsync(HttpContext http, CancellationToken ct = default)
    {
        var slug = LookupSlugForHost(http.Request.Host.Host);
        return Task.FromResult<TenantIdentifier?>(slug is null ? null : new TenantIdentifier(null, slug, "domain"));
    }
}

services.AddScoped<ITenantResolutionStrategy, DomainTenantStrategy>();
```

## Security model

IAM roles are global, so a tenant-bound user could otherwise reach other tenants. The tenant boundary is enforced **inside the services**, whatever the permissions say.

| Caller | May do |
|---|---|
| **Bound to a tenant** (a tenant was resolved) | Read and edit **only that tenant**: rename, settings, members, audit. Never list or create tenants, change status, change a slug or delete |
| **Not bound, with platform access** (`tenants.platform` permission, or inside `platformAccess.Grant()`) | Everything, across all tenants |
| **Not bound, without platform access** | Nothing. A missing tenant claim never turns into extra power |
| A platform administrator who is **acting inside a tenant** | Only that tenant, until they drop the header or claim |

Asking about a tenant you cannot reach returns `tenant.cross_tenant_access` whether or not it exists, so ids cannot be probed.

Permissions only decide whether an endpoint can be called at all. The boundary above decides which tenants it can touch.

## IAM integration

| What | How |
|---|---|
| Tenant in the token | `TenantClaimsContributor` adds a `tenant_id` claim to every access token IAM issues. It is the user's default tenant, else their oldest non-archived one. Users with no tenant get no claim |
| Permissions | Seed them with `await app.Services.SeedIamAsync(TenantPermissionSeeds.All)`. The Administrator role receives all of them |
| Who is acting | `IamTenantActor` reads the user from IAM's `ICurrentUserService`. Platform administrators hold `tenants.platform` |
| Without IAM | Register your own `ITenantActor` before `AddTenant`; it takes precedence |

A user who signed in before being added to a tenant has no claim until they sign in again or refresh their token.

| Permission | Allows |
|---|---|
| `tenants.read` | View tenants |
| `tenants.create` | Create tenants |
| `tenants.update` | Edit tenants |
| `tenants.manage_status` | Activate, suspend, archive, restore |
| `tenants.delete` | Delete archived tenants |
| `tenants.settings.read` / `tenants.settings.update` | View / change settings |
| `tenants.members.read` / `tenants.members.manage` | View / change members |
| `tenants.audit.read` | View the audit log |
| `tenants.platform` | Act across all tenants |

## HTTP API (`app.MapTenants()`)

Default prefix `/tenants`; pass another to `MapTenants("/api/tenants")`.

| Method and path | Permission |
|---|---|
| `GET /` | `tenants.read` (platform) |
| `GET /current` | signed in, tenant required |
| `GET /me` · `PUT /me/default/{tenantId}` | signed in |
| `GET /{id}` | `tenants.read` |
| `POST /` | `tenants.create` (platform), answers 201 |
| `PUT /{id}` | `tenants.update` |
| `POST /{id}/activate` · `/suspend` · `/archive` · `/restore` | `tenants.manage_status` (platform) |
| `DELETE /{id}` | `tenants.delete` (platform) |
| `GET /{tenantId}/settings` · `GET .../{key}` | `tenants.settings.read` |
| `PUT /{tenantId}/settings/{key}` · `PUT .../settings` · `DELETE .../{key}` | `tenants.settings.update` |
| `GET /{tenantId}/members` | `tenants.members.read` |
| `POST /{tenantId}/members` · `DELETE .../{userId}` · `PUT .../{userId}/owner` | `tenants.members.manage` |
| `GET /{tenantId}/audit?page=&pageSize=` | `tenants.audit.read` |

Request bodies: `CreateTenantRequest`, `UpdateTenantRequest`, `ChangeTenantStatusRequest { reason }`, `SetTenantSettingRequest { value }`, `SetTenantSettingsRequest { values }`, `AddTenantMemberRequest { userId, isOwner }`, `SetTenantOwnerRequest { isOwner }`.

## Error codes

Errors are returned as `{ "code": "...", "message": "..." }`.

| Code | HTTP | Meaning |
|---|---|---|
| `validation.failed` | 400 | Bad input (name, slug, key, value, reason) |
| `tenant.required` | 400 | The route needs a tenant and none was identified |
| `tenant.not_authenticated` | 401 | Not signed in |
| `tenant.cross_tenant_access` | 403 | Tried to reach another tenant |
| `tenant.platform_only` | 403 | Needs platform access |
| `tenant.mismatch` | 403 | Token and header name different tenants |
| `tenant.pending` / `tenant.suspended` / `tenant.archived` | 403 | The tenant cannot serve requests |
| `forbidden` | 403 | Other refusals |
| `tenant.not_found` | 404 | Unknown tenant on resolution |
| `not_found` | 404 | A tenant, setting or member does not exist |
| `conflict` | 409 | Duplicate slug, duplicate member, last owner, limits |
| `tenant.invalid_transition` | 409 | Status change not allowed from the current status |
| `tenant.read_only` | 409 | Tried to change an archived tenant |
| `tenant.save_conflict` | 409 | Two changes collided. Reload and try again |

## Database

Own context, `TenantDbContext`. Own migration history table, `__TenantMigrationsHistory`. It can share a database with IAM.

| Table | Content |
|---|---|
| `TENANT_Tenants` | Tenants. Unique index on `Slug`; a concurrency stamp stops two editors overwriting each other |
| `TENANT_Settings` | Key/value settings. Primary key `(TenantId, Key)` |
| `TENANT_Members` | Memberships. Primary key `(TenantId, UserId)` |
| `TENANT_AuditEntries` | Audit trail. No foreign key, so it outlives the tenant |

## Configuration reference

| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:Tenant` | required | PostgreSQL connection |
| `Tenant:InfoCacheSeconds` | `30` | How long a tenant's status is remembered. `0` = no cache |
| `Tenant:MaxSettingsPerTenant` | `200` | Settings limit |
| `Tenant:AdditionalReservedSlugs` | `[]` | Extra slugs nobody can take |
| `Tenant:Resolution:TrustHeader` | `false` | Allow the header strategy |
| `Tenant:Resolution:HeaderName` | `X-Tenant-Id` | Header to read |
| `Tenant:Resolution:ClaimType` | `tenant_id` | Claim to read |

Invalid values fail when the options are first read.

## Project layout

```text
CoreKit.Tenant/
  Abstractions/   public contracts: ICurrentTenant, ITenantService, events ...
  Common/         TenantResult, TenantErrors
  Constants/      permission names, claim type, audit actions
  Entities/       TenantEntity, TenantSetting, TenantMember, TenantAuditEntry
  Models/         DTOs and request types
  Settings/       TenantOptions
  Isolation/      ITenantScoped, TenantAwareDbContext, exceptions
  Persistence/    TenantDbContext, repositories, unit of work (internal)
  Resolution/     CurrentTenant, strategies, middleware (internal)
  Services/       business rules (internal)
  Integration/    IAM glue: claims, actor, permission seeds
  Endpoints/      MapTenants, RequireTenant
  Extensions/     AddTenant, UseTenant
CoreKit.Tenant.Tests/
```

The entity is named `TenantEntity` because a class called `Tenant` inside the `CoreKit.Tenant` namespace makes the simple name ambiguous with the namespace.

## Tests

```powershell
dotnet test CoreKit.Tenant.Tests
```

Tests run against an in-memory SQLite database, so they need no PostgreSQL. They cover: slug, lifecycle and validation rules; the tenant boundary; all service operations; audit and events; the tenant cache; resolution strategies and middleware; EF isolation through a sample `ShopDbContext`; the IAM claims contributor and actor; the unit of work (duplicate and concurrent saves); and dependency-injection registration.

## Known limits

- With several servers, a status change can take up to `InfoCacheSeconds` to reach every one.
- Any database refusal during a save becomes `tenant.save_conflict` and is logged at error level. A unique-key clash cannot be told apart from other failures.
- Member user ids are not verified against IAM.
- Deleting a tenant removes only Tenant's own data. Other modules must listen for `TenantDeletedEvent` and delete theirs.
- Events are in-process and best-effort. There is no outbox or retry.
- A user's tenant claim is fixed at sign-in. Changing their default tenant takes effect on their next token.