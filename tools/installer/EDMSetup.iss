; Inno Setup installer script for Exclusive Download Manager (EDM)
; Compiles with Inno Setup 6.x (ISCC.exe)

[Setup]
AppName=Exclusive Download Manager (EDM)
AppVersion=1.0
AppPublisher=EDM Team
AppPublisherURL=https://github.com/exclusive-download-manager
DefaultDirName={autopf}\Exclusive Download Manager
DefaultGroupName=Exclusive Download Manager (EDM)
DisableProgramGroupPage=no
OutputBaseFilename=EDM_Setup_v1.0
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
Name: startmenuicon; Description: "Create a &Start Menu shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Core Application Files (Published Release Binaries)
Source: "..\..\publish\EDM\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main

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
Root: HKCR; Subkey: "edm"; ValueType: string; ValueData: "URL:EDM Protocol Handler"; Flags: uninsdeletekey; Components: fileassoc
Root: HKCR; Subkey: "edm"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey; Components: fileassoc
Root: HKCR; Subkey: "edm\shell\open\command"; ValueType: string; ValueData: """{app}\EDM.exe"" ""%1"""; Flags: uninsdeletekey; Components: fileassoc

; File Association for .edm batch download container
Root: HKCR; Subkey: ".edm"; ValueType: string; ValueData: "EDM.BatchDownload"; Flags: uninsdeletekey; Components: fileassoc
Root: HKCR; Subkey: "EDM.BatchDownload"; ValueType: string; ValueData: "EDM Download Batch File"; Flags: uninsdeletekey; Components: fileassoc
Root: HKCR; Subkey: "EDM.BatchDownload\DefaultIcon"; ValueType: string; ValueData: "{app}\EDM.exe,0"; Flags: uninsdeletekey; Components: fileassoc
Root: HKCR; Subkey: "EDM.BatchDownload\shell\open\command"; ValueType: string; ValueData: """{app}\EDM.exe"" ""%1"""; Flags: uninsdeletekey; Components: fileassoc

; Chrome & Chromium Auto Extension Registration (IDM-style 1-click prompt)
Root: HKCU; Subkey: "Software\Google\Chrome\Extensions\knldjmfmopnpolahpmmgbagdohdnhkda"; ValueType: string; ValueName: "update_url"; ValueData: "https://clients2.google.com/service/update2/crx"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Edge\Extensions\knldjmfmopnpolahpmmgbagdohdnhkda"; ValueType: string; ValueName: "update_url"; ValueData: "https://edge.microsoft.com/extensionwebstorebase/v1/crx"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\BraveSoftware\Brave-Browser\Extensions\knldjmfmopnpolahpmmgbagdohdnhkda"; ValueType: string; ValueName: "update_url"; ValueData: "https://clients2.google.com/service/update2/crx"; Flags: uninsdeletekey

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
  ExePath := AppDir + '\EDM.NativeHost.exe';
  EscapedExePath := ExePath;
  StringChange(EscapedExePath, '\', '\\');

  // Chrome Manifest & Registry
  ChromeManifestPath := AppDir + '\com.edm.downloader.windows.json';
  ChromeJson := '{' + #13#10 +
    '  "name": "' + NativeHostName + '",' + #13#10 +
    '  "description": "EDM Chrome Native Messaging Host",' + #13#10 +
    '  "path": "' + EscapedExePath + '",' + #13#10 +
    '  "type": "stdio",' + #13#10 +
    '  "allowed_origins": [' + #13#10 +
    '    "chrome-extension://fgnkgamjcmfccjmkifdhipjgnagfgioe/",' + #13#10 +
    '    "chrome-extension://knldjmfmopnpolahpmmgbagdohdnhkda/",' + #13#10 +
    '    "chrome-extension://lhfkofephegnnhpcfkffnflfobafpaoe/",' + #13#10 +
    '    "chrome-extension://pjnefijmagpdjfhhkpljicbbpicelgko/",' + #13#10 +
    '    "chrome-extension://agionbommeaifngbhincahgmoflcikhm/",' + #13#10 +
    '    "chrome-extension://aapbdbdomjkkjkaonfhkkikfgjllcleb/",' + #13#10 +
    '    "chrome-extension://eppiocemhmnlbhjplcgkofciiegomcon/",' + #13#10 +
    '    "chrome-extension://aicmkgpgakddgnaphhhpliifpcfhicfo/",' + #13#10 +
    '    "chrome-extension://ghbmnnjooekpmoecnnnilnnbdlolhkhi/",' + #13#10 +
    '    "chrome-extension://ngpampappnmepgilojfohadhhmbhlaek/",' + #13#10 +
    '    "chrome-extension://joalfcmoabjccbphlngocfcpkglmalkj/",' + #13#10 +
    '    "chrome-extension://omfoimoadhlddiepbagphpoccblokgem/",' + #13#10 +
    '    "chrome-extension://nmmhkkegccagdldgiimedpiccmgmieda/",' + #13#10 +
    '    "chrome-extension://bcmmjkglicliekcndffbfgcfopnidllp/",' + #13#10 +
    '    "chrome-extension://caidcmannjgahlnbpmidmiecjcoiiigg/",' + #13#10 +
    '    "chrome-extension://aohghmighlieiainnegkcijnfilokake/",' + #13#10 +
    '    "chrome-extension://aapocclcgogkmnckokdopfmhonfmgoek/",' + #13#10 +
    '    "chrome-extension://felcaaldnbdncclmgdcncolpebgiejap/",' + #13#10 +
    '    "chrome-extension://apdfllckaahabafndbhieahigkjlhalf/",' + #13#10 +
    '    "chrome-extension://pjkljhegncpnkpknbcohdijeoejaedia/",' + #13#10 +
    '    "chrome-extension://blpcfgokakmgnkcojhhkbfbldkacnbeo/"' + #13#10 +
    '  ]' + #13#10 +
    '}';
  SaveStringToFile(ChromeManifestPath, ChromeJson, False);

  KeyChrome := 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName;
  RegWriteStringValue(HKCU, KeyChrome, '', ChromeManifestPath);

  // Edge Manifest & Registry
  EdgeManifestPath := AppDir + '\com.edm.downloader.edge.json';
  EdgeJson := '{' + #13#10 +
    '  "name": "' + NativeHostName + '",' + #13#10 +
    '  "description": "EDM Edge Native Messaging Host",' + #13#10 +
    '  "path": "' + EscapedExePath + '",' + #13#10 +
    '  "type": "stdio",' + #13#10 +
    '  "allowed_origins": [' + #13#10 +
    '    "chrome-extension://fgnkgamjcmfccjmkifdhipjgnagfgioe/",' + #13#10 +
    '    "chrome-extension://knldjmfmopnpolahpmmgbagdohdnhkda/",' + #13#10 +
    '    "chrome-extension://lhfkofephegnnhpcfkffnflfobafpaoe/",' + #13#10 +
    '    "chrome-extension://pjnefijmagpdjfhhkpljicbbpicelgko/",' + #13#10 +
    '    "chrome-extension://agionbommeaifngbhincahgmoflcikhm/",' + #13#10 +
    '    "chrome-extension://aapbdbdomjkkjkaonfhkkikfgjllcleb/",' + #13#10 +
    '    "chrome-extension://eppiocemhmnlbhjplcgkofciiegomcon/",' + #13#10 +
    '    "chrome-extension://aicmkgpgakddgnaphhhpliifpcfhicfo/",' + #13#10 +
    '    "chrome-extension://ghbmnnjooekpmoecnnnilnnbdlolhkhi/",' + #13#10 +
    '    "chrome-extension://ngpampappnmepgilojfohadhhmbhlaek/",' + #13#10 +
    '    "chrome-extension://joalfcmoabjccbphlngocfcpkglmalkj/",' + #13#10 +
    '    "chrome-extension://omfoimoadhlddiepbagphpoccblokgem/",' + #13#10 +
    '    "chrome-extension://nmmhkkegccagdldgiimedpiccmgmieda/",' + #13#10 +
    '    "chrome-extension://bcmmjkglicliekcndffbfgcfopnidllp/",' + #13#10 +
    '    "chrome-extension://caidcmannjgahlnbpmidmiecjcoiiigg/",' + #13#10 +
    '    "chrome-extension://aohghmighlieiainnegkcijnfilokake/",' + #13#10 +
    '    "chrome-extension://aapocclcgogkmnckokdopfmhonfmgoek/",' + #13#10 +
    '    "chrome-extension://felcaaldnbdncclmgdcncolpebgiejap/",' + #13#10 +
    '    "chrome-extension://apdfllckaahabafndbhieahigkjlhalf/",' + #13#10 +
    '    "chrome-extension://pjkljhegncpnkpknbcohdijeoejaedia/",' + #13#10 +
    '    "chrome-extension://blpcfgokakmgnkcojhhkbfbldkacnbeo/"' + #13#10 +
    '  ]' + #13#10 +
    '}';
  SaveStringToFile(EdgeManifestPath, EdgeJson, False);

  KeyEdge := 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName;
  RegWriteStringValue(HKCU, KeyEdge, '', EdgeManifestPath);

  // Firefox Manifest & Registry
  FirefoxManifestPath := AppDir + '\com.edm.downloader.firefox.json';
  FirefoxJson := '{' + #13#10 +
    '  "name": "' + NativeHostName + '",' + #13#10 +
    '  "description": "EDM Firefox Native Messaging Host",' + #13#10 +
    '  "path": "' + EscapedExePath + '",' + #13#10 +
    '  "type": "stdio",' + #13#10 +
    '  "allowed_extensions": [' + #13#10 +
    '    "edm@exclusive-download-manager.com"' + #13#10 +
    '  ]' + #13#10 +
    '}';
  SaveStringToFile(FirefoxManifestPath, FirefoxJson, False);

  KeyFirefox := 'Software\Mozilla\NativeMessagingHosts\' + NativeHostName;
  RegWriteStringValue(HKCU, KeyFirefox, '', FirefoxManifestPath);
end;

procedure UnregisterNativeMessagingHosts();
begin
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName);
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName);
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Mozilla\NativeMessagingHosts\' + NativeHostName);
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
  UserAppDataPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    UnregisterNativeMessagingHosts();
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    UserAppDataPath := ExpandConstant('{userappdata}\EDM');
    if DirExists(UserAppDataPath) then
    begin
      if MsgBox('Do you want to delete your download history, settings, and database?' + #13#10 + 'Location: ' + UserAppDataPath, mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(UserAppDataPath, True, True, True);
      end;
    end;

    UserDataPath := GetEnv('USERPROFILE') + '\EDM';
    if (UserDataPath <> '\EDM') and DirExists(UserDataPath) then
    begin
      DelTree(UserDataPath, True, True, True);
    end;
  end;
end;
