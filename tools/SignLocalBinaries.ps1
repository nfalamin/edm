# SignLocalBinaries.ps1 - Sign EDM binaries with local code-signing cert
$ErrorActionPreference = "Stop"

Write-Host "=== Signing EDM Production Binaries ===" -ForegroundColor Cyan

$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1

if ($null -eq $cert) {
    Write-Error "No Code Signing Certificate found in Cert:\CurrentUser\My"
    exit 1
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$binaries = @(
    (Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows\EDM.exe"),
    (Join-Path $workspaceRoot "EDM\bin\Release\net10.0-windows\EDM.dll"),
    (Join-Path $workspaceRoot "EDM.NativeHost\bin\Release\net10.0-windows\EDM.NativeHost.exe"),
    (Join-Path $workspaceRoot "EDM.NativeHost\bin\Release\net10.0-windows\EDM.NativeHost.dll")
)

foreach ($binary in $binaries) {
    if (Test-Path $binary) {
        $sig = Set-AuthenticodeSignature -FilePath $binary -Certificate $cert
        Write-Host "[🟢 SIGNED] $binary -> Status: $($sig.StatusMessage)" -ForegroundColor Green
    }
}
