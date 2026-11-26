
$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body (@{email="admin@e2e-audit.local";password=$env:TEST_PASSWORD} | ConvertTo-Json -Compress)
$token = $login.accessToken
Write-Host "Token obtained"

# Test demo/sale (requires sales.create permission)
try {
    $response = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/demo/sale" -Headers @{"Authorization"="Bearer $token"; "X-Tenant-Id"="29b5e251-9efe-48b5-8fb3-cc61629b883f"}
    Write-Host "demo/sale: $response"
} catch {
    Write-Host "demo/sale error: $($_.Exception.Message)"
}

# Test demo/refund (requires sales.refund permission)
try {
    $response = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/demo/refund" -Headers @{"Authorization"="Bearer $token"; "X-Tenant-Id"="29b5e251-9efe-48b5-8fb3-cc61629b883f"}
    Write-Host "demo/refund: $response"
} catch {
    Write-Host "demo/refund error: $($_.Exception.Message)"
}

# Test identity/me
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/identity/auth/me" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "identity/auth/me: $response"
} catch {
    Write-Host "identity/auth/me error: $($_.Exception.Message)"
}

# Test admin platform members (if endpoint exists)
try {
    $response = Invoke-RestMethod -Method Get -Uri "https://localhost:53586/admin/platform/members" -Headers @{"Authorization"="Bearer $token"}
    Write-Host "admin/platform/members: $response"
} catch {
    Write-Host "admin/platform/members error: $($_.Exception.Message)"
}