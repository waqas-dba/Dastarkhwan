
$Root = Get-Location

$ExportFolder = Join-Path $Root "CodeExports"
$ChunkFolder = Join-Path $ExportFolder "Chunks"

New-Item -ItemType Directory -Force -Path $ExportFolder | Out-Null
New-Item -ItemType Directory -Force -Path $ChunkFolder | Out-Null

# ===== OUTPUT FILES =====
$AllFile = Join-Path $ExportFolder "ALL_CODE.txt"
"" | Out-File $AllFile -Encoding utf8 -Force

# ===== CHUNK SETTINGS =====
$ChunkSize = 60000   # change to 60000 for DeepSeek
$ChunkIndex = 1
$CurrentSize = 0
$CurrentChunkFile = Join-Path $ChunkFolder "Chunk_$ChunkIndex.txt"
"" | Out-File $CurrentChunkFile -Encoding utf8 -Force

# ===== CLEAN CODE FUNCTION =====
function Clean-Code {
    param ([string]$content)

    # remove single-line comments
    $content = $content -replace '\/\/.*', ''

    # remove block comments
    $content = $content -replace '\/\*[\s\S]*?\*\/', ''

    # remove empty lines + trim
    $lines = $content -split "`r?`n" |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" }

    return ($lines -join "`n")
}

# ===== CHUNK WRITER =====
function Write-Chunks {
    param ([string]$text)

    $global:CurrentSize += $text.Length

    if ($global:CurrentSize -gt $ChunkSize) {
        $global:ChunkIndex++
        $global:CurrentChunkFile = Join-Path $ChunkFolder "Chunk_$global:ChunkIndex.txt"
        "" | Out-File $global:CurrentChunkFile -Encoding utf8 -Force
        $global:CurrentSize = 0
    }

    Add-Content $global:CurrentChunkFile $text
}

# ===== GET PROJECTS (EXCLUDE TESTS) =====
$Projects = Get-ChildItem -Recurse -Filter *.csproj |
    Where-Object {
        $_.BaseName -notmatch '(?i)test' -and
        $_.FullName -notmatch '\\bin\\' -and
        $_.FullName -notmatch '\\obj\\' -and
        $_.FullName -notmatch '\\.vs\\'
    }

foreach ($Project in $Projects) {

    $ProjectDir  = $Project.Directory.FullName
    $ProjectName = $Project.BaseName

    Write-Host "Processing: $ProjectName"

    $Files = Get-ChildItem $ProjectDir -Recurse -File -Include *.cs |
        Where-Object {
            $_.FullName -notmatch '\\bin\\' -and
            $_.FullName -notmatch '\\obj\\' -and
            $_.FullName -notmatch '\\.vs\\' -and
            $_.FullName -notmatch '\\node_modules\\' -and
            $_.FullName -notmatch '\\.git\\' -and
            $_.FullName -notmatch '\\Migrations\\'
        }

    foreach ($File in $Files) {

        $raw = Get-Content $File.FullName -Raw
        $clean = Clean-Code $raw

        $Header = "===== $ProjectName | $($File.FullName) =====`n"
        $block = $Header + $clean + "`n"

        # ===== GLOBAL FILE =====
        Add-Content $AllFile $block

        # ===== CHUNK FILES =====
        Write-Chunks $block
    }

    Write-Host "Saved: $ProjectName"
}

Write-Host "DONE ✅ AI export completed (clean + chunked + test excluded)"