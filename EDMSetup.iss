; EDMSetup.iss - Inno Setup Installer for EDM (Enhanced Download Manager)
; This installer creates the application and registers context menu entries
; for Files, HTTP, and HTTPS protocols in Windows Explorer

#define MyAppName "Exclusive Download Manager"
#define MyAppShortName "EDM"
#define MyAppVersion "6.0.0"
#define MyAppPublisher "Exclusive Download Manager Technologies"
#define MyAppExeName "EDM.exe"
#define MyAppURL "https://github.com/exclusive/edm"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=.\Output
OutputBaseFilename=EDM_Setup
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
SetupIconFile=Icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "contextmenu"; Description: "Add 'Download with EDM' to Windows Explorer context menu"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main published binaries and dependencies from Output\publish
Source: "Output\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "extension\*"; DestDir: "{app}\extension"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-native-host"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

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
