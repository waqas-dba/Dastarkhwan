$Root = Get-Location

$ExportFolder = Join-Path $Root "CodeExports"

# Create export folder
New-Item -ItemType Directory -Force -Path $ExportFolder | Out-Null

Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {

    $ProjectDir  = $_.Directory.FullName
    $ProjectName = $_.BaseName

    $ProjectFile = Join-Path $ExportFolder "$ProjectName.txt"

    # Reset file
    Set-Content -Path $ProjectFile -Value "" -Encoding UTF8

    Write-Host ""
    Write-Host "Processing Project: $ProjectName" -ForegroundColor Cyan

    $Files = Get-ChildItem $ProjectDir -Recurse -File -Filter *.cs |
    Where-Object {

        $_.FullName -notmatch '\\bin\\' -and
        $_.FullName -notmatch '\\obj\\' -and
        $_.FullName -notmatch '\\\.vs\\' -and
        $_.FullName -notmatch '\\node_modules\\' -and
        $_.FullName -notmatch '\\\.git\\' -and
        $_.FullName -notmatch '\\Migrations\\'

    }

    foreach ($File in $Files) {

        Write-Host "  Exporting: $($File.Name)" -ForegroundColor Yellow

        $Header = @"

==================================================
FILE: $($File.FullName)
==================================================

"@

        $Content = Get-Content $File.FullName -Raw

        Add-Content -Path $ProjectFile -Value $Header
        Add-Content -Path $ProjectFile -Value $Content
        Add-Content -Path $ProjectFile -Value "`r`n"
    }

    Write-Host "Saved: $ProjectFile" -ForegroundColor Green
}

Write-Host ""
Write-Host "DONE. Separate project export files created." -ForegroundColor Magenta