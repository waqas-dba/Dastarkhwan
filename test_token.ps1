# Disable SSL certificate validation

$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body (@{email="admin@e2e-audit.local";password=$env:TEST_PASSWORD} | ConvertTo-Json -Compress)
$token = $login.accessToken
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/demo/needs-login" -Headers @{"Authorization"="Bearer $token"}
Write-Host "Response: $response"