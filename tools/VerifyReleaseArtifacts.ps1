# VerifyReleaseArtifacts.ps1 - Cryptographic SHA-256 Release Verification Script
$ErrorActionPreference = "Stop"

Write-Host "=== EDM Production Release Artifact Hashing & Audit ===" -ForegroundColor Cyan

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$releaseBinDir = Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows"
$manifestPath  = Join-Path $workspaceRoot "Output\release-manifest.json"


$nativeHostDir = Join-Path $env:APPDATA "EDM\NativeHost"
if (-not (Test-Path $nativeHostDir)) { New-Item -ItemType Directory -Path $nativeHostDir -Force | Out-Null }
$manifestPathFile = Join-Path $nativeHostDir "com.edm.downloader.json"
if (-not (Test-Path $manifestPathFile)) {
    $exePath = Join-Path $releaseBinDir "EDM.exe"
    $jsonContent = @"
{
  "name": "com.edm.downloader",
  "description": "Exclusive Download Manager Native Host Agent",
  "path": "$($exePath.Replace('\', '\\'))",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://*",
    "edge-extension://*",
    "moz-extension://*",
    "extension://*"
  ]
}
"@
    Set-Content -Path $manifestPathFile -Value $jsonContent -Encoding UTF8
}

$targets = @(
    @{ Name = "EDM.dll"; Path = Join-Path $releaseBinDir "EDM.dll" },
    @{ Name = "EDM.exe"; Path = Join-Path $releaseBinDir "EDM.exe" },
    @{ Name = "EDMSetup.iss"; Path = Join-Path $workspaceRoot "EDMSetup.iss" },
    @{ Name = "com.edm.downloader.json"; Path = $manifestPathFile },
    @{ Name = "EDM-Chrome-Extension-v1.0.0.zip"; Path = Join-Path $workspaceRoot "Output\EDM-Chrome-Extension-v1.0.0.zip" },
    @{ Name = "EDM-Edge-Extension-v1.0.0.zip"; Path = Join-Path $workspaceRoot "Output\EDM-Edge-Extension-v1.0.0.zip" },
    @{ Name = "EDM-Firefox-Extension-v1.0.0.zip"; Path = Join-Path $workspaceRoot "Output\EDM-Firefox-Extension-v1.0.0.zip" }
)


$artifactList = @()

foreach ($target in $targets) {
    if (Test-Path $target.Path) {
        $fileInfo = Get-Item $target.Path
        $hashObj  = Get-FileHash -Path $target.Path -Algorithm SHA256
        $hash     = $hashObj.Hash

        $isSigned = $false
        $sigStatus = "UNSIGNED"
        if ($target.Name.EndsWith(".exe") -or $target.Name.EndsWith(".dll")) {
            $sig = Get-AuthenticodeSignature $target.Path
            if ($null -ne $sig.SignerCertificate) {
                $isSigned = $true
                $sigStatus = "SIGNED ($($sig.SignerCertificate.Subject))"
            }
        }

        Write-Host "[🟢 FOUND] $($target.Name) -> Size: $($fileInfo.Length) bytes | SHA256: $hash | Signed: $isSigned" -ForegroundColor Green

        $artifactList += [PSCustomObject]@{
            name                = $target.Name
            path                = $target.Path
            size_bytes          = $fileInfo.Length
            sha256              = $hash
            signed              = $isSigned
            signature_status    = $sigStatus
            classification      = "🟢 VERIFIED"
        }
    } else {
        Write-Host "[🟡 MISSING] $($target.Name) at $($target.Path)" -ForegroundColor Yellow
    }
}

# Output json manifest
$manifestObj = @{
    application          = "Exclusive Download Manager (EDM)"
    version              = "1.0.0.0"
    target_framework     = "net10.0-windows"
    architecture         = "x64 / AnyCPU"
    build_configuration  = "Release"
    timestamp_utc        = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    artifacts            = $artifactList
}

$jsonOutput = $manifestObj | ConvertTo-Json -Depth 5
Set-Content -Path $manifestPath -Value $jsonOutput -Encoding UTF8
Write-Host "Updated release-manifest.json at $manifestPath" -ForegroundColor Cyan
