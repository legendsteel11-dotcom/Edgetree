; Edgetree installer (Inno Setup 6).
;
; Payload is the SELF-CONTAINED MULTI-FILE publish (publish\folder) - no .NET runtime needed
; on the target PC, and no single-file bundle. The bundle is why a clean PC shows this app at
; 350~400MB in Task Manager: a single-file self-contained exe unpacks itself into memory
; instead of being mapped from disk page by page. Loose files put that back to normal, and
; the number is the whole reason this installer exists (2026-07-25).
;
; Build that folder first:
;
;   dotnet publish src\Edgetree\Edgetree.csproj -c Release -r win-x64 --self-contained true -o publish\folder
;
; Then compile this script:
;
;   "C:\Users\MASTER\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\Edgetree.iss
;
; Adapted from TabStick's own script, which has been shipping since 2026-08-01 and takes
; ~80% of that app's downloads - the measurement this round was waiting on.
;
; Settings live in %APPDATA%\Edgetree and are NEVER removed by uninstall - the app owns them,
; the installer only touches Program Files.
;
; NOTE: this file must stay UTF-8 WITH BOM. Inno 6 reads a BOM-less script as ANSI and the
; Korean turns to mojibake in the compiled installer, with no error at compile time.

#define MyAppName "Edgetree"
#define MyAppExe "Edgetree.exe"

; Read out of the exe that is about to be packaged, never typed here: a release already
; carries its number in the csproj and in both README titles, and a fourth copy that only
; shows up in the installer's own version column is the one nobody would notice being wrong.
; GetVersionNumbersString gives four parts ("1.5.0.0"); everything this app publishes is
; three, so the last one is trimmed back off.
#define ExeVersion GetVersionNumbersString("..\publish\folder\" + MyAppExe)
#define MyAppVersion Copy(ExeVersion, 1, RPos(".", ExeVersion) - 1)
#define MyAppPublisher "Edgetree"
#define MyAppUrl "https://github.com/legendsteel11/Edgetree"

[Setup]
; A fixed AppId ties upgrades and the uninstall entry together across versions - never change it.
AppId={{07468C6B-33CF-4624-9A1F-7231C5B845F7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
VersionInfoVersion={#MyAppVersion}.0

; Program Files, 64-bit only. The dialog override lets a user without admin rights fall back
; to a per-user install instead of being turned away.
DefaultDirName={autopf}\{#MyAppName}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

DefaultGroupName={#MyAppName}
DisableProgramGroupPage=auto
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
SetupIconFile=..\src\Edgetree\Resources\app.ico
WizardStyle=modern

; Close a running Edgetree before install/uninstall. The app's single-instance mutex lets
; Setup notice it even when no file handle is obviously locked - and this app is usually
; sitting collapsed at a screen edge or in the tray, where "close the app" is not obvious.
CloseApplications=yes
RestartApplications=no
AppMutex=Local\Edgetree-SingleInstance-8f1d6b2e-4a3f-4c9e-9b1a-2d7e5c6f8a90

; Straight into the release folder the other two builds are copied to, rather than an
; Output\ beside this script - three assets kept in two places is two places to check
; (author, 2026-08-06). The folder is created if it is not there yet, and releases\ is
; gitignored, so the 49MB never goes near a commit.
;
; Name shaped like its neighbours: Edgetree-v1.5.0-win-x64.exe (경량),
; -standalone.exe (포터블), -setup.exe (this). The "v" is this repo's convention and is
; NOT what TabStick's script does - do not copy that half back over.
OutputDir=..\releases\v{#MyAppVersion}
OutputBaseFilename=Edgetree-v{#MyAppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

; Only the "app is running" dialog is overridden - everything else comes from the .isl files.
; The stock line says "모든 인스턴스를 닫은 다음", which is jargon outside developer circles,
; and it never says WHERE the app is. This one can be a 6px strip at the edge of the screen or
; nothing but a tray icon, so naming the exact route out is the part that actually unblocks
; someone: the tray icon's 종료. The settings reassurance earns its line - being told to quit
; for an installer is the moment people expect to lose what they set up.
[Messages]
english.SetupAppRunningError={#MyAppName} is running.%n%nRight-click the {#MyAppName} icon in the notification area and choose "Exit". Your bookmarks and settings are kept.%n%nThen click OK to continue installing, or Cancel to stop.
english.UninstallAppRunningError={#MyAppName} is running.%n%nRight-click the {#MyAppName} icon in the notification area and choose "Exit". Your bookmarks and settings are kept.%n%nThen click OK to continue removing it, or Cancel to stop.
korean.SetupAppRunningError={#MyAppName}가 실행 중입니다.%n%n알림 영역(트레이)의 {#MyAppName} 아이콘을 마우스 오른쪽 버튼으로 눌러 [종료]를 선택해 주세요. 북마크·설정은 그대로 유지됩니다.%n%n종료한 뒤 [확인]을 누르면 설치를 계속하고, [취소]를 누르면 설치를 그만둡니다.
korean.UninstallAppRunningError={#MyAppName}가 실행 중입니다.%n%n알림 영역(트레이)의 {#MyAppName} 아이콘을 마우스 오른쪽 버튼으로 눌러 [종료]를 선택해 주세요. 북마크·설정은 그대로 유지됩니다.%n%n종료한 뒤 [확인]을 누르면 제거를 계속하고, [취소]를 누르면 제거를 그만둡니다.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The whole multi-file publish. pdb is a debug symbol file - kept out of the shipped build.
Source: "..\publish\folder\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

; Launching once at the end is not just a courtesy here: "부팅 후 자동 시작" is a
; HKCU\...\Run value holding the exe's PATH, and someone moving from the portable exe to an
; installed one still has the old path registered. The app rewrites that value from
; Environment.ProcessPath on every launch (MainWindow's TrySetStartWithWindows), so the first
; run from {app} repairs it by itself - as long as there IS a first run.
[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
