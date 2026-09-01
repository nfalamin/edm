; EDMSetup.iss — Inno Setup Installer for EDM (Exclusive Download Manager)
; Version 2.1.0 — Enterprise-Grade with Browser Extension + NativeHost + Admin Dashboard

#define MyAppName       "Exclusive Download Manager"
#define MyAppShortName  "EDM"
#define MyAppVersion    "2.1.0"
#define MyAppPublisher  "nfalamin"
#define MyAppExeName    "EDM.exe"
#define MyAppNativeHost "EDM.NativeHost.exe"
#define MyAppURL        "https://edm-app.com"

[Setup]
AppId={{A7B3C2D1-E4F5-4A6B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/update
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.\Output\installer
OutputBaseFilename=EDM_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
SetupIconFile=Icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
WizardStyle=modern
WizardSizePercent=120
DisableProgramGroupPage=yes
DisableDirPage=no
ShowLanguageDialog=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoCopyright=Copyright (C) 2025-2026 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
MinVersion=10.0.17763
LicenseFile=LICENSE.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.WelcomeLabel2=This will install {#MyAppName} v{#MyAppVersion} — a next-generation multi-threaded download manager with browser extension support, live admin dashboard, and intelligent speed optimization.%n%nClick Next to continue.

[Tasks]
Name: "desktopicon";   Description: "{cm:CreateDesktopIcon}";                                    GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "contextmenu";   Description: "Add 'Download with EDM' to right-click context menu";       GroupDescription: "Windows Integration:"
Name: "startuprun";    Description: "Start EDM automatically with Windows (minimized to tray)";   GroupDescription: "Windows Integration:"; Flags: unchecked
Name: "opendashboard"; Description: "Open Admin Dashboard after installation";                    GroupDescription: "After Setup:"; Flags: checkedonce

[Files]
; Main application binaries (published output)
Source: "Output\publish\*";             DestDir: "{app}";                    Flags: ignoreversion recursesubdirs createallsubdirs

; Admin Control Plane Dashboard
Source: "EDM.ControlPlane.Dashboard\*"; DestDir: "{app}\Dashboard";          Flags: ignoreversion recursesubdirs createallsubdirs

; Browser Extension files (Chrome, Edge, Firefox)
Source: "tools\chrome-extension\*";    DestDir: "{app}\extension\chrome";    Flags: ignoreversion recursesubdirs createallsubdirs
Source: "tools\firefox-extension\*";   DestDir: "{app}\extension\firefox";   Flags: ignoreversion recursesubdirs createallsubdirs

; Packaged extension ZIPs for easy install
Source: "Dist\chrome-extension.zip";   DestDir: "{app}\extension"; DestName: "EDM-Chrome-Extension.zip";  Flags: ignoreversion
Source: "Dist\edge-extension.zip";     DestDir: "{app}\extension"; DestName: "EDM-Edge-Extension.zip";    Flags: ignoreversion
Source: "Dist\firefox-extension.zip";  DestDir: "{app}\extension"; DestName: "EDM-Firefox-Extension.zip"; Flags: ignoreversion

[Icons]
; Start Menu group
Name: "{group}\{#MyAppName}";                                 Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Admin Dashboard";                               Filename: "{app}\Dashboard\index.html"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}";           Filename: "{uninstallexe}"

; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Windows startup
Name: "{userstartup}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Parameters: "--minimized"; Tasks: startuprun

[Run]
; Register browser extension native messaging host
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-native-host"; Flags: runhidden waituntilterminated; StatusMsg: "Registering browser extension native host..."

; Launch main app after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; Open Admin Dashboard
Filename: "{app}\Dashboard\index.html"; Description: "Open Admin Dashboard"; Flags: nowait postinstall skipifsilent shellexec; Tasks: opendashboard

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall-native-host"; Flags: runhidden waituntilterminated; RunOnceId: "UnregNativeHost"

[Registry]
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppShortName}"; ValueType: string; ValueName: "Version";       ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppShortName}"; ValueType: string; ValueName: "InstallDir";    ValueData: "{app}"
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppShortName}"; ValueType: string; ValueName: "DashboardPath"; ValueData: "{app}\Dashboard\index.html"

[Code]
{
  ============================================================
  CONTEXT MENU INTEGRATION
  Adds "Download with EDM" to right-click for files & URLs.
  Fully removed on uninstall.
  ============================================================
}

procedure RegisterContextMenu;
var
  IconPath: String;
  ExePath:  String;
begin
  ExePath  := ExpandConstant('"{app}\{#MyAppExeName}"');
  IconPath := ExpandConstant('"{app}\{#MyAppExeName}",0');

  { All Files (*) }
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\*\shell\DownloadWithEDM',           '', 'Download with EDM');
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\*\shell\DownloadWithEDM',           'Icon', IconPath);
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\*\shell\DownloadWithEDM\command',   '', ExePath + ' "%1"');

  { HTTP URLs }
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\http\shell\DownloadWithEDM',        '', 'Download with EDM');
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\http\shell\DownloadWithEDM',        'Icon', IconPath);
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\http\shell\DownloadWithEDM\command','', ExePath + ' "%1"');

  { HTTPS URLs }
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\https\shell\DownloadWithEDM',        '', 'Download with EDM');
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\https\shell\DownloadWithEDM',        'Icon', IconPath);
  RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Classes\https\shell\DownloadWithEDM\command','', ExePath + ' "%1"');
end;

procedure UnregisterContextMenu;
begin
  RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Classes\*\shell\DownloadWithEDM');
  RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Classes\http\shell\DownloadWithEDM');
  RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Classes\https\shell\DownloadWithEDM');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    if WizardIsTaskSelected('contextmenu') then
      RegisterContextMenu;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    UnregisterContextMenu;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
end;
