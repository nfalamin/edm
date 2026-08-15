# CreateLocalSigningCert.ps1 - Generate local developer Authenticode Code Signing Certificate
$ErrorActionPreference = "Stop"

Write-Host "=== Creating Local Authenticode Code Signing Certificate ===" -ForegroundColor Cyan

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=Exclusive Download Manager, O=EDM Project" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)

# Import into Trusted Root Certification Authorities so Windows trusts signature
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$rootStore.Add($cert)
$rootStore.Close()

Write-Host "[🟢 SUCCESS] Created & Trusted Code Signing Certificate:" -ForegroundColor Green
Write-Host "Subject:    $($cert.Subject)"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Expires:    $($cert.NotAfter)"
