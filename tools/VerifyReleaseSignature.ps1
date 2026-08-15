# VerifyReleaseSignature.ps1 - Authenticode Signature Verification Script
param (
    [string]$TargetPath = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\EDM\bin\Release\net10.0-windows\EDM.exe"
)

Write-Host "=== Verifying Authenticode Signature for $TargetPath ===" -ForegroundColor Cyan

if (-not (Test-Path $TargetPath)) {
    Write-Error "Target path does not exist: $TargetPath"
    exit 1
}

$sig = Get-AuthenticodeSignature $TargetPath
if ($sig.Status -eq "Valid") {
    Write-Host "[🟢 VERIFIED] Signature is VALID. Signed by: $($sig.SignerCertificate.Subject)" -ForegroundColor Green
} else {
    Write-Warning "[🔴 NOT VERIFIED] Signature status: $($sig.StatusMessage) (Certificate Required)"
}
