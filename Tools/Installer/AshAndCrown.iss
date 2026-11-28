; Установщик «Пепел и корона» / Ash & Crown для Windows (Inno Setup 6).
;
; Собирается из Unity: меню «Ash & Crown → Собрать установщик для ПК» (Assets/Editor/WindowsInstaller.cs).
; Вручную:  ISCC.exe /DSourceDir="D:\...\Builds\Windows" /DAppVersion=0.9.0 Tools\Installer\AshAndCrown.iss
;
; В установщик попадают только файлы игры — папки *_BurstDebugInformation_DoNotShip и
; *_BackUpThisFolder_ButDontShipItWithYourGame не нужны игрокам.
; Сохранения и настройки лежат в AppData\LocalLow и реестре — при удалении игры они остаются.

#ifndef SourceDir
  #define SourceDir "..\..\Builds\Windows"
#endif
#ifndef AppVersion
  #define AppVersion "0.9.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\Builds\Installer"
#endif

#define AppName "Ash & Crown"
#define ExeName "Ash & Crown.exe"
#define DataDir "Ash & Crown_Data"
#define Publisher "VoidRunar"

[Setup]
; AppId не менять: по нему Windows узнаёт игру при обновлении и удалении.
AppId={{5E166D72-1E38-405A-A3DE-721EE61579F6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://novusegoist.github.io/Chess2.0/
AppSupportURL=https://novusegoist.github.io/Chess2.0/
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Можно поставить и без прав администратора — тогда в папку пользователя.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=AshAndCrown-Setup-{#AppVersion}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Игра запущена — установщик предложит её закрыть.
CloseApplications=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#Publisher}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\UnityPlayer.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\UnityCrashHandler64.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceDir}\{#DataDir}\*"; DestDir: "{app}\{#DataDir}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SourceDir}\D3D12\*"; DestDir: "{app}\D3D12"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Пока игра на тестовом AppID 480 — без этого файла Steam-функции вне Steam не включатся.
; Когда игра выйдет в Steam, его ставит сам Steam, и отсюда строку можно убрать.
Source: "{#SourceDir}\steam_appid.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Логи Unity, которые игра может оставить рядом с собой.
Type: files; Name: "{app}\*.log"
