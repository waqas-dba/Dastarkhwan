<#
.SYNOPSIS
    Dastarkhwan Postman CI Test Runner (PowerShell)
    Runs critical tests for CI/CD pipeline

.USAGE
    .\run-postman-ci.ps1 [-BaseUrl <url>] [-Email <email>] [-Password <password>]
#>

param(
    [string]$BaseUrl = "https://localhost:53584",
    [string]$Email = "admin@example.com",
    [string]$Password = "a-long-test-password"
)

$ErrorActionPreference = "Stop"

$collection = "Dastarkhwan.postman_collection.json"
$environment = "Dastarkhwan.postman_environment.json"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Dastarkhwan Postman CI Tests" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Check if service is up
Write-Host "Checking service availability..." -ForegroundColor Yellow
$maxAttempts = 30
$serviceUp = $false

for ($i = 1; $i -le $maxAttempts; $i++) {
    try {
        $response = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/live" -SkipCertificateCheck -ErrorAction Stop
        if ($response.status -eq "live") {
            $serviceUp = $true
            Write-Host "Service is live!" -ForegroundColor Green
            break
        }
    } catch {
        Write-Host "Attempt $i/$maxAttempts: waiting..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $serviceUp) {
    Write-Error "Service not available after $maxAttempts attempts"
    exit 1
}

# Run critical tests
Write-Host "Running critical test folders..." -ForegroundColor Yellow

$newmanArgs = @(
    "run", $collection,
    "--environment", $environment,
    "--global-var", "baseUrl=$BaseUrl",
    "--global-var", "email=$Email",
    "--global-var", "adminPassword=$Password",
    "--insecure",
    "--reporters", "cli,junit",
    "--reporter-junit-export", "newman-results.xml",
    "--folder", "Health",
    "--folder", "Identity > Auth",
    "--folder", "Tenants > Current User Tenants",
    "--folder", "Access > My Access"
)

Write-Host "Running: newman $($newmanArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

$exitCode = 0
try {
    & newman @newmanArgs
    $exitCode = $LASTEXITCODE
} catch {
    $exitCode = 1
    Write-Error "Newman execution failed: $($_.Exception.Message)"
}

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "CI Tests PASSED" -ForegroundColor Green
} else {
    Write-Host "CI Tests FAILED" -ForegroundColor Red
}

exit $exitCode