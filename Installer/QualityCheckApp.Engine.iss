#define MyAppName "哈勘院质检工具"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "哈勘院"
#define MyAppExeName "QualityCheckApp.Engine.exe"

#ifndef MyBuildOutput
  #error MyBuildOutput is not defined
#endif

#ifndef MyArtifactsRoot
  #error MyArtifactsRoot is not defined
#endif

[Setup]
AppId={{2A96BDB4-4EE7-4C89-A27A-18B1542CA2A7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#MyArtifactsRoot}
OutputBaseFilename=QualityCheckApp.Engine-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x86compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "{#MyBuildOutput}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  MsgBox(
    '安装包只负责部署主程序、快捷方式和卸载入口。' + #13#10#13#10 +
    '请先在目标电脑上手工安装 ArcGIS Engine Runtime 并完成许可配置。',
    mbInformation,
    MB_OK);
  Result := True;
end;
