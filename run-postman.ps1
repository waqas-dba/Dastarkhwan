<#
.SYNOPSIS
    Dastarkhwan Postman Collection Runner (PowerShell)
    Runs the Postman collection using Newman (Postman CLI)

.PREREQUISITES
    npm install -g newman newman-reporter-htmlextra

.USAGE
    .\run-postman.ps1 [-Collection <path>] [-Environment <path>] [-BaseUrl <url>] 
                      [-Email <email>] [-Password <password>] [-Reporters <list>] 
                      [-OutputDir <path>] [-IterationData <path>]

.PARAMETERS
    -Collection     Path to collection (default: Dastarkhwan.postman_collection.json)
    -Environment    Path to Postman environment file (optional)
    -BaseUrl        Base URL override (default: https://localhost:53584)
    -Email          Admin email (default: admin@example.com)
    -Password       Admin password (default: a-long-test-password)
    -Reporters      Newman reporters (default: cli,htmlextra)
    -OutputDir      Output directory for reports (default: ./newman-reports)
    -IterationData  CSV/JSON file for data-driven tests
#>

param(
    [string]$Collection = "Dastarkhwan.postman_collection.json",
    [string]$Environment = "",
    [string]$BaseUrl = "https://localhost:53584",
    [string]$Email = "admin@example.com",
    [string]$Password = "a-long-test-password",
    [string]$Reporters = "cli,htmlextra",
    [string]$OutputDir = "./newman-reports",
    [string]$IterationData = ""
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Dastarkhwan Postman Collection Runner" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Collection: $Collection" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host "Email: $Email" -ForegroundColor Yellow
Write-Host "Output: $OutputDir" -ForegroundColor Yellow
Write-Host ""

# Check if Newman is installed
if (-not (Get-Command newman -ErrorAction SilentlyContinue)) {
    Write-Error "Newman not found. Install with: npm install -g newman newman-reporter-htmlextra"
    exit 1
}

# Check collection exists
if (-not (Test-Path $Collection)) {
    Write-Error "Collection not found: $Collection"
    exit 1
}

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Build Newman command
$newmanArgs = @(
    "run", $Collection,
    "--global-var", "baseUrl=$BaseUrl",
    "--global-var", "email=$Email",
    "--global-var", "adminPassword=$Password",
    "--reporters", $Reporters,
    "--reporter-htmlextra-export", "$OutputDir/report.html",
    "--reporter-cli-show-all-iteration", "true",
    "--insecure"
)

if ($Environment) {
    if (Test-Path $Environment) {
        $newmanArgs += "--environment", $Environment
    } else {
        Write-Warning "Environment file not found: $Environment"
    }
}

if ($IterationData) {
    if (Test-Path $IterationData) {
        $newmanArgs += "--iteration-data", $IterationData
    } else {
        Write-Warning "Iteration data file not found: $IterationData"
    }
}

Write-Host "Running: newman $($newmanArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

# Run Newman
$exitCode = 0
try {
    & newman @newmanArgs
    $exitCode = $LASTEXITCODE
} catch {
    $exitCode = 1
    Write-Error "Newman execution failed: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
if ($exitCode -eq 0) {
    Write-Host "  Collection run PASSED" -ForegroundColor Green
} else {
    Write-Host "  Collection run FAILED (exit code: $exitCode)" -ForegroundColor Red
}
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "HTML Report: $OutputDir/report.html" -ForegroundColor Yellow
Write-Host ""

exit $exitCode