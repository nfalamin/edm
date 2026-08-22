; EDMSetup.iss - Inno Setup Installer for EDM (Exclusive Download Manager)
; Version 2.0 — IDM-Grade with Browser Extension + NativeHost

#define MyAppName "Exclusive Download Manager"
#define MyAppShortName "EDM"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Exclusive Download Manager Technologies"
#define MyAppExeName "EDM.exe"
#define MyAppNativeHost "EDM.NativeHost.exe"
#define MyAppURL "https://edm-app.com"

[Setup]
AppId={{A7B3C2D1-E4F5-4A6B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.\Output
OutputBaseFilename=EDM_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
SetupIconFile=Icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=no
ShowLanguageDialog=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "contextmenu"; Description: "Add 'Download with EDM' to right-click context menu"; GroupDescription: "Windows Integration:"
Name: "startuprun"; Description: "Start EDM automatically with Windows"; GroupDescription: "Windows Integration:"; Flags: unchecked

[Files]
; Main application binaries (published output)
Source: "Output\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Browser Extension files (Chrome, Edge, Firefox)
Source: "tools\chrome-extension\*"; DestDir: "{app}\extension\chrome"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "tools\firefox-extension\*"; DestDir: "{app}\extension\firefox"; Flags: ignoreversion recursesubdirs createallsubdirs

; Packaged extension ZIPs for easy install
Source: "Dist\chrome-extension.zip"; DestDir: "{app}\extension"; DestName: "EDM-Chrome-Extension.zip"; Flags: ignoreversion
Source: "Dist\edge-extension.zip"; DestDir: "{app}\extension"; DestName: "EDM-Edge-Extension.zip"; Flags: ignoreversion
Source: "Dist\firefox-extension.zip"; DestDir: "{app}\extension"; DestName: "EDM-Firefox-Extension.zip"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--minimized"; Tasks: startuprun

[Run]
; Register browser extension native host (Chrome, Edge, Firefox, Brave, Opera)
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-native-host"; Flags: runhidden waituntilterminated; StatusMsg: "Registering browser extension..."
; Launch app after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall-native-host"; Flags: runhidden waituntilterminated; RunOnceId: "UnregNativeHost"

[Code]
{
  ============================================
  REGISTRY CONTEXT MENU INTEGRATION CODE

  This code section modifies Windows Registry to add
  "Download with EDM" context menu entries.

  Registry Paths Modified:
  - HKEY_CURRENT_USER\Software\Classes\*\shell\DownloadWithEDM
  - HKEY_CURRENT_USER\Software\Classes\http\shell\DownloadWithEDM
  - HKEY_CURRENT_USER\Software\Classes\https\shell\DownloadWithEDM

  On Uninstall: All entries are completely removed
  ============================================
}

procedure RegisterContextMenu;
begin
  { Register context menu for all files (*) }
  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\*\shell\DownloadWithEDM',
	'',
	'Download with EDM');

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\*\shell\DownloadWithEDM',
	'Icon',
	ExpandConstant('"{app}\{#MyAppExeName}",0'));

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\*\shell\DownloadWithEDM\command',
	'',
	ExpandConstant('"{app}\{#MyAppExeName}" "%%1"'));

  { Register context menu for HTTP URLs }
  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\http\shell\DownloadWithEDM',
	'',
	'Download with EDM');

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\http\shell\DownloadWithEDM',
	'Icon',
	ExpandConstant('"{app}\{#MyAppExeName}",0'));

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\http\shell\DownloadWithEDM\command',
	'',
	ExpandConstant('"{app}\{#MyAppExeName}" "%%1"'));

  { Register context menu for HTTPS URLs }
  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\https\shell\DownloadWithEDM',
	'',
	'Download with EDM');

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\https\shell\DownloadWithEDM',
	'Icon',
	ExpandConstant('"{app}\{#MyAppExeName}",0'));

  RegWriteStringValue(
	HKEY_CURRENT_USER,
	'Software\Classes\https\shell\DownloadWithEDM\command',
	'',
	ExpandConstant('"{app}\{#MyAppExeName}" "%%1"'));
end;

procedure UnregisterContextMenu;
begin
  { Remove all context menu entries on uninstall }
  RegDeleteKeyIncludingSubkeys(
	HKEY_CURRENT_USER,
	'Software\Classes\*\shell\DownloadWithEDM');

  RegDeleteKeyIncludingSubkeys(
	HKEY_CURRENT_USER,
	'Software\Classes\http\shell\DownloadWithEDM');

  RegDeleteKeyIncludingSubkeys(
	HKEY_CURRENT_USER,
	'Software\Classes\https\shell\DownloadWithEDM');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Register context menu during installation if task selected }
  if CurStep = ssInstall then
  begin
	if WizardIsTaskSelected('contextmenu') then
	  RegisterContextMenu;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  { Remove context menu entries during uninstall }
  if CurUninstallStep = usUninstall then
	UnregisterContextMenu;
end;
