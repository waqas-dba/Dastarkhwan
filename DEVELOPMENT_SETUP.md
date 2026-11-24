# Development Environment Setup — Home PC vs Work PC

## Overview

This document covers the configuration differences between work PC and home PC environments for the Dastarkhwan project. The main differences are database connection strings, JWT signing keys, and external service credentials.

> **Learning Goal:** Understand *why* we separate configuration this way, not just *how* to copy files. Each section explains the architectural reasoning.

## Configuration Files — Why This Structure?

| File | Purpose | Committed? | Why? |
|------|---------|------------|------|
| `Dastarkhwan.Web/appsettings.json` | Base development config (localhost defaults) | Yes | Shared baseline; safe values only (no secrets) |
| `Dastarkhwan.Web/appsettings.Development.json` | Local overrides | **No** — create locally | **Secrets and machine-specific values never committed** |
| `Dastarkhwan.Web/appsettings.Production.json` | Production template | Yes | Template for CI/CD; uses env var placeholders `${VAR}` |
| `Dastarkhwan.Web/appsettings.SaaS.json` | SaaS deployment template | Yes | Optional overlay for multi-tenant SaaS mode |

### The Configuration Precedence (How .NET Merges Them)

```csharp
// In Program.cs (simplified)
var builder = WebApplication.CreateBuilder(args);

// 1. appsettings.json (always loaded)
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// 2. appsettings.{Environment}.json (e.g., Development, Production)
//    This OVERRIDES values from step 1
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// 3. appsettings.SaaS.json (optional overlay for SaaS mode)
//    Loaded conditionally, OVERRIDES previous
if (builder.Configuration.GetValue<bool>("SaaS:Enabled")) {
    builder.Configuration.AddJsonFile("appsettings.SaaS.json", optional: true, reloadOnChange: true);
}

// 4. Environment variables (HIGHEST priority)
//    OVERRIDES everything above
builder.Configuration.AddEnvironmentVariables();

// 5. User Secrets (Development only, higher than env vars in some setups)
//    For local dev secrets without files
if (builder.Environment.IsDevelopment()) {
    builder.Configuration.AddUserSecrets<Program>();
}
```

**Key Insight:** Later sources *override* earlier ones. This is why `appsettings.Development.json` on your machine wins over the committed `appsettings.json`.

---

## Work PC Configuration (Current)

**Database:** `dastarkhwan_db` on `127.0.0.1:5432`  
**User:** `postgres` / `dastarkhwan_runtime`  
**Password:** `123`

Connection string in `appsettings.Development.json` (local, not committed):
```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Database=dastarkhwan_db;Username=postgres;Password=123"
  }
}
```

> **Why `127.0.0.1` not `localhost`?** On Windows, `localhost` resolves to both IPv4 (`127.0.0.1`) and IPv6 (`::1`). PostgreSQL may only listen on IPv4. Explicit `127.0.0.1` avoids "connection refused" on IPv6.

---

## Home PC Configuration

Create `Dastarkhwan.Web/appsettings.Development.json` with your local values:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_home_password"
  },
  "Identity": {
    "Jwt": {
      "SigningKey": "your-home-pc-super-secret-signing-key-at-least-32-chars-long"
    }
  },
  "Email": {
    "Provider": "Console"
  }
}
```

### Key Differences to Adjust — With Reasoning

| Setting | Work PC | Home PC | **Why This Matters** |
|---------|---------|---------|---------------------|
| Database host | `127.0.0.1` | `localhost` or `127.0.0.1` | Match your PostgreSQL `listen_addresses`. Use `localhost` if PG listens on both IPv4/IPv6. |
| Database name | `dastarkhwan_db` | `dastarkhwan_home` | **Isolation.** Different names = no cross-contamination. Each machine owns its data. |
| Database user | `postgres` | `postgres` (or your user) | Match your local PostgreSQL `CREATE USER`. |
| Password | `123` | `your_home_password` | **Never share passwords across machines.** Generate unique per environment. |
| JWT SigningKey | `your-super-secret...` | **Generate new 32+ char key** | **Critical:** Keys must be unique per deployment. Same key = tokens valid across machines (security breach). |
| Email Provider | `Console` | `Console` | Keep Console for local dev — no external dependency. |

### Why Unique JWT Signing Keys Per Machine?

```csharp
// In TokenService.cs - validation checks the signing key
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Identity:Jwt:SigningKey"]));
// If Machine A and Machine B share the same key:
// 1. Token issued on Machine A works on Machine B (unintended trust)
// 2. Compromise of one machine compromises all
// 3. No audit trail of which machine issued a token
```

**Rule:** Treat signing keys like passwords — unique per environment, rotated periodically.

---

## Generating a JWT Signing Key — Multiple Methods

### Method 1: OpenSSL (Cross-platform, recommended)
```bash
openssl rand -base64 32
# Output: 44-char base64 string (32 bytes = 256 bits)
```

### Method 2: PowerShell (Windows native)
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

### Method 3: .NET Interactive (if you have dotnet-script)
```bash
dotnet script -c "Console.WriteLine(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))"
```

### Method 4: C# One-liner (run in any .NET project)
```csharp
Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
```

> **Why 32 bytes (256 bits)?** HS256 (HMAC-SHA256) requires a key of at least 256 bits for cryptographic strength. Shorter keys fail the `IsPlaceholderSigningKey()` validation.

---

## Database Setup on Home PC — Step by Step with Explanations

### 1. Create Database and User — Understanding Roles

```sql
-- Run as postgres superuser (admin)
CREATE DATABASE dastarkhwan_home;           -- Isolated database for this machine
CREATE USER dastarkhwan_runtime WITH PASSWORD 'your_home_password' NOINHERIT;
-- NOINHERIT = user doesn't automatically get permissions from roles it's a member of
-- This is a SECURITY FEATURE: explicit grants required

GRANT CONNECT ON DATABASE dastarkhwan_home TO dastarkhwan_runtime;
-- CONNECT only allows connection, NOT schema/table access yet

-- After migrations create schemas/tables, grant explicit permissions:
\c dastarkhwan_home
GRANT USAGE ON SCHEMA identity, access, tenant TO dastarkhwan_runtime;
-- USAGE on schema = "you can reference objects in this schema"

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity, access, tenant TO dastarkhwan_runtime;
-- DML permissions on existing tables

ALTER DEFAULT PRIVILEGES IN SCHEMA identity, access, tenant 
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dastarkhwan_runtime;
-- DEFAULT PRIVILEGES = future tables created by migrations also get these grants
-- Without this, new migration tables would be inaccessible to the runtime user
```

> **Why `NOINHERIT` and explicit grants?** This implements **principle of least privilege**. The runtime user can *only* do what's explicitly granted. Even if accidentally added to a powerful role, it gains no extra permissions.

### 2. Apply Migrations — Understanding EF Core Migrations

```bash
cd Dastarkhwan.Web

# Each module has its own DbContext and migration history
# --project points to the module containing the DbContext
# --context specifies which DbContext (a project can have multiple)

dotnet ef database update --project ../CoreKit.Identity --context IdentityDbContext
dotnet ef database update --project ../CoreKit.Tenant --context TenantDbContext
dotnet ef database update --project ../CoreKit.Access --context AccessDbContext
```

**What happens during `database update`:**
1. EF Core reads `__EFMigrationsHistory` table to see which migrations applied
2. Compares with migration classes in the project
3. Runs pending migrations *in order* (by timestamp prefix)
4. Each migration runs in a transaction — all-or-nothing
5. Records migration ID in `__EFMigrationsHistory`

> **Why separate DbContexts per module?** Architectural boundary. Identity, Tenant, Access each own their schema. No cross-module foreign keys in the database. This enforces modular monolith boundaries at the data layer.

---

## Common Issues — Root Causes and Fixes

### Issue: "Different names creating problem"

**Symptom:** Migration fails or connection fails because database name/user differs.

**Root Cause:** You copied `appsettings.Development.json` from work PC but your home PostgreSQL has different database name/user/password.

**Fix:** Create a *fresh* `appsettings.Development.json` on home PC with values matching your home PostgreSQL instance. Never copy this file between machines.

### Issue: "JWT SigningKey too short"

**Symptom:** Startup throws `IsPlaceholderSigningKey()` validation error.

**Root Cause:** The default `appsettings.json` has a placeholder key: `"your-super-secret-signing-key-at-least-32-chars-long"`. The validation code checks:
```csharp
// CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs
private static bool IsPlaceholderSigningKey(string key) {
    return key.Length < 32 || key.Contains("your-super-secret", StringComparison.OrdinalIgnoreCase);
}
```

**Fix:** Generate a real 32+ byte base64 key (see methods above) and put it in your local `appsettings.Development.json`.

### Issue: "RLS policies block queries"

**Symptom:** Queries return empty results when using non-superuser role (`dastarkhwan_runtime`).

**Root Cause:** Row Level Security (RLS) policies use `current_setting('app.user_id')` to filter rows. If `app.user_id` isn't set, the policy returns no rows (fail-closed).

```sql
-- Example RLS policy on Identity_Users
CREATE POLICY user_isolation ON identity."Identity_Users"
  USING (id = current_setting('app.user_id')::uuid);
-- If app.user_id not set → current_setting returns NULL → id = NULL → no rows
```

**Fix:** 
- In development: The `IdentityRowLevelSecurityInterceptor` should call `set_config('app.user_id', ...)` before queries
- In tests: Use the `TestUserContext` or set the config manually
- For debugging: Run as `postgres` superuser (bypasses RLS) to verify data exists

### Issue: "Port 5432 already in use"

**Symptom:** PostgreSQL won't start or connection refused.

**Root Cause:** Another PostgreSQL instance (e.g., Docker, Windows service, another version) holds port 5432.

**Fix:** 
```bash
# Windows - find what's using port 5432
netstat -ano | findstr 5432
# Note the PID, then:
taskkill /PID <pid> /F

# Or change PostgreSQL port in postgresql.conf:
# port = 5433
# Then update connection string: Host=localhost;Port=5433;...
```

---

## Quick Start Checklist (Home PC) — With Verification Steps

- [ ] Install PostgreSQL 16+ → Verify: `psql --version`
- [ ] Create database `dastarkhwan_home` and user `dastarkhwan_runtime` → Verify: `\l` and `\du` in psql
- [ ] Create `Dastarkhwan.Web/appsettings.Development.json` with your connection string → Verify: `cat appsettings.Development.json` (no placeholder values)
- [ ] Generate and add unique JWT `SigningKey` → Verify: key is 44+ chars, base64, no "your-super-secret"
- [ ] Run migrations for Identity, Tenant, Access → Verify: `dotnet ef migrations list` shows all applied
- [ ] Verify `dotnet build Dastarkhwan.slnx` succeeds → Verify: 0 errors (warnings OK)
- [ ] Run `dotnet test` to confirm test compilation → Verify: all test projects compile (some tests may fail due to SQLite vs PG differences)

---

## Environment Variables (Alternative to appsettings.Development.json)

Instead of a file, you can use environment variables. **Precedence: Environment Variables > appsettings.Development.json > appsettings.json**

```bash
# Windows PowerShell (current session only)
$env:ConnectionStrings__Default = "Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_password"
$env:Identity__Jwt__SigningKey = "your-generated-key"

# Windows PowerShell (persistent - user level)
[Environment]::SetEnvironmentVariable("ConnectionStrings__Default", "Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_password", "User")

# Linux/Mac (current session)
export ConnectionStrings__Default="Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_password"
export Identity__Jwt__SigningKey="your-generated-key"

# Linux/Mac (persistent - add to ~/.bashrc or ~/.zshrc)
echo 'export ConnectionStrings__Default="Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_password"' >> ~/.bashrc
```

> **Why double underscore `__`?** .NET configuration uses `__` as hierarchy separator for environment variables. `ConnectionStrings__Default` maps to `ConnectionStrings:Default` in JSON.

---

## User Secrets (Best for Development Secrets)

```bash
# Initialize user secrets for the Web project
cd Dastarkhwan.Web
dotnet user-secrets init

# Set secrets (stored in ~/.microsoft/usersecrets/<project-guid>/secrets.json)
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=dastarkhwan_home;Username=postgres;Password=your_password"
dotnet user-secrets set "Identity:Jwt:SigningKey" "your-generated-key"

# List secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "ConnectionStrings:Default"
```

> **Why User Secrets?**
> - Not in source control (stored in OS user profile)
> - Higher precedence than `appsettings.Development.json`
> - Per-project isolation
> - Works with `dotnet run` automatically in Development

---

## Security Notes — The "Why" Behind Each Rule

| Rule | Why? |
|------|------|
| **Never commit `appsettings.Development.json`** | It contains machine-specific connection strings and secrets. Committing it exposes your local DB credentials and creates conflicts for other developers. |
| **Never commit real passwords/JWT keys/API keys** | Git history is permanent. Even if deleted later, secrets remain in history. Attackers scan public repos for leaked keys. |
| **Rotate secrets if ever accidentally committed** | Assume compromised immediately. Generate new keys, update all environments, revoke old ones. |
| **Don't print secret values in reports/logs** | Logs are often aggregated, shared, or leaked. Structured logging should redact sensitive fields. |
| **Use User Secrets for sensitive values in development** | Keeps secrets out of files entirely. OS-protected storage. |

---

## Architecture Context — How This Fits the Big Picture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Dastarkhwan Solution                         │
├─────────────────────────────────────────────────────────────────┤
│  Dastarkhwan.Web (Host)                                         │
│  ├── appsettings.json (committed, safe defaults)               │
│  ├── appsettings.Development.json (YOUR MACHINE ONLY, gitignored)│
│  ├── appsettings.Production.json (template for CI/CD)          │
│  └── appsettings.SaaS.json (optional overlay)                  │
├─────────────────────────────────────────────────────────────────┤
│  CoreKit.Identity  → IdentityDbContext → identity schema       │
│  CoreKit.Tenant    → TenantDbContext   → tenant schema         │
│  CoreKit.Access    → AccessDbContext   → access schema         │
│  Dastarkhwan.Integration (composes all three)                  │
└─────────────────────────────────────────────────────────────────┘
```

**Each developer machine = isolated environment:**
- Own database (`dastarkhwan_home` vs `dastarkhwan_db`)
- Own JWT signing key (tokens not portable)
- Own `appsettings.Development.json` (never shared)
- Shared: `appsettings.json`, `appsettings.Production.json`, code

This mirrors production: each deployment (dev, staging, prod) has its own config, database, and keys.