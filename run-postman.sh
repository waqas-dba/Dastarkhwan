#!/bin/bash
# Dastarkhwan Postman Collection Runner
# Runs the Postman collection using Newman (Postman CLI)
#
# Prerequisites:
#   npm install -g newman newman-reporter-htmlextra
#
# Usage:
#   ./run-postman.sh [options]
# Options:
#   --collection    Path to collection (default: Dastarkhwan.postman_collection.json)
#   --environment   Path to environment file (optional)
#   --base-url      Base URL override (default: from collection)
#   --email         Admin email (default: admin@example.com)
#   --password      Admin password (default: a-long-test-password)
#   --reporters     Newman reporters (default: cli,htmlextra)
#   --output-dir    Output directory for reports (default: ./newman-reports)
#   --iteration-data CSV/JSON file for data-driven tests
#   --help          Show this help

set -euo pipefail

COLLECTION="Dastarkhwan.postman_collection.json"
ENVIRONMENT=""
BASE_URL="https://localhost:53584"
EMAIL="admin@example.com"
PASSWORD="a-long-test-password"
REPORTERS="cli,htmlextra"
OUTPUT_DIR="./newman-reports"
ITERATION_DATA=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --collection) COLLECTION="$2"; shift 2 ;;
        --environment) ENVIRONMENT="$2"; shift 2 ;;
        --base-url) BASE_URL="$2"; shift 2 ;;
        --email) EMAIL="$2"; shift 2 ;;
        --password) PASSWORD="$2"; shift 2 ;;
        --reporters) REPORTERS="$2"; shift 2 ;;
        --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
        --iteration-data) ITERATION_DATA="$2"; shift 2 ;;
        --help)
            grep '^#' "$0" | sed 's/^#//'
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Check if Newman is installed
if ! command -v newman &> /dev/null; then
    echo "Error: Newman not found. Install with: npm install -g newman newman-reporter-htmlextra"
    exit 1
fi

# Check collection exists
if [[ ! -f "$COLLECTION" ]]; then
    echo "Error: Collection not found: $COLLECTION"
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo "============================================"
echo "  Dastarkhwan Postman Collection Runner"
echo "============================================"
echo "Collection: $COLLECTION"
echo "Base URL: $BASE_URL"
echo "Email: $EMAIL"
echo "Output: $OUTPUT_DIR"
echo ""

# Build Newman command
NEWMAN_CMD=("newman" "run" "$COLLECTION")
NEWMAN_CMD+=("--global-var" "baseUrl=$BASE_URL")
NEWMAN_CMD+=("--global-var" "email=$EMAIL")
NEWMAN_CMD+=("--global-var" "adminPassword=$PASSWORD")
NEWMAN_CMD+=("--reporters" "$REPORTERS")
NEWMAN_CMD+=("--reporter-htmlextra-export" "$OUTPUT_DIR/report.html")
NEWMAN_CMD+=("--reporter-cli-show-all-iteration" "true")

if [[ -n "$ENVIRONMENT" ]]; then
    if [[ -f "$ENVIRONMENT" ]]; then
        NEWMAN_CMD+=("--environment" "$ENVIRONMENT")
    else
        echo "Warning: Environment file not found: $ENVIRONMENT"
    fi
fi

if [[ -n "$ITERATION_DATA" ]]; then
    if [[ -f "$ITERATION_DATA" ]]; then
        NEWMAN_CMD+=("--iteration-data" "$ITERATION_DATA")
    else
        echo "Warning: Iteration data file not found: $ITERATION_DATA"
    fi
fi

# Disable SSL verification for local development
NEWMAN_CMD+=("--insecure")

echo "Running: ${NEWMAN_CMD[*]}"
echo ""

# Run Newman
"${NEWMAN_CMD[@]}"

EXIT_CODE=$?

echo ""
echo "============================================"
if [[ $EXIT_CODE -eq 0 ]]; then
    echo "  Collection run PASSED" -ForegroundColor Green 2>/dev/null || echo "  Collection run PASSED"
else
    echo "  Collection run FAILED (exit code: $EXIT_CODE)" -ForegroundColor Red 2>/dev/null || echo "  Collection run FAILED (exit code: $EXIT_CODE)"
fi
echo "============================================"
echo "HTML Report: $OUTPUT_DIR/report.html"
echo ""

exit $EXIT_CODE