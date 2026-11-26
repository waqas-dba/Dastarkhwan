# Disable SSL certificate validation
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$login = Invoke-RestMethod -Method Post -Uri "https://localhost:53584/identity/auth/login" -Headers @{"Content-Type"="application/json"} -Body '{"email":"admin@e2e-audit.local","password":"ResetAdminPass123!"}'
$token = $login.accessToken
Write-Host "Token: $token"
$response = Invoke-RestMethod -Method Get -Uri "https://localhost:53584/demo/needs-login" -Headers @{"Authorization"="Bearer $token"}
Write-Host "Response: $response"