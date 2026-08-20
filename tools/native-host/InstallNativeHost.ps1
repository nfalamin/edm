# InstallNativeHost.ps1 - Registers EDM Native Messaging Host in Windows Registry for Chrome, Edge & Firefox

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolsDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $ToolsDir

$ExePath = Join-Path $RepoRoot "EDM.NativeHost\bin\Debug\net10.0-windows\EDM.NativeHost.exe"
if (-not (Test-Path $ExePath)) {
    $ExePath = Join-Path $RepoRoot "EDM\bin\Debug\net10.0-windows\EDM.exe"
}

$ManifestPath = Join-Path $ScriptDir "com.edm.downloader.json"
$FirefoxManifestPath = Join-Path $ScriptDir "com.edm.downloader.firefox.json"

Write-Host "Registering EDM Native Messaging Host..." -ForegroundColor Cyan

# 1. Update Chromium manifest JSON path to point to absolute EDM.NativeHost.exe path
$ManifestJson = @{
    name = "com.edm.downloader"
    description = "Exclusive Download Manager Native Host Messaging Agent"
    path = $ExePath
    type = "stdio"
    allowed_origins = @(
        "chrome-extension://knldjmfmopnpolahpmmgbagdohdnhkda/",
        "chrome-extension://fgnkgamjcmfccjmkifdhipjgnagfgioe/",
        "chrome-extension://*",
        "extension://*"
    )
} | ConvertTo-Json -Depth 4

Set-Content -Path $ManifestPath -Value $ManifestJson -Encoding UTF8
Write-Host "[OK] Updated Chromium Native Host Manifest at $ManifestPath" -ForegroundColor Green

# 2. Update Firefox manifest JSON path
$FirefoxManifestJson = @{
    name = "com.edm.downloader"
    description = "Exclusive Download Manager Native Host Messaging Agent"
    path = $ExePath
    type = "stdio"
    allowed_extensions = @(
        "edm-extension@edm.app",
        "edm@exclusive-download-manager.com"
    )
} | ConvertTo-Json -Depth 4

Set-Content -Path $FirefoxManifestPath -Value $FirefoxManifestJson -Encoding UTF8
Write-Host "[OK] Updated Firefox Native Host Manifest at $FirefoxManifestPath" -ForegroundColor Green

# 3. Register in Windows Registry for Chrome
$ChromeRegPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"
if (-not (Test-Path $ChromeRegPath)) {
    New-Item -Path $ChromeRegPath -Force | Out-Null
}
Set-ItemProperty -Path $ChromeRegPath -Name "(default)" -Value $ManifestPath
Write-Host "[OK] Registered Native Host for Google Chrome" -ForegroundColor Green

# 4. Register in Windows Registry for Edge
$EdgeRegPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader"
if (-not (Test-Path $EdgeRegPath)) {
    New-Item -Path $EdgeRegPath -Force | Out-Null
}
Set-ItemProperty -Path $EdgeRegPath -Name "(default)" -Value $ManifestPath
Write-Host "[OK] Registered Native Host for Microsoft Edge" -ForegroundColor Green

# 5. Register in Windows Registry for Firefox
$FirefoxRegPath = "HKCU:\Software\Mozilla\NativeMessagingHosts\com.edm.downloader"
if (-not (Test-Path $FirefoxRegPath)) {
    New-Item -Path $FirefoxRegPath -Force | Out-Null
}
Set-ItemProperty -Path $FirefoxRegPath -Name "(default)" -Value $FirefoxManifestPath
Write-Host "[OK] Registered Native Host for Mozilla Firefox" -ForegroundColor Green

Write-Host "`nSUCCESS: EDM Extension Native Messaging Host successfully installed for Chrome, Edge, and Firefox!" -ForegroundColor Cyan
