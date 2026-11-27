# Dastarkhwan Postman Test Suite

## What this is

A single Postman collection that exercises every public HTTP surface of the platform: `CoreKit.Identity`, `CoreKit.Tenant`, `CoreKit.Access`, and the business demo. Covers:

- Positive paths (each endpoint happy-path)
- Negative paths (validation, not-found, conflict)
- **Security regressions** (anonymous access, cross-tenant attempts, privilege escalation, last-admin protection, forged tokens)
- Rate limiting expectations

## Prerequisites

Before you run:

1. **Seed admin exists.**
   Configure once in `appsettings.json` or via env: