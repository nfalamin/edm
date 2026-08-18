Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "    EXCLUSIVE DOWNLOAD MANAGER (EDM) v1.0 - ANTI-TAMPER INTEGRITY CHECK" -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Calculating SHA-256 hash of EDM_Setup_v1.0.exe..." -ForegroundColor White
Write-Host ""

$19070B09A3B4A4DF709617E0DCC56000D128B6FF6CA4DD695439D2961902E6882"
$installerPath = Join-Path $PSScriptRoot "EDM_Setup_v1.0.exe"

if (Test-Path $installerPath) {
    $actualHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash
    Write-Host "Expected Hash: $expectedHash" -ForegroundColor DarkGray
    Write-Host "Actual Hash:   $actualHash" -ForegroundColor DarkGray
    Write-Host ""

    if ($actualHash -eq $expectedHash) {
        Write-Host "[PASS] VERIFICATION SUCCESSFUL!" -ForegroundColor Green
        Write-Host "       The installer is 100% GENUINE, OFFICIAL, and UNMODIFIED." -ForegroundColor Green
        Write-Host "       No malware injection, corruption, or tampering detected." -ForegroundColor Green
    } else {
        Write-Host "[FAIL] VERIFICATION FAILED!" -ForegroundColor Red
        Write-Host "       The installer hash does NOT match the official release!" -ForegroundColor Red
        Write-Host "       WARNING: The file may be corrupt or modified by a third party." -ForegroundColor Yellow
    }
} else {
    Write-Host "[ERROR] EDM_Setup_v1.0.exe was not found in: $installerPath" -ForegroundColor Red
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Read-Host "Press Enter to exit..."
