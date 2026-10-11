; 班级工作台 安装脚本（Inno Setup 6）
;
; 用法：先用 dotnet publish 生成发布目录，再编译本脚本：
;   dotnet publish ClassScheduleApp\ClassScheduleApp.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=<发布目录>\
;   ISCC.exe installer\班级工作台.iss /DSourceDir=<发布目录> /DOutputDir=<输出目录>
;
; 说明：只打包程序本体（自包含 .NET 运行时 + PDFium 原生库 + 使用说明），
;       不包含任何数据；首次运行由应用自己在安装目录下创建 data\ 与空白数据库。

#define AppName "班级工作台"
#define AppVersion "1.0.0"
#define AppPublisher "班级工作台"

; SourceDir / OutputDir 可用命令行 /D 覆盖（默认取脚本同级的 ..\publish 与 ..\dist）
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#define OutputBase "班级工作台_setup_" + AppVersion

[Setup]
AppId={{8F3A2C51-7D64-4E2B-9C1A-5B7E0D3F4A62}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\班级工作台.exe
SetupIconFile=
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} 安装程序
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\班级工作台.exe"; WorkingDir: "{app}"
Name: "{group}\使用说明"; Filename: "{app}\使用说明.txt"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\班级工作台.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\班级工作台.exe"; Description: "立即启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel1=欢迎安装 [name]
WelcomeLabel2=即将把 [name/ver] 安装到你的电脑。%n%n安装包只包含程序本体，不含任何数据；首次运行时程序会在安装目录下自动创建 data 文件夹与空白数据库。%n%n点「下一步」继续。
SelectDirLabel3=安装程序将把 [name] 安装到下面的文件夹。
SelectDirBrowseLabel=点「下一步」继续；如需更换位置（例如装到 D 盘），点「浏览」。
SelectTasksLabel2=请选择安装 [name] 时需要一并完成的附加任务，然后点「下一步」。
ReadyLabel1=安装程序已准备好开始安装 [name]。
ReadyLabel2a=点「安装」开始安装；如需检查或更改设置，点「上一步」。
FinishedLabel=安装完成。%n%n可以从开始菜单或桌面快捷方式启动 [name]；数据保存在程序目录下的 data 文件夹。
FinishedLabelNoIcons=安装完成。%n%n数据保存在程序目录下的 data 文件夹。
ClickFinish=点「完成」结束安装。
RunEntryExec=启动 %1
ButtonNext=下一步(&N) >
ButtonBack=< 上一步(&B)
ButtonInstall=安装(&I)
ButtonFinish=完成(&F)
ButtonBrowse=浏览(&R)...
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonNo=否(&N)
ExitSetupTitle=退出安装
ExitSetupMessage=安装尚未完成，确定要退出吗？
UninstallAppFullTitle=卸载 %1
ConfirmUninstall=确定要卸载 %1 吗？%n%n注意：程序目录下的 data 文件夹（你的全部数据）不会被删除。
UninstalledAll=%1 已成功卸载。%n%n提示：data 文件夹仍保留在原安装目录，如需彻底清除请手动删除。
UninstallStatusLabel=正在卸载 %1，请稍候...
