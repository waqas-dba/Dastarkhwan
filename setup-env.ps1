<#
.SYNOPSIS
    Dastarkhwan Environment Setup Script
    Configures user secrets, applies migrations, seeds admin user, and verifies the API.

.DESCRIPTION
    This script sets up the complete development environment for Dastarkhwan:
    - Configures user secrets for Identity, JWT, and Database
    - Applies EF Core migrations for Identity, Tenant, and Access modules
    - Seeds the platform admin user
    - Runs health checks and basic API verification

.NOTES
    Run this script from the repository root (where Dastarkhwan.Web.csproj is located).
    Requires: .NET 10 SDK, PostgreSQL running on localhost:5432
#>

param(
    [string]$ProjectPath = "Dastarkhwan.Web",
    [string]$AdminEmail = "admin@example.com",
    [string]$AdminPassword = "a-long-test-password",
    [string]$AdminMobile = "+923000000000",
    [string]$AdminDisplayName = "Platform Administrator",
    [string]$JwtSigningKey = "K3WFXWfpftOu9gxRZYVX774n7qPR44pQ+ERsnmvFV3M=",
    [string]$JwtIssuer = "Dastarkhwan",
    [string]$JwtAudience = "Dastarkhwan",
    [string]$DbConnectionString = "Host=127.0.0.1;Database=dastarkhwan_db;Username=dastarkhwan;Password=dastarkhwan_dev_only",
    [string]$BaseUrl = "https://localhost:53584"
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Dastarkhwan Environment Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
Write-Host "  .NET SDK: $dotnetVersion" -ForegroundColor Green

try {
    $pgVersion = psql --version 2>$null
    Write-Host "  PostgreSQL: $pgVersion" -ForegroundColor Green
} catch {
    Write-Warning "  PostgreSQL not found in PATH. Ensure it's running on localhost:5432"
}

Write-Host ""

# Navigate to project directory
$projectDir = Join-Path $PWD $ProjectPath
if (-not (Test-Path $projectDir)) {
    throw "Project directory not found: $projectDir"
}
Set-Location $projectDir
Write-Host "Working in: $projectDir" -ForegroundColor Green
Write-Host ""

# 1. Clear existing user secrets (optional - comment out to preserve)
Write-Host "1. Setting up User Secrets..." -ForegroundColor Yellow
$secrets = @{
    "Identity:Seed:AdminEmail" = $AdminEmail
    "Identity:Seed:AdminPassword" = $AdminPassword
    "Identity:Seed:AdminMobile" = $AdminMobile
    "Identity:Seed:AdminDisplayName" = $AdminDisplayName
    "Identity:Jwt:SigningKey" = $JwtSigningKey
    "Identity:Jwt:Issuer" = $JwtIssuer
    "Identity:Jwt:Audience" = $JwtAudience
    "ConnectionStrings:Default" = $DbConnectionString
}

foreach ($key in $secrets.Keys) {
    $value = $secrets[$key]
    dotnet user-secrets set $key $value | Out-Null
    Write-Host "  Set: $key" -ForegroundColor Green
}
Write-Host ""

# 2. Build the project
Write-Host "2. Building project..." -ForegroundColor Yellow
dotnet build --no-restore
Write-Host "  Build successful" -ForegroundColor Green
Write-Host ""

# 3. Apply migrations
Write-Host "3. Applying EF Core migrations..." -ForegroundColor Yellow

$contexts = @("IdentityDbContext", "TenantDbContext", "AccessDbContext")
foreach ($context in $contexts) {
    Write-Host "  Migrating $context..." -ForegroundColor Cyan
    dotnet ef database update --context $context --no-build 2>&1 | ForEach-Object { Write-Host "    $_" }
    Write-Host "  $context: Done" -ForegroundColor Green
}
Write-Host ""

# 4. Run the application to seed admin and verify
Write-Host "4. Starting application for seeding and verification..." -ForegroundColor Yellow
Write-Host "  This will run migrations, seed admin, and start the web server." -ForegroundColor Gray
Write-Host "  Press Ctrl+C to stop after verification." -ForegroundColor Gray
Write-Host ""

# Start the app in background
$appProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --no-build" -PassThru -WindowStyle Hidden
Write-Host "  Application started (PID: $($appProcess.Id))" -ForegroundColor Green

# Wait for app to be ready
Write-Host "  Waiting for application to be ready..."
$maxWait = 60
$waited = 0
$ready = $false

while ($waited -lt $maxWait -and -not $ready) {
    try {
        $response = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/live" -SkipCertificateCheck -ErrorAction Stop
        if ($response.status -eq "live") {
            $ready = $true
            Write-Host "  Application is live!" -ForegroundColor Green
        }
    } catch {
        Start-Sleep -Seconds 2
        $waited += 2
        Write-Host "  ... waiting ($waited s)" -ForegroundColor Gray
    }
}

if (-not $ready) {
    Write-Error "Application did not become ready within $maxWait seconds"
    Stop-Process -Id $appProcess.Id -Force
    exit 1
}

Write-Host ""

# 5. Verify API endpoints
Write-Host "5. Verifying API endpoints..." -ForegroundColor Yellow

# Health checks
Write-Host "  Health checks:" -ForegroundColor Cyan
$healthLive = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/live" -SkipCertificateCheck
Write-Host "    /health/live: $($healthLive.status)" -ForegroundColor Green

$healthReady = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/ready" -SkipCertificateCheck
Write-Host "    /health/ready: $($healthReady.status)" -ForegroundColor Green

# Login as admin
Write-Host "  Authentication:" -ForegroundColor Cyan
$loginBody = @{ email = $AdminEmail; password = $AdminPassword } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body $loginBody -SkipCertificateCheck
$accessToken = $login.accessToken
$refreshToken = $login.refreshToken
Write-Host "    Login: OK (token acquired)" -ForegroundColor Green

# Get current user
$me = Invoke-RestMethod -Method Get -Uri "$BaseUrl/identity/auth/me" -Headers @{"Authorization"="Bearer $accessToken"} -SkipCertificateCheck
Write-Host "    /identity/auth/me: $($me.email) (ID: $($me.id))" -ForegroundColor Green

# Access/me (platform permissions)
$accessMe = Invoke-RestMethod -Method Get -Uri "$BaseUrl/access/me" -Headers @{"Authorization"="Bearer $accessToken"} -SkipCertificateCheck
Write-Host "    /access/me: $($accessMe.permissions.Count) permissions" -ForegroundColor Green

# Tenants/me (user's tenants)
$tenantsMe = Invoke-RestMethod -Method Get -Uri "$BaseUrl/tenants/me" -Headers @{"Authorization"="Bearer $accessToken"} -SkipCertificateCheck
Write-Host "    /tenants/me: $($tenantsMe.Count) tenant(s)" -ForegroundColor Green

# Access/roles
$roles = Invoke-RestMethod -Method Get -Uri "$BaseUrl/access/roles" -Headers @{"Authorization"="Bearer $accessToken"} -SkipCertificateCheck
Write-Host "    /access/roles: $($roles.Count) role(s)" -ForegroundColor Green

# Access/permissions
$perms = Invoke-RestMethod -Method Get -Uri "$BaseUrl/access/permissions" -Headers @{"Authorization"="Bearer $accessToken"} -SkipCertificateCheck
Write-Host "    /access/permissions: $($perms.Count) permission(s)" -ForegroundColor Green

Write-Host ""

# 6. Create a test tenant (optional)
Write-Host "6. Creating test tenant..." -ForegroundColor Yellow
$tenantBody = @{ name = "Test Tenant"; slug = "test-tenant"; activateImmediately = $true; ownerUserId = $me.id } | ConvertTo-Json -Compress
try {
    $tenant = Invoke-RestMethod -Method Post -Uri "$BaseUrl/tenants" -Headers @{"Content-Type"="application/json"; "Authorization"="Bearer $accessToken"} -Body $tenantBody -SkipCertificateCheck
    Write-Host "    Created tenant: $($tenant.name) (ID: $($tenant.id))" -ForegroundColor Green
    
    # Test tenant-scoped endpoints
    $tenantId = $tenant.id
    $tenantCurrent = Invoke-RestMethod -Method Get -Uri "$BaseUrl/tenants/current" -Headers @{"Authorization"="Bearer $accessToken"; "X-Tenant-Id"=$tenantId} -SkipCertificateCheck
    Write-Host "    /tenants/current (with X-Tenant-Id): $($tenantCurrent.name)" -ForegroundColor Green
} catch {
    Write-Warning "    Tenant creation failed (may already exist): $($_.Exception.Message)"
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Environment Setup Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host "Admin Email: $AdminEmail" -ForegroundColor Yellow
Write-Host "Admin Password: $AdminPassword" -ForegroundColor Yellow
Write-Host ""
Write-Host "Access Token (for Postman/scripts):" -ForegroundColor Yellow
Write-Host $accessToken -ForegroundColor White
Write-Host ""
Write-Host "Import Dastarkhwan.postman_collection.json into Postman" -ForegroundColor Cyan
Write-Host "Set baseUrl variable to: $BaseUrl" -ForegroundColor Cyan
Write-Host "Set adminPassword variable to: $AdminPassword" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application is running in background (PID: $($appProcess.Id))" -ForegroundColor Gray
Write-Host "Press Ctrl+C to stop it when done testing." -ForegroundColor Gray

# Keep script running so app stays up
try {
    $appProcess.WaitForExit()
} catch {
    Stop-Process -Id $appProcess.Id -Force
}