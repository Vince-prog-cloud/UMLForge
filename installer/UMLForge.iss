#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\UMLForge\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{8F4B2C91-6D3E-4A7B-9C21-F0A1B2C3D4E5}
AppName=UMLForge
AppVersion={#MyAppVersion}
AppPublisher=UMLForge
DefaultDirName={autopf}\UmlForge
DefaultGroupName=UmlForge
OutputDir=Output
OutputBaseFilename=UmlForge-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\UmlForge.exe

[Files]
Source: "{#PublishDir}\UmlForge.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\UmlForge"; Filename: "{app}\UmlForge.exe"
Name: "{autodesktop}\UmlForge"; Filename: "{app}\UmlForge.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Registry]
Root: HKA; Subkey: "Software\Classes\.uforge"; ValueType: string; ValueName: ""; ValueData: "UmlForge.Project"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\UmlForge.Project"; ValueType: string; ValueName: ""; ValueData: "UmlForge Project"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\UmlForge.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\UmlForge.exe,0"
Root: HKA; Subkey: "Software\Classes\UmlForge.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\UmlForge.exe"" ""%1"""

[Run]
Filename: "{app}\UmlForge.exe"; Description: "Launch UmlForge"; Flags: nowait postinstall skipifsilent