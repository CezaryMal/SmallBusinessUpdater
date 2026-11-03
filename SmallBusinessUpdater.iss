#define AppVersion GetFileVersion("artifacts\publish\SmallBusinessUpdater.exe")

[Setup]
AppId={{B6B7812C-7F34-4DAE-A513-27F79B53F814}
AppName=Small Business Updater
AppVersion={#AppVersion}
AppPublisher=Small Business Updater
DefaultDirName={autopf}\Small Business Updater
DefaultGroupName=Small Business Updater
UninstallDisplayIcon={app}\SmallBusinessUpdater.exe
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=artifacts\installer
OutputBaseFilename=SmallBusinessUpdater_Setup_v{#AppVersion}
SetupIconFile=app.ico

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "artifacts\publish\SmallBusinessUpdater.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Small Business Updater"; Filename: "{app}\SmallBusinessUpdater.exe"
Name: "{autodesktop}\Small Business Updater"; Filename: "{app}\SmallBusinessUpdater.exe"; Tasks: desktopicon

