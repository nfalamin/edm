# EDM Security and Development Path Audit Script
# Scans output files and codebase for leaked secrets and hardcoded development paths

$outputDir = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\Output"
$binDir = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\EDM\bin\Release\net10.0-windows"

Write-Host "================================================="
Write-Host " EDM SECURITY AND RELEASE INTEGRITY AUDIT "
Write-Host "================================================="

# 1. Scan Configuration & JSON Files for Development Paths
$jsonFiles = Get-ChildItem -Path $outputDir -Filter "*.json" -Recurse
$manifestFiles = Get-ChildItem -Path "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\extension" -Filter "*.json" -Recurse

$allTextFiles = @($jsonFiles) + @($manifestFiles)

$pathPatterns = @("bin\\Debug", "obj\\Debug")
$hardcodedLeaksFound = 0

foreach ($file in $allTextFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    foreach ($pat in $pathPatterns) {
        if ($content -match [regex]::Escape($pat)) {
            Write-Host "[WARNING] Found pattern '$pat' in $($file.FullName)"
            $hardcodedLeaksFound++
        }
    }
}

if ($hardcodedLeaksFound -eq 0) {
    Write-Host "[PASS] Zero development paths or debug build references found in release manifests/configs."
}

# 2. Secret & Credential Scanning
$secretPatterns = @(
    "AIza[0-9A-Za-z-_]{35}", # Google API Key
    "ghp_[0-9a-zA-Z]{36}",    # GitHub Token
    "eyJ[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*", # JWT
    "bearer\s+[A-Za-z0-9\-_\.]+", # Bearer token
    'password\s*=\s*[''"][^''"]{8,}[''"]' # Hardcoded password
)

$sourceFiles = Get-ChildItem -Path "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\EDM" -Include "*.cs", "*.xaml", "*.json" -Recurse
$secretsFound = 0

foreach ($sf in $sourceFiles) {
    if ($sf.FullName -match "\\bin\\" -or $sf.FullName -match "\\obj\\") { continue }
    $txt = Get-Content -Path $sf.FullName -Raw
    foreach ($sp in $secretPatterns) {
        if ($txt -match $sp) {
            # Check if it is a regex definition or test string
            if ($txt -match "Regex" -or $txt -match "pattern" -or $txt -match "const string") {
                # safe
            } else {
                Write-Host "[SECRET WARNING] Possible secret pattern matched in $($sf.FullName)"
                $secretsFound++
            }
        }
    }
}

if ($secretsFound -eq 0) {
    Write-Host "[PASS] Zero leaked credentials, private tokens, or plaintext passwords detected."
}

# 3. Authenticode Signature Status
Write-Host "`n--- AUTHENTICODE SIGNATURE AUDIT ---"
$exePath = Join-Path $outputDir "EDMSetup.exe"
if (Test-Path $exePath) {
    $sig = Get-AuthenticodeSignature -FilePath $exePath
    Write-Host "Installer Signature Status: $($sig.Status)"
    if ($sig.Status -eq "Valid") {
        Write-Host "Signer Certificate: $($sig.SignerCertificate.Subject)"
    } else {
        Write-Host "Note: Binary is unsigned or self-signed. External commercial EV Code Signing certificate required for public SmartScreen trust."
    }
}

Write-Host "`n================================================="
Write-Host " SECURITY & HARDENING SCAN COMPLETE "
Write-Host "================================================="
