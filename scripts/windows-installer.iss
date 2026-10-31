#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
[Setup]
AppId={{A637F09F-884C-48E5-9AD8-873A15BFA15B}
AppName=EyeKeeper
AppVersion={#AppVersion}
AppPublisher=Ali Zeiynali
AppPublisherURL=https://github.com/Ali-zeiynali/EyeKeeper
AppSupportURL=https://github.com/Ali-zeiynali/EyeKeeper/issues
DefaultDirName={localappdata}\Programs\EyeKeeper
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\release
OutputBaseFilename=EyeKeeper-{#AppVersion}-windows-x64-setup
SetupIconFile=..\src\EyeKeeper\Assets\eyekeeper.ico
UninstallDisplayIcon={app}\EyeKeeper.exe
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=no
RestartApplications=no

[Tasks]
Name: startup; Description: "Launch EyeKeeper with Windows"; Flags: checkedonce

[Files]
Source: "..\artifacts\app\EyeKeeper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\EyeKeeper\Assets\eyekeeper.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "release-integration.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\EyeKeeper"; Filename: "{app}\EyeKeeper.exe"; Parameters: "--background"; Check: IntegrationEnabled

[Run]
Filename: "{app}\EyeKeeper.exe"; Parameters: "--background"; Description: "Start EyeKeeper in the notification area"; Flags: nowait postinstall skipifsilent; Check: IntegrationEnabled

[Code]
function IntegrationEnabled: Boolean;
begin
  Result := ExpandConstant('{param:NOINTEGRATION|0}') <> '1';
end;

function RunIntegration(Mode: String; Script: String; Startup: Boolean): Boolean;
var
  ExitCode: Integer;
  Args: String;
begin
  Args := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + Script +
    '" -Mode ' + Mode + ' -Executable "' + ExpandConstant('{app}\EyeKeeper.exe') + '"';
  if Mode = 'Install' then begin
    if Startup then Args := Args + ' -Startup 1' else Args := Args + ' -Startup 0';
  end;
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args,
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  Result := Result and (ExitCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if IntegrationEnabled and FileExists(ExpandConstant('{app}\EyeKeeper.exe')) then begin
    ExtractTemporaryFile('release-integration.ps1');
    if not RunIntegration('Stop', ExpandConstant('{tmp}\release-integration.ps1'), False) then
      Result := 'Please quit EyeKeeper completely, then try again.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and IntegrationEnabled then begin
    if not RunIntegration('Install', ExpandConstant('{app}\release-integration.ps1'), WizardIsTaskSelected('startup')) then
      RaiseException('EyeKeeper was copied, but Windows integration failed. Run Setup again to repair it.');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := RunIntegration('Remove', ExpandConstant('{app}\release-integration.ps1'), False);
  if not Result then MsgBox('Could not stop EyeKeeper or remove its Windows integration. Please retry.', mbError, MB_OK);
end;
