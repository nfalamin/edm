# Exclusive Download Manager (EDM) - Installer Build Guide

This guide explains how to compile the production Windows Installer for Exclusive Download Manager (`EDMSetup.exe`).

---

## Prerequisites

1. **Inno Setup 6+**: Download and install from [jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php).
   Ensure `ISCC.exe` is in your system `PATH` (typically `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`).
2. **.NET SDK 10.0**: Installed and available for compiling WPF binaries.

---

## Building the Installer

### Step 1: Compile Release Binaries

From the repository root directory, run:

```powershell
dotnet publish EDM\EDM.csproj -c Release -r win-x64 --self-contained false
```

This generates executable files and assets in `EDM\bin\Release\net10.0-windows\`.

---

### Step 2: Compile Installer Script

Run the Inno Setup compiler on `EDMSetup.iss`:

```powershell
iscc tools\installer\EDMSetup.iss
```

Or from PowerShell if `ISCC.exe` is not in `PATH`:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" tools\installer\EDMSetup.iss
```

The compiled installer will be saved to:
`tools\installer\Output\EDMSetup.exe`

---

## Installer Features

- **Standard Installation Path**: `{autopf}\Exclusive Download Manager` (`C:\Program Files\Exclusive Download Manager`).
- **Browser Native Messaging Host Registration**:
  - Automatically registers native messaging host manifests in Windows Registry for:
    - **Google Chrome**: `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader`
    - **Microsoft Edge**: `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader`
    - **Mozilla Firefox**: `HKCU\Software\Mozilla\NativeMessagingHosts\com.edm.downloader`
- **Protocol & File Association**:
  - Registers `edm://` custom protocol handler for web browser integration.
  - Registers `.edm` batch download file container association.
- **Shortcuts**: Creates Start Menu folder and optional Desktop shortcut.
- **Uninstaller & Cleanup**:
  - Cleanly unregisters native messaging host registry keys.
  - Interactive prompt asking whether to retain or delete user settings, history DB, and quarantined files located in `%USERPROFILE%\EDM`.
