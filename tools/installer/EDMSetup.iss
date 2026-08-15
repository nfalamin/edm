; Inno Setup installer script for Exclusive Download Manager (EDM)
; Compiles with Inno Setup 6.x (ISCC.exe)

[Setup]
AppName=Exclusive Download Manager (EDM)
AppVersion=2.0
AppPublisher=EDM Team
AppPublisherURL=https://github.com/exclusive-download-manager
DefaultDirName={autopf}\Exclusive Download Manager
DefaultGroupName=Exclusive Download Manager (EDM)
DisableProgramGroupPage=no
OutputBaseFilename=EDMSetup
OutputDir=Output
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\EDM.exe
ChangesAssociations=yes

[Types]
Name: "full"; Description: "Full Installation (Recommended)"
Name: "custom"; Description: "Custom Installation"; Flags: iscustom

[Components]
Name: "main"; Description: "EDM Core Application & Extensions"; Types: full custom; Flags: fixed
Name: "fileassoc"; Description: "Register EDM URL Protocol & File Associations"; Types: full

[Tasks]
Name: desktopicon; Description: "Create a &Desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: startmenuicon; Description: "Create a &Start Menu shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checked

[Files]
; Core Application Files
Source: "..\..\EDM\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main

; Extension Manifest Templates
Source: "..\chrome-extension\*"; DestDir: "{app}\extensions\chrome"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main
Source: "..\edge-extension\*"; DestDir: "{app}\extensions\edge"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main
Source: "..\firefox-extension\*"; DestDir: "{app}\extensions\firefox"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main

[Icons]
Name: "{group}\Exclusive Download Manager"; Filename: "{app}\EDM.exe"
Name: "{group}\Uninstall EDM"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Exclusive Download Manager"; Filename: "{app}\EDM.exe"; Tasks: desktopicon

[Registry]
; Protocol Handler for edm://
HKCR; Subkey: "edm"; ValueType: string; ValueData: "URL:EDM Protocol Handler"; Flags: uninsdeletekey; Components: fileassoc
HKCR; Subkey: "edm"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey; Components: fileassoc
HKCR; Subkey: "edm\shell\open\command"; ValueType: string; ValueData: """{app}\EDM.exe"" ""%1"""; Flags: uninsdeletekey; Components: fileassoc

; File Association for .edm batch download container
HKCR; Subkey: ".edm"; ValueType: string; ValueData: "EDM.BatchDownload"; Flags: uninsdeletekey; Components: fileassoc
HKCR; Subkey: "EDM.BatchDownload"; ValueType: string; ValueData: "EDM Download Batch File"; Flags: uninsdeletekey; Components: fileassoc
HKCR; Subkey: "EDM.BatchDownload\DefaultIcon"; ValueType: string; ValueData: "{app}\EDM.exe,0"; Flags: uninsdeletekey; Components: fileassoc
HKCR; Subkey: "EDM.BatchDownload\shell\open\command"; ValueType: string; ValueData: """{app}\EDM.exe"" ""%1"""; Flags: uninsdeletekey; Components: fileassoc

[Run]
Filename: "{app}\EDM.exe"; Description: "Launch Exclusive Download Manager"; Flags: nowait postinstall skipifsilent

[Code]
const
  NativeHostName = 'com.edm.downloader';

procedure RegisterNativeMessagingHosts(const AppDir: string);
var
  ExePath, EscapedExePath: string;
  ChromeManifestPath, EdgeManifestPath, FirefoxManifestPath: string;
  ChromeJson, EdgeJson, FirefoxJson: string;
  KeyChrome, KeyEdge, KeyFirefox: string;
begin
  ExePath := AppDir + '\EDM.exe';
  EscapedExePath := StringReplace(ExePath, '\', '\\', True);

  // Chrome Manifest & Registry
  ChromeManifestPath := AppDir + '\com.edm.downloader.windows.json';
  ChromeJson := '{' + sLineBreak +
    '  "name": "' + NativeHostName + '",' + sLineBreak +
    '  "description": "EDM Chrome Native Messaging Host",' + sLineBreak +
    '  "path": "' + EscapedExePath + '",' + sLineBreak +
    '  "type": "stdio",' + sLineBreak +
    '  "allowed_origins": [' + sLineBreak +
    '    "chrome-extension://*"' + sLineBreak +
    '  ]' + sLineBreak +
    '}';
  SaveStringToFile(ChromeManifestPath, ChromeJson, False);

  KeyChrome := 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName;
  RegCreateKey(HKCU, KeyChrome);
  RegWriteStringValue(HKCU, KeyChrome, '', ChromeManifestPath);

  // Edge Manifest & Registry
  EdgeManifestPath := AppDir + '\com.edm.downloader.edge.json';
  EdgeJson := '{' + sLineBreak +
    '  "name": "' + NativeHostName + '",' + sLineBreak +
    '  "description": "EDM Edge Native Messaging Host",' + sLineBreak +
    '  "path": "' + EscapedExePath + '",' + sLineBreak +
    '  "type": "stdio",' + sLineBreak +
    '  "allowed_origins": [' + sLineBreak +
    '    "extension://*",' + sLineBreak +
    '    "chrome-extension://*"' + sLineBreak +
    '  ]' + sLineBreak +
    '}';
  SaveStringToFile(EdgeManifestPath, EdgeJson, False);

  KeyEdge := 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName;
  RegCreateKey(HKCU, KeyEdge);
  RegWriteStringValue(HKCU, KeyEdge, '', EdgeManifestPath);

  // Firefox Manifest & Registry
  FirefoxManifestPath := AppDir + '\com.edm.downloader.firefox.json';
  FirefoxJson := '{' + sLineBreak +
    '  "name": "' + NativeHostName + '",' + sLineBreak +
    '  "description": "EDM Firefox Native Messaging Host",' + sLineBreak +
    '  "path": "' + EscapedExePath + '",' + sLineBreak +
    '  "type": "stdio",' + sLineBreak +
    '  "allowed_extensions": [' + sLineBreak +
    '    "edm@exclusive-download-manager.com"' + sLineBreak +
    '  ]' + sLineBreak +
    '}';
  SaveStringToFile(FirefoxManifestPath, FirefoxJson, False);

  KeyFirefox := 'Software\Mozilla\NativeMessagingHosts\' + NativeHostName;
  RegCreateKey(HKCU, KeyFirefox);
  RegWriteStringValue(HKCU, KeyFirefox, '', FirefoxManifestPath);
end;

procedure UnregisterNativeMessagingHosts();
begin
  try RegDeleteKey(HKCU, 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName); except end;
  try RegDeleteKey(HKCU, 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName); except end;
  try RegDeleteKey(HKCU, 'Software\Mozilla\NativeMessagingHosts\' + NativeHostName); except end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RegisterNativeMessagingHosts(ExpandConstant('{app}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    UnregisterNativeMessagingHosts();
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    UserDataPath := ExpandConstant('{userprofile}\EDM');
    if DirExists(UserDataPath) then
    begin
      if MsgBox('Do you want to delete your download history, settings, and quarantined files?' + #13#10 + 'Location: ' + UserDataPath, mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(UserDataPath, True, True, True);
      end;
    end;
  end;
end;
