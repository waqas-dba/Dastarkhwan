#Requires -Version 7.0
param(
    [string]$Collection  = ".\dastarkhwan.postman_collection.json",
    [string]$Environment = ".\dastarkhwan.postman_environment.json",
    [string]$ReportJson  = ".\newman-report.json",
    [string]$ReportHtml  = ".\newman-report.html"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command newman -ErrorAction SilentlyContinue)) {
    Write-Error "newman not installed. Run: npm i -g newman newman-reporter-htmlextra"
    exit 1
}
if (-not (Test-Path $Collection))  { Write-Error "Missing $Collection";  exit 1 }
if (-not (Test-Path $Environment)) { Write-Error "Missing $Environment"; exit 1 }

Write-Host "▶ Running Dastarkhwan Postman suite" -ForegroundColor Cyan
Write-Host "  Collection : $Collection"
Write-Host "  Environment: $Environment"
Write-Host ""

newman run $Collection `
  -e $Environment `
  --reporters cli,json,htmlextra `
  --reporter-json-export $ReportJson `
  --reporter-htmlextra-export $ReportHtml `
  --reporter-htmlextra-title "Dastarkhwan Postman Results" `
  --reporter-htmlextra-browserTitle "Dastarkhwan Postman Results" `
  --reporter-htmlextra-showEnvironmentData `
  --reporter-htmlextra-skipHeaders Authorization,Cookie `
  --delay-request 50 `
  --timeout-request 15000 `
  --timeout-script 5000 `
  --bail `
  --color on

$exit = $LASTEXITCODE
Write-Host ""
if ($exit -eq 0) {
    Write-Host "✔ All tests passed. HTML report: $ReportHtml" -ForegroundColor Green
} else {
    Write-Host "✖ Failures detected. See: $ReportHtml" -ForegroundColor Red
}
exit $exit