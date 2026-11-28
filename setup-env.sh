#!/bin/bash
# Dastarkhwan Environment Setup Script
# Configures user secrets, applies migrations, seeds admin user, and verifies the API.
#
# Usage: ./setup-env.sh [options]
# Options:
#   --project-path     Path to Dastarkhwan.Web project (default: Dastarkhwan.Web)
#   --admin-email      Admin email (default: admin@example.com)
#   --admin-password   Admin password (default: a-long-test-password)
#   --admin-mobile     Admin mobile (default: +923000000000)
#   --admin-name       Admin display name (default: Platform Administrator)
#   --jwt-key          JWT signing key (default: from user-secrets)
#   --jwt-issuer       JWT issuer (default: Dastarkhwan)
#   --jwt-audience     JWT audience (default: Dastarkhwan)
#   --db-conn          Database connection string (default: Host=127.0.0.1;Database=dastarkhwan_db;Username=dastarkhwan;Password=dastarkhwan_dev_only)
#   --base-url         Base URL (default: https://localhost:53584)
#   --skip-build       Skip dotnet build
#   --skip-migrate     Skip migrations
#   --help             Show this help

set -euo pipefail

PROJECT_PATH="Dastarkhwan.Web"
ADMIN_EMAIL="admin@example.com"
ADMIN_PASSWORD="a-long-test-password"
ADMIN_MOBILE="+923000000000"
ADMIN_DISPLAY_NAME="Platform Administrator"
JWT_SIGNING_KEY="K3WFXWfpftOu9gxRZYVX774n7qPR44pQ+ERsnmvFV3M="
JWT_ISSUER="Dastarkhwan"
JWT_AUDIENCE="Dastarkhwan"
DB_CONNECTION_STRING="Host=127.0.0.1;Database=dastarkhwan_db;Username=dastarkhwan;Password=dastarkhwan_dev_only"
BASE_URL="https://localhost:53584"
SKIP_BUILD=false
SKIP_MIGRATE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --project-path) PROJECT_PATH="$2"; shift 2 ;;
        --admin-email) ADMIN_EMAIL="$2"; shift 2 ;;
        --admin-password) ADMIN_PASSWORD="$2"; shift 2 ;;
        --admin-mobile) ADMIN_MOBILE="$2"; shift 2 ;;
        --admin-name) ADMIN_DISPLAY_NAME="$2"; shift 2 ;;
        --jwt-key) JWT_SIGNING_KEY="$2"; shift 2 ;;
        --jwt-issuer) JWT_ISSUER="$2"; shift 2 ;;
        --jwt-audience) JWT_AUDIENCE="$2"; shift 2 ;;
        --db-conn) DB_CONNECTION_STRING="$2"; shift 2 ;;
        --base-url) BASE_URL="$2"; shift 2 ;;
        --skip-build) SKIP_BUILD=true; shift ;;
        --skip-migrate) SKIP_MIGRATE=true; shift ;;
        --help)
            grep '^#' "$0" | sed 's/^#//'
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

echo "============================================"
echo "  Dastarkhwan Environment Setup"
echo "============================================"
echo ""

# Check prerequisites
echo "Checking prerequisites..."
dotnet_version=$(dotnet --version)
echo "  .NET SDK: $dotnet_version"

if command -v psql &> /dev/null; then
    pg_version=$(psql --version)
    echo "  PostgreSQL: $pg_version"
else
    echo "  PostgreSQL: not found in PATH (ensure it's running on localhost:5432)"
fi

echo ""

# Navigate to project directory
if [[ ! -d "$PROJECT_PATH" ]]; then
    echo "Error: Project directory not found: $PROJECT_PATH"
    exit 1
fi

cd "$PROJECT_PATH"
echo "Working in: $(pwd)"
echo ""

# 1. Set up user secrets
echo "1. Setting up User Secrets..."
dotnet user-secrets set "Identity:Seed:AdminEmail" "$ADMIN_EMAIL"
dotnet user-secrets set "Identity:Seed:AdminPassword" "$ADMIN_PASSWORD"
dotnet user-secrets set "Identity:Seed:AdminMobile" "$ADMIN_MOBILE"
dotnet user-secrets set "Identity:Seed:AdminDisplayName" "$ADMIN_DISPLAY_NAME"
dotnet user-secrets set "Identity:Jwt:SigningKey" "$JWT_SIGNING_KEY"
dotnet user-secrets set "Identity:Jwt:Issuer" "$JWT_ISSUER"
dotnet user-secrets set "Identity:Jwt:Audience" "$JWT_AUDIENCE"
dotnet user-secrets set "ConnectionStrings:Default" "$DB_CONNECTION_STRING"
echo "  All secrets configured"
echo ""

# 2. Build the project
if [[ "$SKIP_BUILD" != true ]]; then
    echo "2. Building project..."
    dotnet build --no-restore
    echo "  Build successful"
    echo ""
else
    echo "2. Skipping build (--skip-build)"
    echo ""
fi

# 3. Apply migrations
if [[ "$SKIP_MIGRATE" != true ]]; then
    echo "3. Applying EF Core migrations..."
    for context in "IdentityDbContext" "TenantDbContext" "AccessDbContext"; do
        echo "  Migrating $context..."
        dotnet ef database update --context "$context" --no-build
        echo "  $context: Done"
    done
    echo ""
else
    echo "3. Skipping migrations (--skip-migrate)"
    echo ""
fi

# 4. Run the application for seeding and verification
echo "4. Starting application for seeding and verification..."
echo "  This will run migrations, seed admin, and start the web server."
echo "  Press Ctrl+C to stop after verification."
echo ""

# Start the app in background
dotnet run --no-build &
APP_PID=$!
echo "  Application started (PID: $APP_PID)"

# Wait for app to be ready
echo "  Waiting for application to be ready..."
MAX_WAIT=60
WAITED=0
READY=false

while [[ $WAITED -lt $MAX_WAIT && "$READY" != true ]]; do
    if curl -k -s "$BASE_URL/health/live" | grep -q '"status":"live"'; then
        READY=true
        echo "  Application is live!"
    else
        sleep 2
        WAITED=$((WAITED + 2))
        echo "  ... waiting (${WAITED}s)"
    fi
done

if [[ "$READY" != true ]]; then
    echo "Error: Application did not become ready within $MAX_WAIT seconds"
    kill $APP_PID 2>/dev/null || true
    exit 1
fi

echo ""

# 5. Verify API endpoints
echo "5. Verifying API endpoints..."

# Health checks
echo "  Health checks:"
HEALTH_LIVE=$(curl -k -s "$BASE_URL/health/live")
echo "    /health/live: $(echo "$HEALTH_LIVE" | jq -r .status)"

HEALTH_READY=$(curl -k -s "$BASE_URL/health/ready")
echo "    /health/ready: $(echo "$HEALTH_READY" | jq -r .status)"

# Login as admin
echo "  Authentication:"
LOGIN_RESPONSE=$(curl -k -s -X POST "$BASE_URL/identity/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r .accessToken)
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r .refreshToken)
echo "    Login: OK (token acquired)"

# Get current user
ME_RESPONSE=$(curl -k -s "$BASE_URL/identity/auth/me" -H "Authorization: Bearer $ACCESS_TOKEN")
ME_EMAIL=$(echo "$ME_RESPONSE" | jq -r .email)
ME_ID=$(echo "$ME_RESPONSE" | jq -r .id)
echo "    /identity/auth/me: $ME_EMAIL (ID: $ME_ID)"

# Access/me (platform permissions)
ACCESS_ME=$(curl -k -s "$BASE_URL/access/me" -H "Authorization: Bearer $ACCESS_TOKEN")
PERM_COUNT=$(echo "$ACCESS_ME" | jq '.permissions | length')
echo "    /access/me: $PERM_COUNT permissions"

# Tenants/me (user's tenants)
TENANTS_ME=$(curl -k -s "$BASE_URL/tenants/me" -H "Authorization: Bearer $ACCESS_TOKEN")
TENANT_COUNT=$(echo "$TENANTS_ME" | jq 'length')
echo "    /tenants/me: $TENANT_COUNT tenant(s)"

# Access/roles
ROLES=$(curl -k -s "$BASE_URL/access/roles" -H "Authorization: Bearer $ACCESS_TOKEN")
ROLE_COUNT=$(echo "$ROLES" | jq 'length')
echo "    /access/roles: $ROLE_COUNT role(s)"

# Access/permissions
PERMS=$(curl -k -s "$BASE_URL/access/permissions" -H "Authorization: Bearer $ACCESS_TOKEN")
PERM_TOTAL=$(echo "$PERMS" | jq 'length')
echo "    /access/permissions: $PERM_TOTAL permission(s)"

echo ""

# 6. Create a test tenant (optional)
echo "6. Creating test tenant..."
TENANT_BODY="{\"name\":\"Test Tenant\",\"slug\":\"test-tenant\",\"activateImmediately\":true,\"ownerUserId\":\"$ME_ID\"}"
TENANT_RESPONSE=$(curl -k -s -X POST "$BASE_URL/tenants" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -d "$TENANT_BODY" || true)

if echo "$TENANT_RESPONSE" | jq -e .id >/dev/null 2>&1; then
    TENANT_NAME=$(echo "$TENANT_RESPONSE" | jq -r .name)
    TENANT_ID=$(echo "$TENANT_RESPONSE" | jq -r .id)
    echo "    Created tenant: $TENANT_NAME (ID: $TENANT_ID)"
    
    # Test tenant-scoped endpoints
    TENANT_CURRENT=$(curl -k -s "$BASE_URL/tenants/current" \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        -H "X-Tenant-Id: $TENANT_ID")
    echo "    /tenants/current (with X-Tenant-Id): $(echo "$TENANT_CURRENT" | jq -r .name)"
else
    echo "    Tenant creation failed or already exists: $(echo "$TENANT_RESPONSE" | jq -r .detail // .title // .)"
fi

echo ""
echo "============================================"
echo "  Environment Setup Complete!"
echo "============================================"
echo ""
echo "Base URL: $BASE_URL"
echo "Admin Email: $ADMIN_EMAIL"
echo "Admin Password: $ADMIN_PASSWORD"
echo ""
echo "Access Token (for Postman/scripts):"
echo "$ACCESS_TOKEN"
echo ""
echo "Import Dastarkhwan.postman_collection.json into Postman"
echo "Set baseUrl variable to: $BASE_URL"
echo "Set adminPassword variable to: $ADMIN_PASSWORD"
echo ""
echo "Application is running in background (PID: $APP_PID)"
echo "Press Ctrl+C to stop it when done testing."

# Keep script running so app stays up
trap "kill $APP_PID 2>/dev/null || true" EXIT
wait $APP_PID