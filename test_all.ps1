[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body '{"email":"admin@e2e-audit.local","password":"ResetAdminPass123!"}'
$token = $login.accessToken
Write-Host "Token: $token"

# Test demo/needs-tenant with X-Tenant-Id header
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/demo/needs-tenant" -Headers @{"Authorization"="Bearer $token"; "X-Tenant-Id"="29b5e251-9efe-48b5-8fb3-cc61629b883f"}
Write-Host "demo/needs-tenant: $response"

# Test tenants/current (should use tenant_id claim from token)
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/tenants/current" -Headers @{"Authorization"="Bearer $token"}
Write-Host "tenants/current: $response"

# Test tenants/me
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/tenants/me" -Headers @{"Authorization"="Bearer $token"}
Write-Host "tenants/me: $response"

# Test access/me
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/access/me" -Headers @{"Authorization"="Bearer $token"}
Write-Host "access/me: $response"

# Test access/roles
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/access/roles" -Headers @{"Authorization"="Bearer $token"}
Write-Host "access/roles: $response"

# Test access/permissions
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/access/permissions" -Headers @{"Authorization"="Bearer $token"}
Write-Host "access/permissions: $response"