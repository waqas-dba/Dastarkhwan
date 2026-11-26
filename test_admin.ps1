[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body '{"email":"admin@e2e-audit.local","password":"ResetAdminPass123!"}'
$token = $login.accessToken
Write-Host "Token obtained"

# Test admin platform members
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53586/admin/platform/members" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "admin/platform/members: $response"
} catch {
    Write-Host "admin/platform/members error: $($_.Exception.Message)"
    if ($_.ErrorDetails) { Write-Host "Details: $($_.ErrorDetails.Message)" }
}

# Test admin platform roles
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53586/admin/platform/roles" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "admin/platform/roles: $response"
} catch {
    Write-Host "admin/platform/roles error: $($_.Exception.Message)"
}

# Test admin platform tenants
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53586/admin/platform/tenants" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "admin/platform/tenants: $response"
} catch {
    Write-Host "admin/platform/tenants error: $($_.Exception.Message)"
}