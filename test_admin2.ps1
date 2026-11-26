
$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body (@{email="admin@e2e-audit.local";password=$env:TEST_PASSWORD} | ConvertTo-Json -Compress)
$token = $login.accessToken
Write-Host "Token obtained"

# Test admin health ready
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53586/admin/health/ready" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "admin/health/ready: $response"
} catch {
    Write-Host "admin/health/ready error: $($_.Exception.Message)"
    if ($_.ErrorDetails) { Write-Host "Details: $($_.ErrorDetails.Message)" }
}

# Test identity/me on Web host
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/identity/auth/me" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "identity/auth/me: $response"
} catch {
    Write-Host "identity/auth/me error: $($_.Exception.Message)"
    if ($_.ErrorDetails) { Write-Host "Details: $($_.ErrorDetails.Message)" }
}