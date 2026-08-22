# EDM Installer & Context Menu Integration

## Building the Installer

To create the EDMSetup executable, you need Inno Setup 6.0 or later.

### Prerequisites
1. Install Inno Setup from: https://jrsoftware.org/isdl.php
2. Build EDM in Release mode:
   ```
   dotnet build -c Release
   ```

### Creating the Installer
1. Open `EDMSetup.iss` in Inno Setup IDE
2. Click "Build" → "Compile"
3. The installer will be created in `.\Output\EDMSetup.exe`

Alternatively, from PowerShell:
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" EDMSetup.iss
```

## Features

### 1. Installer Registration
During installation, users can choose to:
- **Register Context Menu**: Automatically adds "Download with EDM" to Windows Explorer right-click menu for:
  - All files (`*`)
  - HTTP URLs (`http://`)
  - HTTPS URLs (`https://`)

The installer requires administrator privileges to modify registry.

### 2. Runtime Toggle
Users can enable/disable the context menu at any time from Settings:
1. Open EDM → Settings
2. Scroll to "Windows Explorer Integration" section
3. Click "Enable" or "Disable" button
4. Confirm in the dialog (requires admin privileges)

### 3. Registry Entries
When registered, the following registry keys are created:
```
HKEY_CURRENT_USER\Software\Classes\*\shell\DownloadWithEDM
HKEY_CURRENT_USER\Software\Classes\http\shell\DownloadWithEDM
HKEY_CURRENT_USER\Software\Classes\https\shell\DownloadWithEDM
```

Each entry includes:
- Label: "Download with EDM"
- Icon: EDM.exe application icon
- Command: `EDM.exe "%1"` (passes selected file/URL as argument)

### 4. Uninstall
When uninstalling, all registry entries are automatically removed.

## Code Files

### New Files Created
- **EDMSetup.iss** - Inno Setup installer script
- **EDM/Services/ContextMenuService.cs** - Registry management service (static)
- **EDM/Views/ContextMenuRegistrationWindow.xaml** - Confirmation dialog UI
- **EDM/Views/ContextMenuRegistrationWindow.xaml.cs** - Dialog handler

### Modified Files
- **EDM/Views/SettingsWindow.xaml** - Added Windows Explorer Integration section
- **EDM/Views/SettingsWindow.xaml.cs** - Added context menu toggle handlers

## ContextMenuService API

All methods are static and require no instantiation:

```csharp
// Check if context menu is registered
bool isActive = ContextMenuService.IsContextMenuActive();

// Register context menu (requires admin)
var result = ContextMenuService.RegisterContextMenu();
if (result.Success) { /* success */ }
else { MessageBox.Show(result.Message); }

// Unregister context menu (requires admin)
var result = ContextMenuService.UnregisterContextMenu();

// Toggle registration state
var result = ContextMenuService.ToggleContextMenu();

// Check if app is running as admin
bool isAdmin = IsRunningAsAdmin(); // private method

// Elevate to admin and run callback
ContextMenuService.ElevateAndExecute(() => {
	// Code here runs as admin
});
```

## Admin Privilege Handling

1. **On Install**: Installer runs as admin (PrivilegesRequired=admin)
2. **At Runtime**: If user tries to toggle without admin:
   - ContextMenuService detects lack of privileges
   - Returns error message in ContextMenuResult
   - UI shows "Admin Privileges Required"
   - User can click button again to re-elevate the entire EDM application

## Security Notes

- Registry modifications are user-scoped (HKEY_CURRENT_USER, not HKEY_LOCAL_MACHINE)
- No system-wide installation required
- Context menu entries only affect current Windows user
- All file paths are properly escaped and quoted
- Admin privileges required to prevent malicious registry modifications

## Testing

To test the context menu:
1. Enable context menu via Settings (or run installer with checkbox)
2. Open Windows Explorer
3. Right-click any file → Should see "Download with EDM"
4. Right-click HTTP/HTTPS link → Should see "Download with EDM"
5. Click option → EDM launches with URL/file path as argument

To disable:
1. Return to Settings
2. Click "Disable" button
3. Context menu entries are removed from registry

## Troubleshooting

**Context menu not appearing:**
- Verify registry keys exist in `regedit.exe` at `HKEY_CURRENT_USER\Software\Classes\*\shell\DownloadWithEDM`
- Check EDM.exe path is correct: `"{app}\EDM.exe"`
- Windows Explorer may need to be restarted
- Verify admin privileges were used

**"Admin Privileges Required" message:**
- User doesn't have sufficient permissions
- Windows may block elevation (check UAC settings)
- Group Policy may restrict registry access

**EDM not launching from context menu:**
- Verify EDM.exe is in the installation directory
- Check event viewer for errors
- Verify argument passing: `EDM.exe "%1"` in command registry key

## Implementation Notes

- ContextMenuService uses `System.Threading.Tasks.Parallel` for non-blocking operations
- Registry access uses `Microsoft.Win32.Registry` API
- Dialog uses standard WPF MessageBox for consistency
- Logging via existing LoggingService for audit trail
- All operations logged to EDM.log for debugging
