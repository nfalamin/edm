# PowerShell script to package browser extensions into store-ready ZIP archives
param(
    [string]$OutputDir = "$PSScriptRoot\store-packages"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$extensions = @(
    @{ Name = "chrome-extension"; ZipName = "edm-chrome-extension-v1.0.0.zip" },
    @{ Name = "edge-extension";   ZipName = "edm-edge-extension-v1.0.0.zip" },
    @{ Name = "firefox-extension"; ZipName = "edm-firefox-extension-v1.0.0.zip" }
)

foreach ($ext in $extensions) {
    $sourceDir = Join-Path $PSScriptRoot $ext.Name
    $zipPath = Join-Path $OutputDir $ext.ZipName

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    Write-Host "Packaging $($ext.Name) -> $zipPath"

    # Exclude native messaging manifests (local registry installer files) and dev files
    $excludePatterns = @("*.json.json", "com.edm.downloader.*.json", "*.map", "*.tmp", "*.log", "*.bak")

    # Temp staging folder for clean compression
    $stageDir = Join-Path $env:TEMP "edm_pack_$($ext.Name)_$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $stageDir | Out-Null

    try {
        Copy-Item -Path "$sourceDir\*" -Destination $stageDir -Recurse -Force

        # Clean up excluded patterns from staging
        foreach ($pattern in $excludePatterns) {
            Get-ChildItem -Path $stageDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
        }

        Compress-Archive -Path "$stageDir\*" -DestinationPath $zipPath -Force
        Write-Host "Successfully created: $zipPath" -ForegroundColor Green
    }
    finally {
        if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
    }
}
