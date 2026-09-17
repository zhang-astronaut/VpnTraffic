; Inno Setup script for VpnTraffic Command Palette extension (WinGet)
#define AppVersion "0.2.1.0"
#define AppName "VpnTraffic"
#define AppExe "VpnTraffic.exe"

[Setup]
AppId={{a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=zhang-astronaut
AppPublisherURL=https://github.com/zhang-astronaut/VpnTraffic
AppSupportURL=https://github.com/zhang-astronaut/VpnTraffic
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=artifacts\installer
OutputBaseFilename=VpnTraffic-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCU; Subkey: "Software\Classes\CLSID\{{a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17}"; ValueType: string; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{{a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17}\LocalServer32"; ValueType: string; ValueData: """{app}\{#AppExe}"" -RegisterProcessAsComServer"; Flags: uninsdeletekey

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
