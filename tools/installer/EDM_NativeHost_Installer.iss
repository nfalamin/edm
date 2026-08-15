; Inno Setup script for EDM native messaging host installer
; Save as tools/installer/EDM_NativeHost_Installer.iss and compile with Inno Setup

[Setup]
AppName=EDM Native Host
AppVersion=1.0
DefaultDirName={localappdata}\Programs\EDM
DefaultGroupName=EDM
DisableProgramGroupPage=no
OutputBaseFilename=EDM_NativeHost_Installer
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\EDM\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EDM"; Filename: "{app}\EDM.exe"
Name: "{commondesktop}\EDM"; Filename: "{app}\EDM.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
; Filename: "{app}\EDM.exe"; Description: "Launch EDM"; Flags: nowait postinstall skipifsilent

[Code]
const
  NativeHostName = 'com.edm.downloader';
  AllowedOrigin = 'chrome-extension://acokcbpbjbnkdkmghlfenmiobchhpgdp/';

function SaveUtf8StringToFile(const FileName, S: string): Boolean;
var
  F: TFileStream;
  Bytes: AnsiString;
begin
  Result := False;
  try
	Bytes := UTF8Encode(S);
	F := TFileStream.Create(FileName, fmCreate);
	try
	  F.WriteBuffer(PAnsiChar(Bytes)^, Length(Bytes));
	  Result := True;
	finally
	  F.Free;
	end;
  except
	Result := False;
  end;
end;

procedure RegisterNativeHostInHKCU(const ManifestPath: string);
var
  KeyChrome, KeyEdge: string;
begin
  KeyChrome := 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName;
  KeyEdge := 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName;

  RegCreateKey(HKCU, KeyChrome);
  RegWriteStringValue(HKCU, KeyChrome, '', ManifestPath);

  RegCreateKey(HKCU, KeyEdge);
  RegWriteStringValue(HKCU, KeyEdge, '', ManifestPath);
end;

procedure UnregisterNativeHostFromHKCU();
var
  KeyChrome, KeyEdge: string;
begin
  KeyChrome := 'Software\Google\Chrome\NativeMessagingHosts\' + NativeHostName;
  KeyEdge := 'Software\Microsoft\Edge\NativeMessagingHosts\' + NativeHostName;

  try
	RegDeleteKey(HKCU, KeyChrome);
  except
  end;

  try
	RegDeleteKey(HKCU, KeyEdge);
  except
  end;
end;

function GetInstalledHostExePath(): string;
begin
  if FileExists(ExpandConstant('{app}\EDM.exe')) then
	Result := ExpandConstant('{app}\EDM.exe')
  else if FileExists(ExpandConstant('{app}\EDM.Host.exe')) then
	Result := ExpandConstant('{app}\EDM.Host.exe')
  else
	Result := ExpandConstant('{app}\EDM.exe');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ManifestPath, ExePath, ManifestJson, EscapedExePath: string;
  Created: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
	ManifestPath := ExpandConstant('{app}\com.edm.downloader.windows.json');
	ExePath := GetInstalledHostExePath();

	EscapedExePath := StringReplace(ExePath, '\', '\\', True);

	ManifestJson := '{' + sLineBreak +
	  '  "name": "' + NativeHostName + '",' + sLineBreak +
	  '  "description": "EDM Download capture native host",' + sLineBreak +
	  '  "path": "' + EscapedExePath + '",' + sLineBreak +
	  '  "type": "stdio",' + sLineBreak +
	  '  "allowed_origins": [' + sLineBreak +
	  '    "' + AllowedOrigin + '"' + sLineBreak +
	  '  ]' + sLineBreak +
	  '}';

	Created := SaveUtf8StringToFile(ManifestPath, ManifestJson);
	if not Created then
	begin
	  MsgBox('Failed to write native messaging manifest to: ' + ManifestPath, mbError, MB_OK);
	  Exit;
	end;

	RegisterNativeHostInHKCU(ManifestPath);
  end;
end;

procedure DeinitializeUninstall();
begin
  UnregisterNativeHostFromHKCU();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
	UnregisterNativeHostFromHKCU();
  end;
end;
