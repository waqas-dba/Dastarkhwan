#!/bin/bash
# Dastarkhwan Postman CI Test Runner
# Runs critical tests for CI/CD pipeline
#
# Usage: ./run-postman-ci.sh
# Exit codes: 0 = success, 1 = failure

set -euo pipefail

COLLECTION="Dastarkhwan.postman_collection.json"
ENVIRONMENT="Dastarkhwan.postman_environment.json"
BASE_URL="${BASE_URL:-https://localhost:53584}"
EMAIL="${ADMIN_EMAIL:-admin@example.com}"
PASSWORD="${ADMIN_PASSWORD:-a-long-test-password}"

echo "============================================"
echo "  Dastarkhwan Postman CI Tests"
echo "============================================"
echo "Base URL: $BASE_URL"
echo ""

# Check if service is up
echo "Checking service availability..."
for i in {1..30}; do
    if curl -k -s -f "$BASE_URL/health/live" >/dev/null 2>&1; then
        echo "Service is live!"
        break
    fi
    if [[ $i -eq 30 ]]; then
        echo "Service not available after 30 attempts"
        exit 1
    fi
    sleep 2
done

# Run critical tests only (folders: Health, Identity > Auth > login/me, Tenants > me, Access > me/roles/permissions)
# Use --folder to run specific folders
echo "Running critical test folders..."

# We'll use newman with a filter for critical folders
newman run "$COLLECTION" \
    --environment "$ENVIRONMENT" \
    --global-var "baseUrl=$BASE_URL" \
    --global-var "email=$EMAIL" \
    --global-var "adminPassword=$PASSWORD" \
    --insecure \
    --reporters cli,junit \
    --reporter-junit-export newman-results.xml \
    --folder "Health" \
    --folder "Identity > Auth" \
    --folder "Tenants > Current User Tenants" \
    --folder "Access > My Access"

EXIT_CODE=$?

echo ""
if [[ $EXIT_CODE -eq 0 ]]; then
    echo "CI Tests PASSED"
else
    echo "CI Tests FAILED"
fi

exit $EXIT_CODE