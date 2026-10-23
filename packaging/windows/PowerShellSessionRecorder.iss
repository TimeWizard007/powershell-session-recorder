; Inno Setup 6 script for a self-contained win-x64 build.
; Compile on Windows with packaging\windows\build-setup.ps1
; SetupIconFile is the installer executable icon. Shortcuts use the
; application executable, which embeds the same multi-resolution icon.
;
; PrivilegesRequired=lowest makes the dialog default a per-user install.
; PrivilegesRequiredOverridesAllowed=dialog lets the user explicitly choose
; an all-users install, which then asks for UAC. UsePreviousPrivileges=yes
; keeps an upgrade in the scope already registered for this AppId and does
; not silently switch scope.
; {autopf} is Program Files for an all-users install and
; {localappdata}\Programs for a per-user install.
; Uninstall removes only {app}. It does not remove
; %LOCALAPPDATA%\PowerShellSessionRecorder.

#define MyAppName "PowerShell Session Recorder"
#define MyAppVersion "0.1.0-beta.2"
#define MyAppPublisher "TimeWizard"
#define MyAppExeName "PowerShellSessionRecorder.exe"
#define MyAppURL "https://github.com/TimeWizard007/powershell-session-recorder"

[Setup]
AppId={{A7B3C1E2-4F58-4D1A-9C6E-2B8F0A1D5E77}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\PowerShellSessionRecorder
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts
OutputBaseFilename=PowerShellSessionRecorder-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=yes
SetupIconFile=..\..\assets\branding\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=0.1.0.0
VersionInfoProductVersion=0.1.0.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,settings.json,customers.json,history.json"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
